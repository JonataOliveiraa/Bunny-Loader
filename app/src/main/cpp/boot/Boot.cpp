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

    // Os mods NAO sobem aqui — quem faz isso e a sonda (Probe.cpp).
    //
    // A sonda tambem espera o jogo assentar antes de tocar no il2cpp, o que
    // aqui, dentro do proprio il2cpp_init, nao daria para fazer.

    // Inicio rapido: o hook do splash entra agora, antes do primeiro quadro;
    // e ele que acorda a sonda quando o jogo termina de carregar. Sem este
    // caminho (watcher sem hook, como ja foi no MuMu), a sonda instala depois
    // dos 10 s dela, ainda a tempo de encurtar o splash.
    installFastIntro();
    prepareTexturePacks(config().modsDir, config().enabledMods);

    // TODO(Fase 3): installProjectileHooks() e demais hooks de runtime.
    BL_DEBUG("runtime pronto; mods ficam a cargo da sonda");
}

} // namespace bl::runtime
