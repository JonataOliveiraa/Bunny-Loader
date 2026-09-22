#include "runtime/Probe.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "runtime/GameRefs.h"

namespace bl::runtime { void installHookTest(); }

#include <chrono>
#include <dlfcn.h>
#include <thread>

namespace bl::runtime {

namespace {

using DomainGetFn = Il2CppDomain* (*)();

// IMPORTANTE: chamar il2cpp_domain_get() durante a janela de inicializacao do
// il2cpp crasha (SIGSEGV) — o caminho do hook so tocaria a API DEPOIS do
// il2cpp_init. Sem poder hookar (houdini), a sonda espera o jogo assentar por
// um tempo fixo antes de tocar em qualquer coisa do il2cpp, e nunca faz poll
// apertado.
constexpr int kSettleMs = 10000;  // tempo ate o jogo chegar ao menu
constexpr int kRetries = 12;
constexpr int kRetryGapMs = 2500;

bool il2cppReady() {
    void* lib = dlopen("libil2cpp.so", RTLD_NOLOAD | RTLD_NOW);
    if (!lib) return false;
    auto domain_get = reinterpret_cast<DomainGetFn>(dlsym(lib, "il2cpp_domain_get"));
    return domain_get && domain_get() != nullptr;
}

void probeThread() {
    BL_INFO("sonda: aguardando o jogo assentar (%d ms)...", kSettleMs);
    std::this_thread::sleep_for(std::chrono::milliseconds(kSettleMs));

    for (int i = 0; i < kRetries; ++i) {
        if (il2cppReady()) {
            BL_INFO("sonda: il2cpp pronto; carregando API");
            auto& a = il2cpp::api();
            if (!a.load()) {
                BL_ERROR("sonda: Api::load() falhou");
                return;
            }
            a.thread_attach(a.domain_get());
            if (resolveGameRefs()) {
                BL_INFO("sonda: RESOLUCAO OK — camada de bind validada no processo do jogo");
                // Testa o hook por endereco (deve funcionar sob houdini, ao
                // contrario do hook pendente por nome).
                installHookTest();
            } else {
                BL_ERROR("sonda: resolveGameRefs falhou");
            }
            return;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(kRetryGapMs));
    }
    BL_ERROR("sonda: il2cpp nao ficou pronto a tempo");
}

} // namespace

void startResolutionProbe() {
    std::thread(probeThread).detach();
}

} // namespace bl::runtime
