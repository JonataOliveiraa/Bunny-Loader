#include "mods/ModLoader.h"
#include "core/Log.h"
#include "script/ScriptEngine.h"
#include "mods/BuiltinMods.h"  // gerado pelo CMake a partir do .js
#include <cstdio>
#include <map>

namespace bl::mods {

namespace {
std::vector<LoadedMod>& registry() {
    static std::vector<LoadedMod> list;
    return list;
}

std::string& currentDirSlot() {
    static std::string atual;
    return atual;
}

// id do mod -> pasta do entry. O id e tambem o nome do MODULO no QuickJS, e e
// assim que bl.loadTexture descobre de qual mod veio a chamada mesmo depois da
// carga, la dentro de um hook.
std::map<std::string, std::string>& dirsPorId() {
    static std::map<std::string, std::string> m;
    return m;
}

bool fileExists(const std::string& path) {
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return false;
    std::fclose(f);
    return true;
}
}

void loadAll(const std::string& modsDir, const std::vector<ModSpec>& enabled) {
    registry().clear();
    BL_INFO("carregando mods de %s (%zu habilitados)", modsDir.c_str(), enabled.size());

    // TODO(Fase 5): ordenação topológica por dependências (ciclo/faltante =>
    // desativa e loga).
    for (const auto& spec : enabled) {
        const std::string& id = spec.id;
        LoadedMod mod;
        mod.id = id;
        mod.dir = modsDir + "/" + id;
        registry().push_back(mod);

        if (!script::engine().ready()) continue;

        // O `entry` do manifesto manda, e e relativo a content/. Ele era
        // IGNORADO: o carregador abria content/main.js fixo, entao um mod que
        // declarasse outro arquivo rodava o main.js e ninguem avisava.
        //
        // Sem entry (config antiga, ou pacote sem o campo) caimos no padrao. E
        // a raiz continua valendo depois de content/, para o formato antigo,
        // que deixava o main.js sem pasta.
        std::string path;
        const std::string nome = spec.entry.empty() ? std::string("main.js") : spec.entry;
        for (const std::string& tentativa : {mod.dir + "/content/" + nome, mod.dir + "/" + nome}) {
            if (fileExists(tentativa)) { path = tentativa; break; }
        }
        if (path.empty()) {
            BL_ERROR("mod %s: nao achei o entry '%s' (nem em content/ nem na raiz)",
                     id.c_str(), nome.c_str());
            registry().back().enabled = false;
            continue;
        }
        mod.entry = path.substr(mod.dir.size() + 1);
        registry().back().entry = mod.entry;
        // A pasta do ARQUIVO, nao a do pacote: o main.js mora em content/, e
        // "ao lado do main.js" e onde o autor poe as imagens dele.
        size_t barra = path.rfind('/');
        currentDirSlot() = barra == std::string::npos ? mod.dir : path.substr(0, barra);
        dirsPorId()[id] = currentDirSlot();
        bool ok = script::engine().evalFile(path, id);
        currentDirSlot().clear();
        if (!ok) {
            BL_ERROR("mod %s falhou ao carregar (%s)", id.c_str(), path.c_str());
            registry().back().enabled = false;
        }
    }
}

const std::string& currentDir() { return currentDirSlot(); }

const std::string& dirOf(const std::string& id) {
    static const std::string vazio;
    auto it = dirsPorId().find(id);
    return it == dirsPorId().end() ? vazio : it->second;
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
