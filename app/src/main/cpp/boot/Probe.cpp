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
        BL_WARN("probe: Main._isAsyncLoadComplete not found; not waiting for the game to load");
        return;
    }
    auto& a = il2cpp::api();
    for (int waited = 0;; waited += kLoadPollMs) {
        uint8_t loaded = 0;
        a.field_static_get_value(done, &loaded);
        if (loaded) {
            if (waited) BL_DEBUG("probe: game loaded (+%d ms of waiting)", waited);
            return;
        }
        if (waited >= kLoadWaitMaxMs) {
            BL_ERROR("probe: the game did not finish loading in %d s; loading the mods anyway", kLoadWaitMaxMs / 1000);
            return;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(kLoadPollMs));
    }
}

void probeThread() {
    struct Finished {
        ~Finished() { markProbeFinished(); }
    } finished;
    BL_DEBUG("probe: waiting for the game to settle (at most %d ms)...", kSettleMs);
    waitForGameLoaded(std::chrono::milliseconds(kSettleMs));

    for (int i = 0; i < kRetries; ++i) {
        if (il2cppReady()) {
            BL_DEBUG("probe: il2cpp ready; loading the API");
            auto& a = il2cpp::api();
            if (!a.load()) {
                BL_ERROR("probe: Api::load() failed");
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
                BL_ERROR("probe: resolveGameRefs failed");
                return;
            }
            BL_DEBUG("probe: references resolved");
            installSplashHook();
            waitGameContentLoaded();

            script::installConsoleLog();

            runtime::registerGameTexts();
            mods::loadAll(config().modsDir, config().enabledMods);
            dotnet::host().finishLoading();
            runtime::installNetGuard();
            BL_INFO("probe: %zu mod(s) loaded", mods::loadedCount());
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
    BL_ERROR("probe: il2cpp was not ready in time");
}

}

void startResolutionProbe() {
    std::thread(probeThread).detach();
}

}
