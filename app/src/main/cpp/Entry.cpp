#include "core/Config.h"
#include "core/Log.h"
#include "loader/LibWatcher.h"

// Ponto de entrada do caminho B (lançar + injetar).
//
// A libbunny.so e pre-carregada no processo do Terraria via a propriedade
// wrap.<pacote> (LD_PRELOAD), ANTES de qualquer coisa da Unity. Como nao ha
// codigo Java nosso nesse processo, o boot comeca aqui, no load da lib.
//
// A ordem que isso garante e justamente a que o projeto precisa:
//   1. exec do processo do jogo -> LD_PRELOAD carrega a libbunny.so -> este ctor
//   2. registramos o hook PENDENTE de il2cpp_init (ShadowHook aceita por nome
//      mesmo com a libil2cpp.so ainda nao carregada)
//   3. o Application do jogo roda -> PairIP inicializa -> strings decifradas
//   4. a Unity carrega a libil2cpp.so -> il2cpp_init -> nosso hook dispara
//      runtime::boot()
//
// Nao ha JNI aqui. A config vem do arquivo gravado pelo launcher.

namespace {

__attribute__((constructor))
void bl_on_load() {
    // A config e opcional. No modo repackage a libbunny so e carregada dentro
    // do processo do jogo (esta no APK dele), entao seguimos mesmo sem config —
    // logando no logcat. No modo preload (wrap), a config identifica o processo.
    bool hasConfig = bl::loadConfigFromFile(bl::kDefaultConfigPath);
    auto& c = bl::config();

    bl::log::open(c.logPath.empty() ? nullptr : c.logPath.c_str());
    BL_INFO("libbunny carregada no processo do jogo (config=%s, versao alvo %lld)",
            hasConfig ? "sim" : "ausente", (long long)c.gameVersion);
    if (hasConfig) {
        BL_INFO("  modsDir=%s  mods habilitados=%zu",
                c.modsDir.c_str(), c.enabledMods.size());
    }

    if (!bl::loader::installWatcher()) {
        BL_ERROR("falha ao registrar o watcher de il2cpp_init");
        return;
    }
    BL_INFO("watcher instalado; aguardando il2cpp_init");
}

} // namespace
