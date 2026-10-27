// O runtime .NET do app do catálogo: sobe o Mono (o mesmo pacote do app, ver
// app/build.gradle.kts) uma vez e chama o BunnyLoader.Compiler.NativeEntry.Build
// a cada mod C# com fontes. O Kotlin (CompilerRuntime.kt) chama daqui sempre da
// mesma thread, a do Bridge.
#include <android/log.h>
#include <dirent.h>
#include <dlfcn.h>
#include <jni.h>
#include <stdlib.h>

#include <cstdint>
#include <mutex>
#include <string>

namespace {

#define LOG(...) __android_log_print(ANDROID_LOG_INFO, "BLCatalog", __VA_ARGS__)

struct MonoDomain;
struct MonoAssembly;
struct MonoImage;
struct MonoClass;
struct MonoMethod;

// O tamanho fixo da ABI (mono/utils/mono-error.h), como no MonoApi.h do app.
struct MonoError {
    uint16_t errorCode;
    uint16_t flags;
    void* hiddenFields[12];
};

using BuildFn = char* (*)(const char* entry, const char* runtimeDir, int force);

std::mutex g_lock;
BuildFn g_build = nullptr;
std::string g_failure;

template <typename T>
bool symbol(void* lib, const char* name, T* out) {
    *out = reinterpret_cast<T>(dlsym(lib, name));
    if (!*out) g_failure = std::string("libmonosgen-2.0.so has no ") + name;
    return *out != nullptr;
}

std::string assemblies(const std::string& dir) {
    std::string out;
    if (DIR* d = opendir(dir.c_str())) {
        while (dirent* e = readdir(d)) {
            const std::string name = e->d_name;
            if (name.size() <= 4 || name.compare(name.size() - 4, 4, ".dll") != 0) continue;
            if (!out.empty()) out += ':';
            out += dir + "/" + name;
        }
        closedir(d);
    }
    return out;
}

/** monovm_initialize + mono_jit_init e o ponteiro do NativeEntry.Build. */
bool start(const std::string& runtimeDir, const std::string& nativeDir) {
    void* lib = dlopen("libmonosgen-2.0.so", RTLD_NOW | RTLD_GLOBAL);
    if (!lib) {
        g_failure = std::string("dlopen libmonosgen-2.0.so: ") + dlerror();
        return false;
    }
    int (*monovm_initialize)(int, const char**, const char**);
    MonoDomain* (*mono_jit_init_version)(const char*, const char*);
    MonoAssembly* (*mono_domain_assembly_open)(MonoDomain*, const char*);
    MonoImage* (*mono_assembly_get_image)(MonoAssembly*);
    MonoClass* (*mono_class_from_name)(MonoImage*, const char*, const char*);
    MonoMethod* (*mono_class_get_method_from_name)(MonoClass*, const char*, int);
    void* (*mono_method_get_unmanaged_callers_only_ftnptr)(MonoMethod*, MonoError*);
    const char* (*mono_error_get_message)(MonoError*);
    if (!symbol(lib, "monovm_initialize", &monovm_initialize) ||
        !symbol(lib, "mono_jit_init_version", &mono_jit_init_version) ||
        !symbol(lib, "mono_domain_assembly_open", &mono_domain_assembly_open) ||
        !symbol(lib, "mono_assembly_get_image", &mono_assembly_get_image) ||
        !symbol(lib, "mono_class_from_name", &mono_class_from_name) ||
        !symbol(lib, "mono_class_get_method_from_name", &mono_class_get_method_from_name) ||
        !symbol(lib, "mono_method_get_unmanaged_callers_only_ftnptr", &mono_method_get_unmanaged_callers_only_ftnptr) ||
        !symbol(lib, "mono_error_get_message", &mono_error_get_message)) {
        return false;
    }

    // A suspensão cooperativa, como no jogo: o Mono não manda sinais às threads.
    setenv("MONO_THREADS_SUSPEND", "coop", 1);
    const std::string tpa = assemblies(runtimeDir);
    const std::string nativeDirs = nativeDir + ":";
    const std::string baseDir = runtimeDir + "/";
    const char* keys[] = {
        "TRUSTED_PLATFORM_ASSEMBLIES", "NATIVE_DLL_SEARCH_DIRECTORIES", "APP_CONTEXT_BASE_DIRECTORY",
        "RUNTIME_IDENTIFIER", "System.Globalization.Invariant",
    };
    const char* values[] = { tpa.c_str(), nativeDirs.c_str(), baseDir.c_str(), "android-arm64", "true" };
    if (monovm_initialize(sizeof(keys) / sizeof(keys[0]), keys, values) != 0) {
        g_failure = "monovm_initialize failed";
        return false;
    }
    MonoDomain* domain = mono_jit_init_version("BLCatalog", "v4.0.30319");
    if (!domain) {
        g_failure = "mono_jit_init_version failed";
        return false;
    }
    const std::string path = runtimeDir + "/BunnyLoader.Compiler.dll";
    MonoAssembly* assembly = mono_domain_assembly_open(domain, path.c_str());
    MonoImage* image = assembly ? mono_assembly_get_image(assembly) : nullptr;
    MonoClass* entry = image ? mono_class_from_name(image, "BunnyLoader.Compiler", "NativeEntry") : nullptr;
    MonoMethod* method = entry ? mono_class_get_method_from_name(entry, "Build", 3) : nullptr;
    if (!method) {
        g_failure = "BunnyLoader.Compiler.NativeEntry.Build not found in " + path;
        return false;
    }
    MonoError error{};
    g_build = reinterpret_cast<BuildFn>(mono_method_get_unmanaged_callers_only_ftnptr(method, &error));
    if (!g_build) {
        g_failure = std::string("NativeEntry.Build: ") + mono_error_get_message(&error);
        return false;
    }
    LOG("compiler: Mono started from %s", runtimeDir.c_str());
    return true;
}

std::string utf8(JNIEnv* env, jstring value) {
    const char* chars = env->GetStringUTFChars(value, nullptr);
    std::string out = chars ? chars : "";
    if (chars) env->ReleaseStringUTFChars(value, chars);
    return out;
}

} // namespace

// O relatório volta em bytes UTF-8: o NewStringUTF só aceita o UTF-8
// modificado do Java, e um erro do compilador pode citar qualquer caractere.
extern "C" JNIEXPORT jbyteArray JNICALL
Java_dev_bunnyloader_catalog_CompilerRuntime_nativeBuild(JNIEnv* env, jclass, jstring runtimeDir,
                                                         jstring nativeDir, jstring entry, jboolean force) {
    std::lock_guard<std::mutex> guard(g_lock);
    const std::string runtime = utf8(env, runtimeDir);
    if (!g_build && g_failure.empty() && !start(runtime, utf8(env, nativeDir))) LOG("compiler: %s", g_failure.c_str());
    std::string report;
    if (!g_build) {
        report = "status failed\nerror " + g_failure + "\n";
    } else {
        char* text = g_build(utf8(env, entry).c_str(), runtime.c_str(), force ? 1 : 0);
        report = text ? text : "status failed\nerror no report\n";
        free(text);
    }
    jbyteArray out = env->NewByteArray(static_cast<jsize>(report.size()));
    env->SetByteArrayRegion(out, 0, static_cast<jsize>(report.size()), reinterpret_cast<const jbyte*>(report.data()));
    return out;
}
