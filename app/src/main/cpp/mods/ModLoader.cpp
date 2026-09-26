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

/**
 * O texto de uma chave do manifesto ("id", "name"...), lido a mao: sao tres
 * campos, e puxar um parser de JSON para o nucleo por causa deles nao se paga.
 * A chave so conta seguida de ':' — o mesmo texto entre aspas dentro de um
 * valor nao engana.
 */
std::string manifestString(const std::string& txt, const char* key) {
    const std::string quoted = std::string("\"") + key + "\"";
    auto skipSpace = [&](size_t i) {
        while (i < txt.size() && (txt[i] == ' ' || txt[i] == '\t' || txt[i] == '\r' || txt[i] == '\n')) ++i;
        return i;
    };
    for (size_t k = txt.find(quoted); k != std::string::npos; k = txt.find(quoted, k + 1)) {
        size_t i = skipSpace(k + quoted.size());
        if (i >= txt.size() || txt[i] != ':') continue;
        i = skipSpace(i + 1);
        if (i >= txt.size() || txt[i] != '"') return {};
        std::string out;
        for (++i; i < txt.size() && txt[i] != '"'; ++i) {
            if (txt[i] == '\\' && i + 1 < txt.size()) ++i;
            out += txt[i];
        }
        return out;
    }
    return {};
}

void readManifest(LoadedMod* mod) {
    FILE* f = std::fopen((mod->dir + "/manifest.json").c_str(), "rb");
    std::string txt;
    if (f) {
        char buf[1024];
        size_t n;
        while ((n = std::fread(buf, 1, sizeof(buf), f)) > 0) txt.append(buf, n);
        std::fclose(f);
    }
    mod->internalName = manifestString(txt, "id");
    mod->displayName = manifestString(txt, "name");
    mod->version = manifestString(txt, "version");
    if (mod->displayName.empty()) mod->displayName = mod->id;
}
}

void loadAll(const std::string& modsDir, const std::vector<ModSpec>& enabled) {
    registry().clear();
    BL_INFO("carregando mods de %s (%zu habilitados)", modsDir.c_str(), enabled.size());

    // 1a passada: todos no registro, com manifesto e entry, antes de qualquer
    // main.js rodar. A ordem de carga e a do uid (aleatoria na pratica), entao
    // sem isto o ModLoader.TryGetMod de um mod dependeria do sorteio do uid do
    // outro.
    //
    // TODO(Fase 5): ordenação topológica por dependências (ciclo/faltante =>
    // desativa e loga).
    for (const auto& spec : enabled) {
        const std::string& id = spec.id;
        LoadedMod mod;
        mod.id = id;
        mod.dir = modsDir + "/" + id;
        readManifest(&mod);
        registry().push_back(mod);

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
        registry().back().entry = path.substr(mod.dir.size() + 1);
        // A pasta do ARQUIVO, nao a do pacote: o main.js mora em content/, e
        // "ao lado do main.js" e onde o autor poe as imagens dele.
        size_t slash = path.rfind('/');
        dirsById()[id] = slash == std::string::npos ? mod.dir : path.substr(0, slash);
    }

    if (!script::engine().ready()) return;

    // 2a passada: os main.js, na mesma ordem. Por indice: o registro nao muda
    // durante a carga, mas uma referencia guardada atravessando o evalFile
    // seria a mesma armadilha do g_frames.back() do JsHook.
    for (size_t i = 0; i < registry().size(); ++i) {
        if (!registry()[i].enabled) continue;
        const std::string id = registry()[i].id;
        const std::string path = registry()[i].dir + "/" + registry()[i].entry;
        currentDirSlot() = dirsById()[id];
        currentIdSlot() = id;
        bool ok = script::engine().evalFile(path, id);
        currentDirSlot().clear();
        currentIdSlot().clear();
        if (!ok) {
            BL_ERROR("mod %s falhou ao carregar (%s)", id.c_str(), path.c_str());
            registry()[i].enabled = false;
        } else {
            registry()[i].loaded = true;
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
    const LoadedMod* m = find(id);
    return m ? m->displayName : id;
}

const LoadedMod* find(const std::string& id) {
    for (const LoadedMod& m : registry()) if (m.id == id) return &m;
    return nullptr;
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
    // Como no loadAll: todos registrados antes de o primeiro rodar.
    const size_t first = registry().size();
    for (int i = 0; i < kBuiltinModCount; ++i) {
        LoadedMod mod;
        mod.id = kBuiltinMods[i].id;
        mod.internalName = mod.id;
        mod.displayName = mod.id;
        mod.dir = "(builtin)";
        mod.entry = "(builtin)";
        registry().push_back(mod);
    }
    for (int i = 0; i < kBuiltinModCount; ++i) {
        const auto& b = kBuiltinMods[i];
        const bool ok = script::engine().eval(b.source, b.id);
        if (!ok) BL_ERROR("mod embutido %s falhou ao carregar", b.id);
        registry()[first + i].enabled = ok;
        registry()[first + i].loaded = ok;
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
