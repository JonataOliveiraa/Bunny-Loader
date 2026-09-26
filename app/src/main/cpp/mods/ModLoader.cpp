#include "mods/ModLoader.h"
#include "core/Log.h"
#include "script/bridge/ScriptEngine.h"
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
    static std::string current;
    return current;
}

std::string& currentIdSlot() {
    static std::string current;
    return current;
}

// id do mod -> pasta do entry. O id e tambem o nome do MODULO no QuickJS, e e
// assim que bl.loadTexture descobre de qual mod veio a chamada mesmo depois da
// carga, la dentro de um hook.
std::map<std::string, std::string>& dirsById() {
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
        const std::string name = spec.entry.empty() ? std::string("main.js") : spec.entry;
        for (const std::string& candidate : {mod.dir + "/content/" + name, mod.dir + "/" + name}) {
            if (fileExists(candidate)) { path = candidate; break; }
        }
        if (path.empty()) {
            BL_ERROR("mod %s: nao achei o entry '%s' (nem em content/ nem na raiz)",
                     id.c_str(), name.c_str());
            registry().back().enabled = false;
            continue;
        }
        mod.entry = path.substr(mod.dir.size() + 1);
        registry().back().entry = mod.entry;
        // A pasta do ARQUIVO, nao a do pacote: o main.js mora em content/, e
        // "ao lado do main.js" e onde o autor poe as imagens dele.
        size_t slash = path.rfind('/');
        currentDirSlot() = slash == std::string::npos ? mod.dir : path.substr(0, slash);
        dirsById()[id] = currentDirSlot();
        currentIdSlot() = id;
        bool ok = script::engine().evalFile(path, id);
        currentDirSlot().clear();
        currentIdSlot().clear();
        if (!ok) {
            BL_ERROR("mod %s falhou ao carregar (%s)", id.c_str(), path.c_str());
            registry().back().enabled = false;
        }
    }
}

const std::string& currentDir() { return currentDirSlot(); }
const std::string& currentId() { return currentIdSlot(); }

std::string rootOf(const std::string& id) {
    for (const LoadedMod& m : registry()) if (m.id == id) return m.dir;
    return {};
}

std::string displayName(const std::string& id) {
    // O "name" do manifesto, lido a mao: e um campo so, e puxar um parser de
    // JSON para o nucleo por causa dele nao se paga.
    const std::string root = rootOf(id);
    FILE* f = root.empty() ? nullptr : std::fopen((root + "/manifest.json").c_str(), "rb");
    if (!f) return id;
    std::string txt;
    char buf[1024];
    size_t n;
    while ((n = std::fread(buf, 1, sizeof(buf), f)) > 0) txt.append(buf, n);
    std::fclose(f);
    size_t k = txt.find("\"name\"");
    if (k == std::string::npos) return id;
    k = txt.find(':', k);
    k = k == std::string::npos ? k : txt.find('"', k);
    if (k == std::string::npos) return id;
    std::string name;
    for (size_t i = k + 1; i < txt.size() && txt[i] != '"'; ++i) {
        if (txt[i] == '\\' && i + 1 < txt.size()) ++i;
        name += txt[i];
    }
    return name.empty() ? id : name;
}

const std::string& dirOf(const std::string& id) {
    static const std::string none;
    auto it = dirsById().find(id);
    return it == dirsById().end() ? none : it->second;
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
