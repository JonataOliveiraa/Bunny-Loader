#include "script/dotnet/JitRecorder.h"
#include "core/Config.h"
#include "core/Log.h"

#include <dlfcn.h>
#include <stdlib.h>
#include <sys/system_properties.h>
#include <time.h>

#include <cstdint>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

namespace bl::dotnet {

namespace {

struct MonoMethod;
struct MonoClass;
struct MonoImage;
struct MonoType;
struct MonoMethodSignature;
struct MonoProfiler;
struct MonoProfilerDesc;
using MonoProfilerHandle = MonoProfilerDesc*;

constexpr int kMonoTypeGenericInst = 0x15;

struct Api {
    MonoProfilerHandle (*profilerCreate)(MonoProfiler*);
    void (*setJitBegin)(MonoProfilerHandle, void (*)(MonoProfiler*, MonoMethod*));
    void (*setJitFailed)(MonoProfilerHandle, void (*)(MonoProfiler*, MonoMethod*));
    void (*setJitDone)(MonoProfilerHandle, void (*)(MonoProfiler*, MonoMethod*, void*));
    uint32_t (*methodToken)(MonoMethod*);
    MonoClass* (*methodClass)(MonoMethod*);
    const char* (*methodName)(MonoMethod*);
    MonoMethodSignature* (*methodSignature)(MonoMethod*);
    char* (*methodFullName)(MonoMethod*, int);
    char* (*signatureFullName)(MonoMethodSignature*);
    uint32_t (*signatureParamCount)(MonoMethodSignature*);
    MonoImage* (*classImage)(MonoClass*);
    const char* (*className)(MonoClass*);
    const char* (*classNamespace)(MonoClass*);
    MonoClass* (*classNesting)(MonoClass*);
    MonoType* (*classType)(MonoClass*);
    int (*typeKind)(MonoType*);
    char* (*typeFullName)(MonoType*);
    const char* (*imageName)(MonoImage*);
    const char* (*imageGuid)(MonoImage*);
    void (*free)(void*);
};

Api g_api{};
FILE* g_out = nullptr;
std::mutex g_mutex;

struct Frame {
    uint64_t start;
    uint64_t children;
};
thread_local std::vector<Frame> t_frames;

uint64_t nowNs() {
    timespec ts{};
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return uint64_t(ts.tv_sec) * 1000000000ull + uint64_t(ts.tv_nsec);
}

uint64_t epochMs() {
    timespec ts{};
    clock_gettime(CLOCK_REALTIME, &ts);
    return uint64_t(ts.tv_sec) * 1000ull + uint64_t(ts.tv_nsec) / 1000000ull;
}

void onBegin(MonoProfiler*, MonoMethod*) {
    t_frames.push_back({nowNs(), 0});
}

uint64_t popFrame() {
    if (t_frames.empty()) return 0;
    const Frame f = t_frames.back();
    t_frames.pop_back();
    const uint64_t total = nowNs() - f.start;
    if (!t_frames.empty()) t_frames.back().children += total;
    return total > f.children ? total - f.children : 0;
}

void onFailed(MonoProfiler*, MonoMethod*) {
    popFrame();
}

std::string classPath(MonoClass* klass) {
    std::string out = g_api.className(klass);
    MonoClass* outermost = klass;
    while (MonoClass* outer = g_api.classNesting(outermost)) {
        out = std::string(g_api.className(outer)) + "/" + out;
        outermost = outer;
    }
    return std::string(g_api.classNamespace(outermost)) + "." + out;
}

void onDone(MonoProfiler*, MonoMethod* method, void*) {
    const uint64_t ns = popFrame();
    const uint32_t token = g_api.methodToken(method);
    MonoClass* klass = g_api.methodClass(method);
    MonoImage* image = klass ? g_api.classImage(klass) : nullptr;
    const char* guid = image ? g_api.imageGuid(image) : nullptr;
    if (token == 0 || !image || !guid) {
        std::lock_guard<std::mutex> lock(g_mutex);
        if (g_out) std::fprintf(g_out, "W\t%llu\t%llu\n", static_cast<unsigned long long>(ns / 1000),
                                static_cast<unsigned long long>(epochMs()));
        return;
    }
    MonoMethodSignature* sig = g_api.methodSignature(method);
    char* sigName = sig ? g_api.signatureFullName(sig) : nullptr;
    char* full = g_api.methodFullName(method, 0);
    const char* colon = full ? std::strrchr(full, ':') : nullptr;
    const bool methodInst = colon && std::strchr(colon, '<');
    MonoType* type = g_api.classType(klass);
    const bool classInst = type && g_api.typeKind(type) == kMonoTypeGenericInst;
    const std::string path = classPath(klass);
    char* typeName = classInst ? g_api.typeFullName(type) : nullptr;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        if (g_out) {
            std::fprintf(g_out, "M\t%s\t%s\t%s\t%d\t%s\t%u\t%s\t%d\t%llu\t%llu\t%s\t%s\n",
                         g_api.imageName(image), guid, path.c_str(), classInst ? 1 : 0,
                         g_api.methodName(method), sig ? g_api.signatureParamCount(sig) : 0u,
                         sigName ? sigName : "", methodInst ? 1 : 0,
                         static_cast<unsigned long long>(ns / 1000),
                         static_cast<unsigned long long>(epochMs()),
                         typeName ? typeName : "-", methodInst && full ? full : "-");
        }
    }
    if (typeName) g_api.free(typeName);
    if (sigName) g_api.free(sigName);
    if (full) g_api.free(full);
}

int requestedLevel() {
    char value[PROP_VALUE_MAX] = {};
    __system_property_get("debug.bl.jitprofile", value);
    return value[0] >= '1' && value[0] <= '9' ? value[0] - '0' : 0;
}

template <typename T>
bool resolve(void* lib, T& slot, const char* name) {
    slot = reinterpret_cast<T>(dlsym(lib, name));
    if (!slot) BL_WARN("JIT recorder: missing Mono symbol %s", name);
    return slot != nullptr;
}

}

void prepareJitRecorder() {
    if (requestedLevel() < 2) return;
    setenv("MONO_LOG_LEVEL", "debug", 1);
    setenv("MONO_LOG_MASK", "aot", 1);
}

void startJitRecorder() {
    if (requestedLevel() == 0) return;
    void* lib = dlopen("libmonosgen-2.0.so", RTLD_NOW | RTLD_NOLOAD);
    if (!lib) return;
    bool ok = true;
    ok &= resolve(lib, g_api.profilerCreate, "mono_profiler_create");
    ok &= resolve(lib, g_api.setJitBegin, "mono_profiler_set_jit_begin_callback");
    ok &= resolve(lib, g_api.setJitFailed, "mono_profiler_set_jit_failed_callback");
    ok &= resolve(lib, g_api.setJitDone, "mono_profiler_set_jit_done_callback");
    ok &= resolve(lib, g_api.methodToken, "mono_method_get_token");
    ok &= resolve(lib, g_api.methodClass, "mono_method_get_class");
    ok &= resolve(lib, g_api.methodName, "mono_method_get_name");
    ok &= resolve(lib, g_api.methodSignature, "mono_method_signature");
    ok &= resolve(lib, g_api.methodFullName, "mono_method_full_name");
    ok &= resolve(lib, g_api.signatureFullName, "mono_signature_full_name");
    ok &= resolve(lib, g_api.signatureParamCount, "mono_signature_get_param_count");
    ok &= resolve(lib, g_api.classImage, "mono_class_get_image");
    ok &= resolve(lib, g_api.className, "mono_class_get_name");
    ok &= resolve(lib, g_api.classNamespace, "mono_class_get_namespace");
    ok &= resolve(lib, g_api.classNesting, "mono_class_get_nesting_type");
    ok &= resolve(lib, g_api.classType, "mono_class_get_type");
    ok &= resolve(lib, g_api.typeKind, "mono_type_get_type");
    ok &= resolve(lib, g_api.typeFullName, "mono_type_full_name");
    ok &= resolve(lib, g_api.imageName, "mono_image_get_name");
    ok &= resolve(lib, g_api.imageGuid, "mono_image_get_guid");
    ok &= resolve(lib, g_api.free, "mono_free");
    if (!ok) return;

    std::string path = config().logPath;
    const size_t slash = path.rfind('/');
    path = (slash == std::string::npos ? std::string(".") : path.substr(0, slash)) + "/jitprofile.txt";
    g_out = std::fopen(path.c_str(), "w");
    if (!g_out) {
        BL_WARN("JIT recorder: cannot open %s", path.c_str());
        return;
    }
    std::setvbuf(g_out, nullptr, _IOLBF, 1 << 16);

    MonoProfilerHandle handle = g_api.profilerCreate(nullptr);
    g_api.setJitBegin(handle, onBegin);
    g_api.setJitFailed(handle, onFailed);
    g_api.setJitDone(handle, onDone);
    BL_INFO("JIT recorder: on (debug.bl.jitprofile=1), writing %s", path.c_str());
}

}
