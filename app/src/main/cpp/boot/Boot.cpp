#include "boot/Boot.h"
#include "boot/QuickStart.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "content/common/GameRefs.h"
#include "content/textures/TexturePacks.h"
#include "core/Config.h"

#include <unistd.h>

#include <atomic>

namespace bl::runtime {

namespace {
std::atomic<int> g_gameTid{0};
std::atomic<uint32_t> g_updateTick{1};
}

int gameThreadId() { return g_gameTid.load(std::memory_order_relaxed); }

uint32_t gameUpdateTick() { return g_updateTick.load(std::memory_order_relaxed); }

void noteGameThread() {
    g_updateTick.fetch_add(1, std::memory_order_relaxed);
    int eu = static_cast<int>(gettid());
    if (g_gameTid.load(std::memory_order_relaxed) != eu) {
        g_gameTid.store(eu, std::memory_order_relaxed);
    }
}

void boot() {
    auto& a = il2cpp::api();

    if (!a.load()) {
        BL_ERROR("Falha ao carregar a API do IL2CPP. Mods desativados.");
        return;
    }
    a.thread_attach(a.domain_get());

    if (!resolveGameRefs()) {
        BL_ERROR("Versao do jogo incompativel. Mods desativados.");
        return;
    }

    installSplashHook();
    prepareTexturePacks(config().modsDir, config().enabledMods);

    BL_DEBUG("runtime pronto; mods ficam a cargo da sonda");
}

}
