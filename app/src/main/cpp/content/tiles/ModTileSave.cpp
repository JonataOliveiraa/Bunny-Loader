#include "content/tiles/ModTileSave.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "content/common/GameRefs.h"
#include "content/tiles/ModTiles.h"
#include "content/tiles/TileAccess.h"

#include <cstdio>
#include <cstdlib>
#include <map>
#include <mutex>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace bl::runtime {

namespace {

constexpr const char* kSuffix = ".tiles.bl";
constexpr const char* kHeader = "bunny-tiles 1";

/** Um tile de mod: onde, de qual mod, e o que o jogo guarda dele. */
struct SavedTile {
    int x = 0, y = 0;
    std::string key;   // "<uid>/<nome>"
    int frameX = 0, frameY = 0, sHeader = 0, bHeader = 0, bHeader2 = 0, bHeader3 = 0;
};

std::mutex g_mx;
// Por mundo: os tiles de mod que nao estao carregados (voltam ao arquivo).
std::map<std::string, std::vector<SavedTile>> g_kept;

struct Refs {
    bool ok = false;
    FieldInfo* activeWorld = nullptr;   // Main.ActiveWorldFileData
    int32_t path = -1, cloud = -1;      // FileData._path / _isCloudSave
    // Os setters do struct Tile: `this` e o endereco do int _tileOffset.
    void (*setType)(int32_t*, uint16_t, const MethodInfo*) = nullptr;
    void (*setSHeader)(int32_t*, int16_t, const MethodInfo*) = nullptr;
    void (*setFrameX)(int32_t*, int16_t, const MethodInfo*) = nullptr;
    void (*setFrameY)(int32_t*, int16_t, const MethodInfo*) = nullptr;
    void (*setB1)(int32_t*, uint8_t, const MethodInfo*) = nullptr;
    void (*setB2)(int32_t*, uint8_t, const MethodInfo*) = nullptr;
    void (*setB3)(int32_t*, uint8_t, const MethodInfo*) = nullptr;
    const MethodInfo *mType = nullptr, *mSHeader = nullptr, *mFrameX = nullptr, *mFrameY = nullptr,
                     *mB1 = nullptr, *mB2 = nullptr, *mB3 = nullptr;
};
Refs g_refs;

template <typename Fn>
bool setter(Il2CppClass* tile, const char* name, const MethodInfo** m, Fn* fn) {
    *m = il2cpp::api().class_get_method_from_name(tile, name, 1);
    if (!*m) return false;
    *fn = *reinterpret_cast<Fn const*>(*m);   // methodPointer: o primeiro campo do MethodInfo
    return *fn != nullptr;
}

bool resolve() {
    using namespace il2cpp;
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* fileData = findClass({"Terraria.IO", "FileData", {}});
    Il2CppClass* tile = findClass({"Terraria", "Tile", {}});
    if (!main || !fileData || !tile) return false;
    Refs& r = g_refs;
    r.activeWorld = findField(main, "ActiveWorldFileData");
    r.path = fieldOffset(fileData, "_path");
    r.cloud = fieldOffset(fileData, "_isCloudSave");
    r.ok = r.activeWorld && r.path >= 0 && r.cloud >= 0 &&
           setter(tile, "set_type", &r.mType, &r.setType) &&
           setter(tile, "set_sTileHeader", &r.mSHeader, &r.setSHeader) &&
           setter(tile, "set_frameX", &r.mFrameX, &r.setFrameX) &&
           setter(tile, "set_frameY", &r.mFrameY, &r.setFrameY) &&
           setter(tile, "set_bTileHeader", &r.mB1, &r.setB1) &&
           setter(tile, "set_bTileHeader2", &r.mB2, &r.setB2) &&
           setter(tile, "set_bTileHeader3", &r.mB3, &r.setB3);
    return r.ok;
}

std::string toUtf8(Il2CppString* s) {
    std::string out;
    if (!s) return out;
    for (int32_t i = 0; i < s->length; ++i) {
        const char32_t c = s->chars[i];
        if (c < 0x80) out += static_cast<char>(c);
        else if (c < 0x800) { out += static_cast<char>(0xC0 | (c >> 6)); out += static_cast<char>(0x80 | (c & 0x3F)); }
        else {
            out += static_cast<char>(0xE0 | (c >> 12));
            out += static_cast<char>(0x80 | ((c >> 6) & 0x3F));
            out += static_cast<char>(0x80 | (c & 0x3F));
        }
    }
    return out;
}

/** O .wld do mundo aberto; "" para mundo na nuvem. */
std::string activeWorldPath() {
    Il2CppObject* data = nullptr;
    il2cpp::api().field_static_get_value(g_refs.activeWorld, &data);
    if (!data || field<uint8_t>(data, g_refs.cloud)) return {};
    return toUtf8(field<Il2CppString*>(data, g_refs.path));
}

std::vector<SavedTile> readFile(const std::string& path) {
    std::vector<SavedTile> out;
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return out;
    char line[1024];
    bool header = false;
    while (std::fgets(line, sizeof(line), f)) {
        std::string s(line);
        while (!s.empty() && (s.back() == '\n' || s.back() == '\r')) s.pop_back();
        if (!header) {
            header = true;
            if (s != kHeader) {
                BL_ERROR("tiles de mod: %s com cabecalho desconhecido; ignorado", path.c_str());
                break;
            }
            continue;
        }
        // x \t y \t frameX \t frameY \t sHeader \t b1 \t b2 \t b3 \t chave
        std::vector<std::string> cols;
        size_t start = 0;
        for (size_t tab; (tab = s.find('\t', start)) != std::string::npos; start = tab + 1) {
            cols.push_back(s.substr(start, tab - start));
        }
        cols.push_back(s.substr(start));
        if (cols.size() != 9 || cols[8].empty()) continue;
        SavedTile t;
        t.x = std::atoi(cols[0].c_str());
        t.y = std::atoi(cols[1].c_str());
        t.frameX = std::atoi(cols[2].c_str());
        t.frameY = std::atoi(cols[3].c_str());
        t.sHeader = std::atoi(cols[4].c_str());
        t.bHeader = std::atoi(cols[5].c_str());
        t.bHeader2 = std::atoi(cols[6].c_str());
        t.bHeader3 = std::atoi(cols[7].c_str());
        t.key = cols[8];
        out.push_back(std::move(t));
    }
    std::fclose(f);
    return out;
}

/** Temporario + rename: queda no meio nao deixa arquivo pela metade. */
void writeFile(const std::string& path, const std::vector<SavedTile>& tiles) {
    if (tiles.empty()) {
        std::remove(path.c_str());
        return;
    }
    const std::string tmp = path + ".tmp";
    FILE* f = std::fopen(tmp.c_str(), "wb");
    if (!f) {
        BL_ERROR("tiles de mod: nao consegui escrever %s", tmp.c_str());
        return;
    }
    std::fprintf(f, "%s\n", kHeader);
    for (const SavedTile& t : tiles) {
        std::fprintf(f, "%d\t%d\t%d\t%d\t%d\t%d\t%d\t%d\t%s\n", t.x, t.y, t.frameX, t.frameY, t.sHeader,
                     t.bHeader, t.bHeader2, t.bHeader3, t.key.c_str());
    }
    const bool ok = std::fflush(f) == 0;
    std::fclose(f);
    if (!ok || std::rename(tmp.c_str(), path.c_str()) != 0) {
        BL_ERROR("tiles de mod: falha ao gravar %s", path.c_str());
        std::remove(tmp.c_str());
    }
}

// ------------------------------ salvar ------------------------------

using SaveTilesFn = int32_t (*)(Il2CppObject*, const MethodInfo*);
SaveTilesFn g_origSave = nullptr;

int32_t hkSaveWorldTiles(Il2CppObject* writer, const MethodInfo* m) {
    const std::string path = activeWorldPath();
    TileArrays t;
    std::vector<SavedTile> tiles;
    // Definicao de tipo de mod -> o que ela era (volta depois do save).
    std::unordered_map<uint32_t, std::pair<uint16_t, int16_t>> stripped;
    if (tileTypeCount() > kVanillaTileCount && tileArrays(&t)) {
        std::unordered_map<int, std::string> keys;
        const int64_t n = static_cast<int64_t>(t.width) * t.height;
        for (int64_t pos = 0; pos < n; ++pos) {
            const uint32_t def = t.lookup[pos];
            const int type = t.type[def];
            if (type < kVanillaTileCount || !(t.sHeader[def] & kTileActiveBit)) continue;
            auto k = keys.find(type);
            if (k == keys.end()) k = keys.emplace(type, modTileKey(type)).first;
            if (k->second.empty()) continue;
            SavedTile s;
            s.x = static_cast<int>(pos % t.width);
            s.y = static_cast<int>(pos / t.width);
            s.key = k->second;
            s.frameX = t.frameX[def];
            s.frameY = t.frameY[def];
            s.sHeader = t.sHeader[def];
            s.bHeader = t.bHeader[def];
            s.bHeader2 = t.bHeader2[def];
            s.bHeader3 = t.bHeader3[def];
            tiles.push_back(std::move(s));
            stripped.emplace(def, std::make_pair(t.type[def], t.sHeader[def]));
        }
        // Ar para o jogo: tipo 0, inativo. Parede, liquido e fios ficam.
        for (const auto& [def, orig] : stripped) {
            t.type[def] = 0;
            t.sHeader[def] = static_cast<int16_t>(orig.second & ~kTileActiveBit);
        }
    }

    const int32_t r = g_origSave(writer, m);

    for (const auto& [def, orig] : stripped) {
        t.type[def] = orig.first;
        t.sHeader[def] = orig.second;
    }
    if (!path.empty()) {
        size_t kept = 0;
        {
            std::lock_guard<std::mutex> l(g_mx);
            auto it = g_kept.find(path);
            if (it != g_kept.end()) {
                kept = it->second.size();
                tiles.insert(tiles.end(), it->second.begin(), it->second.end());
            }
        }
        writeFile(path + kSuffix, tiles);
        if (!tiles.empty()) {
            BL_INFO("tiles de mod: mundo salvo sem tile de mod no .wld; %zu tile(s) em %s (%zu de mod "
                    "nao carregado)", tiles.size(), (path + kSuffix).c_str(), kept);
        }
    }
    return r;
}

// ------------------------------ carregar ------------------------------

using LoadTilesFn = void (*)(Il2CppObject*, Il2CppArray*, const MethodInfo*);
LoadTilesFn g_origLoad = nullptr;

void hkLoadWorldTiles(Il2CppObject* reader, Il2CppArray* importance, const MethodInfo* m) {
    g_origLoad(reader, importance, m);
    const std::string path = activeWorldPath();
    if (path.empty()) return;
    const std::vector<SavedTile> saved = readFile(path + kSuffix);
    std::vector<SavedTile> kept;
    int restored = 0, builtOver = 0;
    TileArrays t;
    const bool world = tileArrays(&t);
    const Refs& r = g_refs;
    for (const SavedTile& s : saved) {
        const int type = modTileTypeByKey(s.key);
        if (type < 0 || !world) {   // o mod nao esta: fica para quando voltar
            kept.push_back(s);
            continue;
        }
        if (s.x < 0 || s.y < 0 || s.x >= t.width || s.y >= t.height) continue;
        int32_t offset = t.width * s.y + s.x;
        // Construiram no lugar sem o mod: vale o que esta la.
        if (t.sHeader[t.lookup[offset]] & kTileActiveBit) {
            ++builtOver;
            continue;
        }
        r.setSHeader(&offset, static_cast<int16_t>(s.sHeader), r.mSHeader);
        r.setType(&offset, static_cast<uint16_t>(type), r.mType);
        r.setFrameX(&offset, static_cast<int16_t>(s.frameX), r.mFrameX);
        r.setFrameY(&offset, static_cast<int16_t>(s.frameY), r.mFrameY);
        r.setB1(&offset, static_cast<uint8_t>(s.bHeader), r.mB1);
        r.setB2(&offset, static_cast<uint8_t>(s.bHeader2), r.mB2);
        r.setB3(&offset, static_cast<uint8_t>(s.bHeader3), r.mB3);
        ++restored;
    }
    {
        std::lock_guard<std::mutex> l(g_mx);
        if (kept.empty()) g_kept.erase(path);
        else g_kept[path] = kept;
    }
    if (!saved.empty()) {
        BL_INFO("tiles de mod: %d tile(s) reposto(s), %zu de mod nao carregado (ficam no arquivo), "
                "%d com algo construido no lugar", restored, kept.size(), builtOver);
    }
}

} // namespace

void installModTileSave() {
    Il2CppClass* worldFile = il2cpp::findClass({"Terraria.IO", "WorldFile", {}});
    auto& a = il2cpp::api();
    const MethodInfo* save = worldFile ? a.class_get_method_from_name(worldFile, "SaveWorldTilesFast", 1) : nullptr;
    const MethodInfo* load = worldFile ? a.class_get_method_from_name(worldFile, "LoadWorldTiles", 2) : nullptr;
    if (!resolve() || !save || !load || !hook::install(save, hkSaveWorldTiles, &g_origSave) ||
        !hook::install(load, hkLoadWorldTiles, &g_origLoad)) {
        BL_ERROR("tiles de mod: sem o save do mundo; tile de mod iria para o .wld pelo numero");
        return;
    }
    BL_DEBUG("tiles de mod: save do mundo pronto (%s)", kSuffix);
}

} // namespace bl::runtime
