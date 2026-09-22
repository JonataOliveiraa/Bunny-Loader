#include "runtime/Boot.h"
#include "core/Config.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "mods/ModLoader.h"
#include "runtime/GameRefs.h"
#include "script/ScriptEngine.h"

namespace bl::runtime {

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

    if (!script::engine().init()) {
        BL_ERROR("Falha ao iniciar o motor de script.");
        return;
    }

    mods::loadAll(config().modsDir, config().enabledMods);

    // TODO(Fase 3): installProjectileHooks() e demais hooks de runtime.
    BL_INFO("Bunny Loader pronto");
}

} // namespace bl::runtime
