#include "boot/Probe.h"
#include "boot/FramePacing.h"
#include "graphics/RenderHook.h"
#include "boot/QuickStart.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
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
#include "content/walls/ModWalls.h"
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

constexpr int kLoadPollMs = 100;
constexpr int kLoadWaitMaxMs = 180000;

/**
 * Espera a carga do proprio jogo acabar (Main._isAsyncLoadComplete) antes de
 * carregar os mods.
 *
 * O prazo fixo de kSettleMs supunha o jogo pronto em 10 s. Com a CPU ocupada
 * a carga dele passa disso, e a dos mods corria junto: o LoadContent_Deferred
 * do jogo (no DrawSplash, thread do jogo) percorre o dicionario de textos no
 * SetLanguage enquanto o registro dos itens do mod acrescenta chaves nele.
 * O "Collection was modified" mata a carga do jogo, o splash nunca acaba e a
 * tela fica preta (24/10, com o Thorium; reproduzido sob carga de CPU).
 */
void waitGameContentLoaded() {
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    FieldInfo* done = main ? il2cpp::findField(main, "_isAsyncLoadComplete") : nullptr;
    if (!done) {
        BL_WARN("sonda: Main._isAsyncLoadComplete nao encontrado; segue sem esperar a carga do jogo");
        return;
    }
    auto& a = il2cpp::api();
    for (int waited = 0;; waited += kLoadPollMs) {
        uint8_t loaded = 0;
        a.field_static_get_value(done, &loaded);
        if (loaded) {
            if (waited) BL_DEBUG("sonda: carga do jogo concluida (+%d ms de espera)", waited);
            return;
        }
        if (waited >= kLoadWaitMaxMs) {
            BL_ERROR("sonda: a carga do jogo nao terminou em %d s; mods carregam assim mesmo", kLoadWaitMaxMs / 1000);
            return;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(kLoadPollMs));
    }
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
            // Esta thread termina no fim do bloco: sai do registro do il2cpp em
            // qualquer saida. Registrada e morta, ela deixava o coletor (e com
            // ele a thread do jogo) esperando a resposta dela para sempre: a
            // tela preta intermitente da abertura com mod, sob carga de CPU.
            struct Attached {
                Il2CppThread* thread;
                ~Attached() {
                    auto& api = il2cpp::api();
                    if (thread && api.thread_detach) api.thread_detach(thread);
                }
            } attached{a.thread_attach(a.domain_get())};
            if (!resolveGameRefs()) {
                BL_ERROR("sonda: resolveGameRefs falhou");
                return;
            }
            BL_DEBUG("sonda: RESOLUCAO OK");
            waitGameContentLoaded();

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
            registerUnloadedWall();
            prepareModNpcs();

            // Menu de cheats (acoes nativas) + botao flutuante na Activity.
            installCheats();
            installDevTools();
            installFramePacing();
            graphics::installRenderHook();
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
