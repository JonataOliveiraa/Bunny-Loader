#include "boot/Probe.h"
#include "content/common/GameTexts.h"
#include "content/common/ModNet.h"
#include "boot/FramePacing.h"
#include "graphics/RenderHook.h"
#include "boot/QuickStart.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "content/common/GameRefs.h"

#include "script/dotnet/DotnetHost.h"
#include "mods/ModLoader.h"
#include "core/Config.h"
#include "menu/Cheats.h"
#include "content/items/ModItemSave.h"
#include "content/items/VisualItemClone.h"
#include "content/tiles/ModTileSave.h"
#include "content/npcs/ModNpcSave.h"
#include "content/items/ModItems.h"
#include "content/npcs/ModNpcs.h"
#include "content/walls/ModWalls.h"
#include "menu/CheatButton.h"
#include "menu/DevTools.h"
#include "script/api/Console.h"

#include <chrono>
#include <dlfcn.h>
#include <thread>

namespace bl::runtime {

namespace {

using DomainGetFn = Il2CppDomain* (*)();

constexpr int kSettleMs = 10000;
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
    struct Finished {
        ~Finished() { markProbeFinished(); }
    } finished;
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
            installSplashHook();
            waitGameContentLoaded();

            script::installConsoleLog();

            runtime::registerGameTexts();
            mods::loadAll(config().modsDir, config().enabledMods);
            dotnet::host().finishLoading();
            runtime::installNetGuard();
            BL_INFO("sonda: %zu mod(s) carregado(s)", mods::loadedCount());
            registerUnloadedPool();
            registerUnloadedWall();
            prepareModNpcs();

            installCheats();
            installDevTools();
            installFramePacing();
            graphics::installRenderHook();
            installModItemSave();
            installVisualItemClone();
            installModTileSave();
            installModNpcSave();
            ui::installCheatButton();
            markCoreReady();
            return;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(kRetryGapMs));
    }
    BL_ERROR("sonda: il2cpp nao ficou pronto a tempo");
}

}

void startResolutionProbe() {
    std::thread(probeThread).detach();
}

}
