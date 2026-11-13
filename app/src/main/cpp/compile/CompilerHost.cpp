#include <android/log.h>
#include <dirent.h>
#include <dlfcn.h>
#include <jni.h>
#include <stdlib.h>

#include <cstdint>
#include <cstring>
#include <string>

#define COMPILE_LOG(priority, ...) __android_log_print(priority, "BunnyLoader", __VA_ARGS__)

namespace {

struct MonoDomain;
struct MonoAssembly;
struct MonoImage;
struct MonoClass;
struct MonoMethod;

struct MonoError {
    uint16_t errorCode;
    uint16_t flags;
    void* hiddenFields[12];
};

using BuildFn = char* (*)(const char*, const char*, const char*, int32_t);

BuildFn g_build = nullptr;
void (*g_free)(void*) = nullptr;

template <typename T>
bool resolve(void* lib, T& slot, const char* name) {
    slot = reinterpret_cast<T>(dlsym(lib, name));
    if (!slot) COMPILE_LOG(ANDROID_LOG_ERROR, "compile: missing Mono symbol %s", name);
    return slot != nullptr;
}

std::string assembliesIn(const std::string& dir) {
    std::string out;
    DIR* d = opendir(dir.c_str());
    if (!d) return out;
    while (dirent* e = readdir(d)) {
        const std::string name = e->d_name;
        if (name.size() <= 4 || name.compare(name.size() - 4, 4, ".dll") != 0) continue;
        if (!out.empty()) out += ':';
        out += dir + "/" + name;
    }
    closedir(d);
    return out;
}

std::string toString(JNIEnv* env, jstring value) {
    const char* chars = env->GetStringUTFChars(value, nullptr);
    std::string out = chars ? chars : "";
    if (chars) env->ReleaseStringUTFChars(value, chars);
    return out;
}

std::string startMono(const std::string& dir, const std::string& libDir) {
    void* lib = dlopen("libmonosgen-2.0.so", RTLD_NOW | RTLD_GLOBAL);
    if (!lib) return std::string("libmonosgen-2.0.so: ") + dlerror();

    int (*initialize)(int, const char**, const char**) = nullptr;
    void (*setAssembliesPath)(const char*) = nullptr;
    void (*setSignalChaining)(int) = nullptr;
    void (*setCrashChaining)(int) = nullptr;
    MonoDomain* (*initVersion)(const char*, const char*) = nullptr;
    MonoAssembly* (*openAssembly)(MonoDomain*, const char*) = nullptr;
    MonoImage* (*imageOf)(MonoAssembly*) = nullptr;
    MonoClass* (*classFromName)(MonoImage*, const char*, const char*) = nullptr;
    MonoMethod* (*methodFromName)(MonoClass*, const char*, int) = nullptr;
    void* (*callersOnly)(MonoMethod*, MonoError*) = nullptr;
    const char* (*errorMessage)(MonoError*) = nullptr;

    if (!resolve(lib, initialize, "monovm_initialize") ||
        !resolve(lib, setAssembliesPath, "mono_set_assemblies_path") ||
        !resolve(lib, setSignalChaining, "mono_set_signal_chaining") ||
        !resolve(lib, setCrashChaining, "mono_set_crash_chaining") ||
        !resolve(lib, initVersion, "mono_jit_init_version") ||
        !resolve(lib, openAssembly, "mono_domain_assembly_open") ||
        !resolve(lib, imageOf, "mono_assembly_get_image") ||
        !resolve(lib, classFromName, "mono_class_from_name") ||
        !resolve(lib, methodFromName, "mono_class_get_method_from_name") ||
        !resolve(lib, callersOnly, "mono_method_get_unmanaged_callers_only_ftnptr") ||
        !resolve(lib, errorMessage, "mono_error_get_message") ||
        !resolve(lib, g_free, "mono_free")) {
        return "a Mono symbol is missing";
    }

    setenv("MONO_THREADS_SUSPEND", "coop", 1);
    setSignalChaining(1);
    setCrashChaining(1);
    setAssembliesPath(dir.c_str());

    const std::string tpa = assembliesIn(dir);
    const std::string nativeDirs = libDir + ":";
    const std::string baseDir = dir + "/";
    const char* keys[] = {
        "TRUSTED_PLATFORM_ASSEMBLIES",
        "NATIVE_DLL_SEARCH_DIRECTORIES",
        "APP_CONTEXT_BASE_DIRECTORY",
        "RUNTIME_IDENTIFIER",
        "System.Globalization.Invariant",
    };
    const char* values[] = {tpa.c_str(), nativeDirs.c_str(), baseDir.c_str(), "android-arm64", "true"};
    if (initialize(sizeof(keys) / sizeof(keys[0]), keys, values) != 0) return "monovm_initialize failed";
    MonoDomain* domain = initVersion("BunnyLoaderCompile", "v4.0.30319");
    if (!domain) return "mono_jit_init_version failed";

    const std::string path = dir + "/BunnyLoader.Compiler.dll";
    MonoAssembly* assembly = openAssembly(domain, path.c_str());
    MonoImage* image = assembly ? imageOf(assembly) : nullptr;
    MonoClass* entry = image ? classFromName(image, "BunnyLoader.Compiler", "NativeEntry") : nullptr;
    MonoMethod* method = entry ? methodFromName(entry, "Build", 4) : nullptr;
    if (!method) return "BunnyLoader.Compiler.NativeEntry.Build not found";
    MonoError error{};
    g_build = reinterpret_cast<BuildFn>(callersOnly(method, &error));
    if (!g_build) return std::string("NativeEntry.Build: ") + errorMessage(&error);
    return "";
}

}

extern "C" {

JNIEXPORT jstring JNICALL Java_dev_bunnyloader_compile_CompilerNative_start(JNIEnv* env, jobject, jstring dir,
                                                                            jstring libDir) {
    const std::string error = startMono(toString(env, dir), toString(env, libDir));
    if (!error.empty()) COMPILE_LOG(ANDROID_LOG_ERROR, "compile: %s", error.c_str());
    return env->NewStringUTF(error.c_str());
}

JNIEXPORT jbyteArray JNICALL Java_dev_bunnyloader_compile_CompilerNative_build(JNIEnv* env, jobject, jstring entry,
                                                                               jstring runtimeDir, jstring references) {
    if (!g_build) return env->NewByteArray(0);
    char* report = g_build(toString(env, entry).c_str(), toString(env, runtimeDir).c_str(),
                           toString(env, references).c_str(), 0);
    if (!report) return env->NewByteArray(0);
    const auto size = static_cast<jsize>(strlen(report));
    jbyteArray out = env->NewByteArray(size);
    env->SetByteArrayRegion(out, 0, size, reinterpret_cast<const jbyte*>(report));
    g_free(report);
    return out;
}

}
