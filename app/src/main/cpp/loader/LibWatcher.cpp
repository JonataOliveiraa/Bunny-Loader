#include "loader/LibWatcher.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "runtime/Boot.h"
#include "shadowhook.h"

namespace bl::loader {

namespace {

using InitFn = int (*)(const char*);
InitFn origInit = nullptr;
void* initStub = nullptr;

int hkInit(const char* domainName) {
    // Deixa o IL2CPP terminar: só depois existem domínio, classes e metadados.
    int result = origInit(domainName);
    BL_INFO("il2cpp_init concluido (domain=%s)", domainName ? domainName : "?");
    runtime::boot();
    return result;
}

} // namespace

bool installWatcher() {
    // Idempotente de propósito. Dois caminhos chamam isto:
    //   - o construtor da libbunny (Entry.cpp), herdado do modelo LD_PRELOAD,
    //     que roda no System.loadLibrary("bunny");
    //   - NativeBridge.init(), logo em seguida, vindo do Kotlin.
    // Sem esta guarda o segundo recebia "Duplicate hook" e devolvia JNI_FALSE,
    // o que fazia o launcher anunciar "rodando sem mods" com o núcleo
    // perfeitamente instalado pelo primeiro.
    static bool installed = false;
    if (installed) return true;

    shadowhook_init(SHADOWHOOK_MODE_UNIQUE, false);
    initStub = shadowhook_hook_sym_name(
        "libil2cpp.so",
        "il2cpp_init",
        reinterpret_cast<void*>(hkInit),
        reinterpret_cast<void**>(&origInit));

    if (!initStub && shadowhook_get_errno() != SHADOWHOOK_ERRNO_PENDING) {
        BL_ERROR("Falha ao registrar hook de il2cpp_init: %s",
                 shadowhook_to_errmsg(shadowhook_get_errno()));
        return false;
    }
    installed = true;
    BL_INFO("watcher de il2cpp_init registrado");
    return true;
}

} // namespace bl::loader
