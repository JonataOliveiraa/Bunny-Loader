#include "script/dotnet/DotnetHost.h"
#include "script/dotnet/JitRecorder.h"
#include "script/dotnet/MonoApi.h"
#include "script/dotnet/NativeApi.h"
#include "core/Config.h"
#include "core/Log.h"

#include <dirent.h>
#include <dlfcn.h>
#include <signal.h>
#include <stdlib.h>

#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

namespace bl::dotnet {

namespace {

std::vector<std::string> listAssemblies(const std::string& dir) {
    std::vector<std::string> out;
    DIR* d = opendir(dir.c_str());
    if (!d) return out;
    while (dirent* e = readdir(d)) {
        const std::string name = e->d_name;
        if (isAssemblyEntry(name)) out.push_back(dir + "/" + name);
    }
    closedir(d);
    return out;
}

std::string joined(const std::vector<std::string>& parts) {
    std::string out;
    for (const std::string& p : parts) {
        if (!out.empty()) out += ':';
        out += p;
    }
    return out;
}

struct SignalSnapshot {
    int signal;
    struct sigaction action;
};

const char* handlerOwner(const struct sigaction& sa) {
    void* fn = (sa.sa_flags & SA_SIGINFO) ? reinterpret_cast<void*>(sa.sa_sigaction)
                                          : reinterpret_cast<void*>(sa.sa_handler);
    Dl_info info{};
    if (fn && dladdr(fn, &info) && info.dli_fname) {
        const char* slash = strrchr(info.dli_fname, '/');
        return slash ? slash + 1 : info.dli_fname;
    }
    return fn ? "?" : "default";
}

bool sameHandler(const struct sigaction& a, const struct sigaction& b) {
    return a.sa_sigaction == b.sa_sigaction && a.sa_flags == b.sa_flags;
}

class GameGcPause {
public:
    GameGcPause() {
        void* lib = dlopen("libil2cpp.so", RTLD_NOLOAD | RTLD_NOW);
        if (lib) {
            disable_ = reinterpret_cast<void (*)()>(dlsym(lib, "il2cpp_gc_disable"));
            enable_ = reinterpret_cast<void (*)()>(dlsym(lib, "il2cpp_gc_enable"));
        }
        if (!disable_ || !enable_) {
            BL_WARN("dotnet: il2cpp_gc_disable/enable not found; the game collector keeps running during Mono init");
            disable_ = enable_ = nullptr;
        }
        for (int sig : {SIGPWR, SIGXCPU}) {
            SignalSnapshot s{sig, {}};
            sigaction(sig, nullptr, &s.action);
            saved_.push_back(s);
        }
        if (disable_) disable_();
    }

    ~GameGcPause() {
        for (const SignalSnapshot& s : saved_) {
            struct sigaction now{};
            sigaction(s.signal, nullptr, &now);
            if (sameHandler(now, s.action)) continue;
            BL_DEBUG("dotnet: signal %d handler moved from %s to %s; restoring",
                     s.signal, handlerOwner(s.action), handlerOwner(now));
            sigaction(s.signal, &s.action, nullptr);
        }
        if (enable_) enable_();
    }

private:
    void (*disable_)() = nullptr;
    void (*enable_)() = nullptr;
    std::vector<SignalSnapshot> saved_;
};

std::string aotModuleSymbol(const std::string& file) {
    std::string name = file.substr(0, file.size() - strlen(".dll.so"));
    for (char& c : name) {
        const bool keep = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
        if (!keep) c = '_';
    }
    return "mono_aot_module_" + name + "_info";
}

std::string aotFolder(const std::string& dotnetDir) {
    FILE* f = fopen((dotnetDir + "/aot-key.txt").c_str(), "r");
    if (!f) return "";
    char key[128] = {};
    const bool read = fgets(key, sizeof(key), f) != nullptr;
    fclose(f);
    if (!read) return "";
    std::string k = key;
    while (!k.empty() && (k.back() == '\n' || k.back() == '\r' || k.back() == ' ')) k.pop_back();
    for (char c : k) {
        const bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '.';
        if (!ok) return "";
    }
    const size_t slash = dotnetDir.rfind('/');
    if (k.empty() || slash == std::string::npos) return "";
    return dotnetDir.substr(0, slash) + "/aot/" + k;
}

void registerAotModules(const std::string& dir) {
    if (dir.empty()) return;
    DIR* d = opendir(dir.c_str());
    if (!d) return;
    int count = 0;
    while (dirent* e = readdir(d)) {
        const std::string name = e->d_name;
        if (name.size() <= 7 || name.compare(name.size() - 7, 7, ".dll.so") != 0) continue;
        const std::string path = dir + "/" + name;
        void* lib = dlopen(path.c_str(), RTLD_NOW | RTLD_LOCAL);
        if (!lib) {
            BL_WARN("dotnet: AOT image %s did not open: %s", name.c_str(), dlerror());
            continue;
        }
        const std::string symbol = aotModuleSymbol(name);
        auto* slot = reinterpret_cast<void**>(dlsym(lib, symbol.c_str()));
        if (!slot || !*slot) {
            BL_WARN("dotnet: AOT image %s has no %s", name.c_str(), symbol.c_str());
            dlclose(lib);
            continue;
        }
        mono().mono_aot_register_module(reinterpret_cast<void**>(*slot));
        ++count;
    }
    closedir(d);
    if (count) BL_INFO("dotnet: %d AOT image(s) registered from %s", count, dir.c_str());
}

MonoDomain* initRuntime(const std::string& dir) {
    auto& m = mono();
    prepareJitRecorder();
    setenv("MONO_THREADS_SUSPEND", "coop", 1);
    m.mono_set_signal_chaining(1);
    m.mono_set_crash_chaining(1);
    m.mono_set_assemblies_path(dir.c_str());

    const std::string tpa = joined(listAssemblies(dir));
    const std::string nativeDirs = config().gameLibDir + ":";
    const std::string baseDir = dir + "/";
    const char* keys[] = {
        "TRUSTED_PLATFORM_ASSEMBLIES",
        "NATIVE_DLL_SEARCH_DIRECTORIES",
        "APP_CONTEXT_BASE_DIRECTORY",
        "RUNTIME_IDENTIFIER",
        "System.Globalization.Invariant",
    };
    const char* values[] = {
        tpa.c_str(),
        nativeDirs.c_str(),
        baseDir.c_str(),
        "android-arm64",
        "true",
    };
    if (m.monovm_initialize(sizeof(keys) / sizeof(keys[0]), keys, values) != 0) {
        BL_ERROR("dotnet: monovm_initialize failed");
        return nullptr;
    }
    startJitRecorder();
    registerAotModules(aotFolder(dir));
    MonoDomain* domain = m.mono_jit_init_version("BunnyLoader", "v4.0.30319");
    if (!domain) BL_ERROR("dotnet: mono_jit_init_version failed");
    return domain;
}

}

DotnetHost& host() {
    static DotnetHost instance;
    return instance;
}

bool isAssemblyEntry(const std::string& entry) {
    return entry.size() > 4 && entry.compare(entry.size() - 4, 4, ".dll") == 0;
}

bool isCSharpEntry(const std::string& entry) {
    return isAssemblyEntry(entry) || (entry.size() > 3 && entry.compare(entry.size() - 3, 3, ".cs") == 0);
}

bool DotnetHost::start() {
    if (started_) return true;
    if (failed_) return false;
    failed_ = true;

    const std::string& dir = config().dotnetDir;
    if (dir.empty()) {
        BL_ERROR("dotnet: no runtime folder configured; C# mods disabled");
        return false;
    }
    auto& m = mono();
    if (!m.load()) return false;

    MonoDomain* domain = nullptr;
    {
        GameGcPause pause;
        domain = initRuntime(dir);
    }
    if (!domain) return false;

    if (char* info = m.mono_get_runtime_build_info()) {
        BL_INFO("dotnet: Mono %s started", info);
        m.mono_free(info);
    }

    const std::string apiPath = dir + "/BunnyLoader.dll";
    MonoAssembly* assembly = m.mono_domain_assembly_open(domain, apiPath.c_str());
    MonoImage* image = assembly ? m.mono_assembly_get_image(assembly) : nullptr;
    MonoClass* hostClass = image ? m.mono_class_from_name(image, "BunnyLoader", "Host") : nullptr;
    if (!hostClass) {
        BL_ERROR("dotnet: BunnyLoader.Host not found in %s", apiPath.c_str());
        return false;
    }
    auto entryPoint = [&](const char* name, int argc) -> void* {
        MonoMethod* method = m.mono_class_get_method_from_name(hostClass, name, argc);
        if (!method) {
            BL_ERROR("dotnet: BunnyLoader.Host.%s not found", name);
            return nullptr;
        }
        MonoError error{};
        void* fn = m.mono_method_get_unmanaged_callers_only_ftnptr(method, &error);
        if (!fn) {
            BL_ERROR("dotnet: BunnyLoader.Host.%s: %s", name, m.mono_error_get_message(&error));
            m.mono_error_cleanup(&error);
        }
        return fn;
    };
    auto initialize = reinterpret_cast<int (*)(const void*)>(entryPoint("Initialize", 1));
    loadModFn_ = reinterpret_cast<int (*)(const char*, const char*)>(entryPoint("LoadMod", 2));
    if (!initialize || !loadModFn_) return false;
    consoleSubmitFn_ = reinterpret_cast<void (*)(const char*)>(entryPoint("ConsoleSubmit", 1));
    consoleTickFn_ = reinterpret_cast<void (*)()>(entryPoint("ConsoleTick", 0));
    if (!initialize(nativeApiTable())) {
        BL_ERROR("dotnet: BunnyLoader.Host.Initialize refused the native table");
        return false;
    }

    failed_ = false;
    started_ = true;
    return true;
}

bool DotnetHost::loadMod(const std::string& id, const std::string& path) {
    if (!start()) {
        BL_ERROR("mod %s: the .NET runtime did not start", id.c_str());
        return false;
    }
    const bool ok = loadModFn_(id.c_str(), path.c_str()) != 0;
    BL_DEBUG("dotnet: mod %s returned from Host.LoadMod (%s)", id.c_str(), ok ? "ok" : "failed");
    return ok;
}

bool DotnetHost::consoleSubmit(const std::string& code) {
    if (!started_ || !consoleSubmitFn_) return false;
    consoleSubmitFn_(code.c_str());
    return true;
}

void DotnetHost::consoleTick() {
    if (started_ && consoleTickFn_) consoleTickFn_();
}

void DotnetHost::finishLoading() {
    if (!start()) return;
    auto& m = mono();
    void* stackdata = nullptr;
    m.mono_threads_enter_gc_unsafe_region_unbalanced(&stackdata);
    if (MonoThread* thread = m.mono_thread_current()) m.mono_thread_detach(thread);
    BL_DEBUG("dotnet: load thread detached");
}

}
