#include "mods/ModLoader.h"
#include "core/Log.h"
#include "script/bridge/ScriptEngine.h"
#include "mods/BuiltinMods.h"  // gerado pelo CMake a partir do .js
#include <algorithm>
#include <cstdio>
#include <map>

#include <dirent.h>
#include <sys/stat.h>

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
 * Os .js de uma pasta do mod (Content/, Common/), recursivo e em ordem
 * alfabetica, relativos a `base`. A ordem e a do registro dos tipos, e ela
 * precisa ser a mesma em todo aparelho (cliente e servidor).
 */
void collectScripts(const std::string& base, const std::string& rel, std::vector<std::string>* out) {
    DIR* d = opendir((base + "/" + rel).c_str());
    if (!d) return;
    std::vector<std::string> names;
    while (dirent* e = readdir(d)) {
        const std::string name = e->d_name;
        if (name != "." && name != "..") names.push_back(name);
    }
    closedir(d);
    std::sort(names.begin(), names.end());
    for (const std::string& name : names) {
        const std::string child = rel + "/" + name;
        struct stat st{};
        if (stat((base + "/" + child).c_str(), &st) != 0) continue;
        if (S_ISDIR(st.st_mode)) collectScripts(base, child, out);
        else if (name.size() > 3 && name.compare(name.size() - 3, 3, ".js") == 0) out->push_back(child);
    }
}

/**
 * O texto de uma chave do manifesto ("id", "name"...), lido a mao: sao tres
 * campos, e puxar um parser de JSON para o nucleo por causa deles nao se paga.
 *
 * So valem as chaves do objeto de FORA. `authors` tem um "name" por autor, e
 * um manifesto que lista os autores antes do nome do mod fazia o jogo chamar o
 * mod pelo nome do primeiro autor. Texto entre aspas (chave ou valor) nunca
 * conta como chave ou chave/colchete.
 */
std::string manifestString(const std::string& txt, const char* key) {
    auto skipSpace = [&](size_t i) {
        while (i < txt.size() && (txt[i] == ' ' || txt[i] == '\t' || txt[i] == '\r' || txt[i] == '\n')) ++i;
        return i;
    };
    // Le a string que comeca nas aspas de `i`; devolve o indice depois dela.
    auto readString = [&](size_t i, std::string* out) {
        for (++i; i < txt.size() && txt[i] != '"'; ++i) {
            if (txt[i] == '\\' && i + 1 < txt.size()) ++i;
            if (out) *out += txt[i];
        }
        return i + 1;
    };
    int depth = 0;
    for (size_t i = 0; i < txt.size();) {
        const char c = txt[i];
        if (c == '{' || c == '[') { ++depth; ++i; continue; }
        if (c == '}' || c == ']') { --depth; ++i; continue; }
        if (c != '"') { ++i; continue; }
        std::string name;
        size_t after = readString(i, &name);
        size_t colon = skipSpace(after);
        if (depth == 1 && colon < txt.size() && txt[colon] == ':' && name == key) {
            size_t v = skipSpace(colon + 1);
            if (v >= txt.size() || txt[v] != '"') return {};
            std::string out;
            readString(v, &out);
            return out;
        }
        i = after;
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
    // main.js rodar. A ordem de carga vem da lista escolhida no launcher;
    // sem isto o ModLoader.TryGetMod de um mod dependeria da posicao do outro.
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

        // O `entry` do manifesto manda, relativo a content/ (sem ele, main.js).
        const std::string name = spec.entry.empty() ? std::string("main.js") : spec.entry;
        const std::string path = mod.dir + "/content/" + name;
        if (!fileExists(path)) {
            BL_ERROR("mod %s: nao achei o arquivo de entrada content/%s", id.c_str(), name.c_str());
            registry().back().enabled = false;
            continue;
        }
        registry().back().entry = path.substr(mod.dir.size() + 1);
        // A pasta do ARQUIVO de entrada: Assets/, Content/, Common/ e
        // Localization/ ficam ao lado dele.
        dirsById()[id] = path.substr(0, path.rfind('/'));
    }

    if (!script::engine().ready()) return;

    // 2a passada: cada mod, na mesma ordem. Por indice: o registro nao muda
    // durante a carga, mas uma referencia guardada atravessando o loadMod
    // seria a mesma armadilha do g_frames.back() do JsHook.
    for (size_t i = 0; i < registry().size(); ++i) {
        if (!registry()[i].enabled) continue;
        const std::string id = registry()[i].id;
        const std::string path = registry()[i].dir + "/" + registry()[i].entry;
        const std::string dir = dirsById()[id];
        std::vector<std::string> files{path.substr(dir.size() + 1)};
        for (const char* folder : {"Common", "Content"}) collectScripts(dir, folder, &files);
        files.erase(std::remove(files.begin() + 1, files.end(), files[0]), files.end());
        BL_DEBUG("mod %s: %zu arquivo(s) em Common/ e Content/", id.c_str(), files.size() - 1);
        currentDirSlot() = dir;
        currentIdSlot() = id;
        bool ok = script::engine().loadMod(id, files);
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
        currentIdSlot() = b.id;
        const bool ok = script::engine().loadBuiltinMod(b.source, b.id);
        currentIdSlot().clear();
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

} // namespace bl::mods
