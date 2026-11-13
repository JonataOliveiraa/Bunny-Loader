#include "core/Config.h"
#include "core/Log.h"
#include "boot/LibWatcher.h"
#include "boot/Probe.h"

namespace {

__attribute__((constructor))
void bl_on_load() {
    bool hasConfig = bl::loadConfigFromFile(bl::kDefaultConfigPath);
    auto& c = bl::config();

    bl::log::open(c.logPath.empty() ? nullptr : c.logPath.c_str());
    BL_INFO("libbunny carregada no processo do jogo (config=%s, versao alvo %lld)",
            hasConfig ? "sim" : "ausente", (long long)c.gameVersion);
    if (hasConfig) {
        BL_INFO("  modsDir=%s  mods habilitados=%zu",
                c.modsDir.c_str(), c.enabledMods.size());
    }

    if (bl::loader::installWatcher()) {
        BL_INFO("watcher instalado; aguardando il2cpp_init");
    } else {
        BL_WARN("watcher nao instalado (esperado no emulador); seguindo com a sonda");
    }

    bl::runtime::startResolutionProbe();
}

}
