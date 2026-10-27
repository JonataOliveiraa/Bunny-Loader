#include "script/dotnet/DotnetHost.h"
#include "script/dotnet/MonoApi.h"
#include "script/dotnet/NativeApi.h"
#include "core/Config.h"
#include "core/Log.h"

#include <dirent.h>
#include <dlfcn.h>
#include <signal.h>
#include <stdlib.h>

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

/**
 * Os sinais de parar o mundo do Boehm (o coletor do IL2CPP). O SGen do Mono
 * usa os MESMOS no Android (SIGPWR para suspender, SIGXCPU para retomar), e o
 * manipulador instalado por ultimo ganha: se o do Mono ficasse, a coleta do
 * jogo mandaria o sinal para uma thread que o Mono nao conhece e esperaria a
 * resposta para sempre. Com a suspensao cooperativa (MONO_THREADS_SUSPEND),
 * o Mono nao manda esses sinais; os do Boehm voltam ao lugar depois da
 * inicializacao.
 */
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

/**
 * A janela da inicializacao do Mono. O mono_jit_init instala os manipuladores
 * dele para SIGPWR/SIGXCPU logo no comeco e so devolve o controle segundos
 * depois (no MuMu, com o houdini): uma coleta do jogo nesse meio manda o
 * sinal para as threads do jogo, o manipulador do Mono nao as conhece e
 * aborta ("mono_thread_info_current: info", 2026-10-26). Entao o coletor do
 * jogo fica parado do comeco ao fim, e os manipuladores do Boehm voltam ANTES
 * de ele ser religado.
 */
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

/** monovm_initialize + mono_jit_init, com os assemblies de `dir`. */
MonoDomain* initRuntime(const std::string& dir) {
    auto& m = mono();
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
    MonoDomain* domain = m.mono_jit_init_version("BunnyLoader", "v4.0.30319");
    if (!domain) BL_ERROR("dotnet: mono_jit_init_version failed");
    return domain;
}

} // namespace

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

void DotnetHost::finishLoading() {
    if (!started_) return;
    auto& m = mono();
    // O detach desfaz o registro inteiro, entao a volta ao BLOCKING nao
    // precisa do par (exit_gc_unsafe).
    void* stackdata = nullptr;
    m.mono_threads_enter_gc_unsafe_region_unbalanced(&stackdata);
    if (MonoThread* thread = m.mono_thread_current()) m.mono_thread_detach(thread);
    BL_DEBUG("dotnet: load thread detached");
}

} // namespace bl::dotnet
