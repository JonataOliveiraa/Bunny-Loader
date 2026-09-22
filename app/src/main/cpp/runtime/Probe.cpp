#include "runtime/Probe.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "runtime/GameRefs.h"

#include <chrono>
#include <dlfcn.h>
#include <thread>

namespace bl::runtime {

namespace {

using DomainGetFn = Il2CppDomain* (*)();

// Espera a libil2cpp.so carregar E o il2cpp_init terminar. Antes do init,
// il2cpp_domain_get() retorna null. Timeout generoso: o jogo demora a subir.
bool waitForIl2cpp(int timeoutMs) {
    const int stepMs = 200;
    for (int waited = 0; waited < timeoutMs; waited += stepMs) {
        void* lib = dlopen("libil2cpp.so", RTLD_NOLOAD | RTLD_NOW);
        if (lib) {
            auto domain_get = reinterpret_cast<DomainGetFn>(dlsym(lib, "il2cpp_domain_get"));
            if (domain_get && domain_get() != nullptr) {
                return true;
            }
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(stepMs));
    }
    return false;
}

void probeThread() {
    BL_INFO("sonda: aguardando il2cpp_init...");
    if (!waitForIl2cpp(60000)) {
        BL_ERROR("sonda: il2cpp nao inicializou em 60s");
        return;
    }
    BL_INFO("sonda: il2cpp pronto; carregando API");

    auto& a = il2cpp::api();
    if (!a.load()) {
        BL_ERROR("sonda: Api::load() falhou");
        return;
    }
    // A thread precisa estar anexada ao dominio para chamar a API com seguranca.
    a.thread_attach(a.domain_get());

    if (resolveGameRefs()) {
        BL_INFO("sonda: RESOLUCAO OK — camada de bind validada no processo do jogo");
    } else {
        BL_ERROR("sonda: resolveGameRefs falhou");
    }
}

} // namespace

void startResolutionProbe() {
    std::thread(probeThread).detach();
}

} // namespace bl::runtime
