#include "boot/Probe.h"
#include "boot/FramePacing.h"
#include "boot/QuickStart.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "content/common/GameRefs.h"

#include "script/bridge/ScriptEngine.h"
#include "mods/ModLoader.h"
#include "core/Config.h"
#include "menu/Cheats.h"
#include "content/items/ModItemSave.h"
#include "content/tiles/ModTileSave.h"
#include "content/npcs/ModNpcSave.h"
#include "content/items/ModItems.h"
#include "content/npcs/ModNpcs.h"
#include "menu/CheatButton.h"
#include "menu/DevTools.h"

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
    // O prazo fixo, com uma saida: com o inicio rapido, o hook do splash
    // avisa quando o jogo termina de carregar (Main._isAsyncLoadComplete).
    // Nesse ponto a thread do jogo so desenha o splash; e a janela mais calma
    // para carregar os mods. Nao da para decidir aqui pela config: esta thread
    // nasce no load da libbunny, ANTES de o NativeBridge.init entregar a
    // config. Sem o inicio rapido ninguem avisa, e a espera e a de sempre.
    BL_DEBUG("sonda: aguardando o jogo assentar (no maximo %d ms)...", kSettleMs);
    waitForGameLoaded(std::chrono::milliseconds(kSettleMs));

    for (int i = 0; i < kRetries; ++i) {
        if (il2cppReady()) {
            BL_DEBUG("sonda: il2cpp pronto; carregando API");
            auto& a = il2cpp::api();
            if (!a.load()) {
                BL_ERROR("sonda: Api::load() falhou");
                return;
            }
            a.thread_attach(a.domain_get());
            if (!resolveGameRefs()) {
                BL_ERROR("sonda: resolveGameRefs falhou");
                return;
            }
            BL_DEBUG("sonda: RESOLUCAO OK");

            // Sobe o QuickJS e carrega os mods habilitados (main.js de cada um).
            if (!script::engine().init()) {
                BL_ERROR("sonda: QuickJS nao iniciou");
                return;
            }
            mods::loadAll(config().modsDir, config().enabledMods);
            // Sem pasta configurada, usa os embutidos. Uma lista vazia enviada
            // pelo launcher significa que o usuario desativou todos os mods.
            if (config().modsDir.empty() && mods::loadedCount() == 0) {
                BL_INFO("sonda: sem pasta de mods configurada; usando embutidos");
                mods::loadBuiltins();
            }
            BL_INFO("sonda: %zu mod(s) carregado(s)", mods::loadedCount());
            // Depois dos mods: os ids deles ficam os mesmos de sempre.
            registerUnloadedPool();
            prepareModNpcs();

            // Menu de cheats (acoes nativas) + botao flutuante na Activity.
            installCheats();
            installDevTools();
            installFramePacing();
            installModItemSave();
            installModTileSave();
            installModNpcSave();
            ui::installCheatButton();
            // Inicio rapido: sem o watcher do il2cpp_init o hook do splash
            // entra so agora; com os mods carregados, o splash pode acabar.
            installFastIntro();
            markCoreReady();
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
