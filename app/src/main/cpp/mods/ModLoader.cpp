#include "mods/ModLoader.h"
#include "core/Log.h"
#include "script/ScriptEngine.h"
#include "mods/BuiltinMods.h"  // gerado pelo CMake a partir do .js

namespace bl::mods {

namespace {
std::vector<LoadedMod>& registry() {
    static std::vector<LoadedMod> list;
    return list;
}
}

void loadAll(const std::string& modsDir, const std::vector<std::string>& enabled) {
    registry().clear();
    BL_INFO("carregando mods de %s (%zu habilitados)", modsDir.c_str(), enabled.size());

    // TODO(Fase 5):
    //  1. ler mod.json de cada mod habilitado
    //  2. ordenação topológica por dependências (ciclo/faltante => desativa e loga)
    //  3. evalFile do entry de cada um
    for (const auto& id : enabled) {
        LoadedMod mod;
        mod.id = id;
        mod.dir = modsDir + "/" + id;
        mod.entry = "main.js";
        registry().push_back(mod);

        if (!script::engine().ready()) continue;
        const std::string path = mod.dir + "/" + mod.entry;
        if (!script::engine().evalFile(path, id)) {
            BL_ERROR("mod %s falhou ao carregar", id.c_str());
            registry().back().enabled = false;
        }
    }
}

void loadBuiltins() {
    if (!script::engine().ready()) {
        BL_ERROR("mods embutidos: QuickJS nao esta pronto");
        return;
    }
    BL_INFO("carregando %d mod(s) embutido(s) na libbunny", kBuiltinModCount);
    for (int i = 0; i < kBuiltinModCount; ++i) {
        const auto& b = kBuiltinMods[i];
        LoadedMod mod;
        mod.id = b.id;
        mod.dir = "(builtin)";
        mod.entry = "(builtin)";
        registry().push_back(mod);
        if (!script::engine().eval(b.source, b.id)) {
            BL_ERROR("mod embutido %s falhou ao carregar", b.id);
            registry().back().enabled = false;
        }
    }
}

size_t loadedCount() { return registry().size(); }

LoadedMod* get(uint16_t index) {
    if (index >= registry().size()) return nullptr;
    return &registry()[index];
}

void disableAtRuntime(uint16_t index) {
    if (LoadedMod* mod = get(index)) {
        mod->enabled = false;
        BL_ERROR("mod %s desativado em runtime (erros demais)", mod->id.c_str());
    }
}

} // namespace bl::mods
