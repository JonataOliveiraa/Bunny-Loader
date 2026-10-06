#include "content/tiles/ModTileSave.h"
#include "core/Log.h"
#include "hook/CodePatch.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "content/common/GameRefs.h"
#include "content/tiles/ModTiles.h"
#include "content/tiles/TileAccess.h"
#include "content/walls/ModWalls.h"

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <algorithm>
#include <map>
#include <mutex>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <type_traits>
#include <vector>

namespace bl::runtime {

namespace {

constexpr const char* kSuffix = ".tiles.bl";
constexpr const char* kHeader = "bunny-tiles 1";
constexpr const char* kWallSuffix = ".walls.bl";
constexpr const char* kWallHeader = "bunny-walls 2";
constexpr const char* kWallHeaderV1 = "bunny-walls 1";

struct SavedWall {
    int x = 0, y = 0;
    int color = 0;
    int flags = 0;   // 1 = invisivel, 2 = fullbright
    std::string key;
};

/** Um tile de mod: onde, de qual mod, e o que o jogo guarda dele. */
struct SavedTile {
    int x = 0, y = 0;
    std::string key;   // "<uid>/<nome>"
    int frameX = 0, frameY = 0, sHeader = 0, bHeader = 0, bHeader2 = 0, bHeader3 = 0;
};

std::mutex g_mx;
// Por mundo: os tiles de mod que nao estao carregados (voltam ao arquivo).
std::map<std::string, std::vector<SavedTile>> g_kept;
std::map<std::string, std::vector<SavedWall>> g_keptWalls;

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
    void (*setWall)(int32_t*, uint16_t, const MethodInfo*) = nullptr;
    void (*wallFrame)(int32_t, int32_t, bool, const MethodInfo*) = nullptr;
    uint8_t (*getWallColor)(int32_t*, const MethodInfo*) = nullptr;
    bool (*getInvisible)(int32_t*, const MethodInfo*) = nullptr;
    bool (*getFullbright)(int32_t*, const MethodInfo*) = nullptr;
    void (*setWallColor)(int32_t*, uint8_t, const MethodInfo*) = nullptr;
    void (*setInvisible)(int32_t*, bool, const MethodInfo*) = nullptr;
    void (*setFullbright)(int32_t*, bool, const MethodInfo*) = nullptr;
    const MethodInfo *mGetWallColor = nullptr, *mGetInvisible = nullptr, *mGetFullbright = nullptr,
                     *mSetWallColor = nullptr, *mSetInvisible = nullptr, *mSetFullbright = nullptr;
    const MethodInfo *mType = nullptr, *mSHeader = nullptr, *mFrameX = nullptr, *mFrameY = nullptr,
                     *mB1 = nullptr, *mB2 = nullptr, *mB3 = nullptr, *mWall = nullptr, *mWallFrame = nullptr;
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
    if (!setter(tile, "set_wall", &r.mWall, &r.setWall)) {
        BL_ERROR("paredes de mod: Tile.set_wall nao achado; paredes de mod nao voltam do arquivo");
    }
    const auto method = [&](const char* name, int args, const MethodInfo** m, auto** fn) {
        *m = il2cpp::api().class_get_method_from_name(tile, name, args);
        if (*m) *fn = *reinterpret_cast<std::remove_reference_t<decltype(*fn)> const*>(*m);
    };
    method("wallColor", 0, &r.mGetWallColor, &r.getWallColor);
    method("invisibleWall", 0, &r.mGetInvisible, &r.getInvisible);
    method("fullbrightWall", 0, &r.mGetFullbright, &r.getFullbright);
    method("wallColor", 1, &r.mSetWallColor, &r.setWallColor);
    method("invisibleWall", 1, &r.mSetInvisible, &r.setInvisible);
    method("fullbrightWall", 1, &r.mSetFullbright, &r.setFullbright);
    if (!r.getWallColor || !r.setWallColor) BL_WARN("paredes de mod: Tile.wallColor nao achado; a tinta de parede de mod nao volta");
    Il2CppClass* framing = findClass({"Terraria", "Framing", {}});
    r.mWallFrame = framing ? il2cpp::api().class_get_method_from_name(framing, "WallFrame", 3) : nullptr;
    if (r.mWallFrame) r.wallFrame = *reinterpret_cast<decltype(r.wallFrame) const*>(r.mWallFrame);
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

std::vector<SavedWall> readWallFile(const std::string& path) {
    std::vector<SavedWall> out;
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return out;
    char line[1024];
    bool header = false, v1 = false;
    while (std::fgets(line, sizeof(line), f)) {
        std::string s(line);
        while (!s.empty() && (s.back() == '\n' || s.back() == '\r')) s.pop_back();
        if (!header) {
            header = true;
            if (s == kWallHeaderV1) {
                v1 = true;
            } else if (s != kWallHeader) {
                BL_ERROR("paredes de mod: %s com cabecalho desconhecido; ignorado", path.c_str());
                break;
            }
            continue;
        }
        std::vector<std::string> cols;
        size_t start = 0;
        for (size_t tab; (tab = s.find('\t', start)) != std::string::npos; start = tab + 1) {
            cols.push_back(s.substr(start, tab - start));
        }
        cols.push_back(s.substr(start));
        const size_t want = v1 ? 3 : 5;
        if (cols.size() != want || cols.back().empty()) continue;
        SavedWall w;
        w.x = std::atoi(cols[0].c_str());
        w.y = std::atoi(cols[1].c_str());
        if (!v1) {
            w.color = std::atoi(cols[2].c_str());
            w.flags = std::atoi(cols[3].c_str());
        }
        w.key = cols.back();
        out.push_back(std::move(w));
    }
    std::fclose(f);
    return out;
}

void writeWallFile(const std::string& path, const std::vector<SavedWall>& walls) {
    if (walls.empty()) {
        std::remove(path.c_str());
        return;
    }
    const std::string tmp = path + ".tmp";
    FILE* f = std::fopen(tmp.c_str(), "wb");
    if (!f) {
        BL_ERROR("paredes de mod: nao consegui escrever %s", tmp.c_str());
        return;
    }
    std::fprintf(f, "%s\n", kWallHeader);
    for (const SavedWall& w : walls) std::fprintf(f, "%d\t%d\t%d\t%d\t%s\n", w.x, w.y, w.color, w.flags, w.key.c_str());
    const bool ok = std::fflush(f) == 0;
    std::fclose(f);
    if (!ok || std::rename(tmp.c_str(), path.c_str()) != 0) {
        BL_ERROR("paredes de mod: falha ao gravar %s", path.c_str());
        std::remove(tmp.c_str());
    }
}

// ------------------------------ salvar ------------------------------
//
// A gravacao le uma copia. O SaveWorldTilesFast pega o bloco de estaticos do
// TileData UMA vez, logo no comeco (`ldr x8, [x8, #0xb8]`, o static_fields
// da classe), e dali em diante usa os ponteiros dele — TileLookup, TileType,
// TileSHeader... — ja em registradores. Essa instrucao vira `mov x8, x27`, e
// quem chama pos em x27 um bloco FALSO: copia do verdadeiro, com TileType e
// TileSHeader apontando para copias em que o tipo de mod e ar. O jogo segue
// com o bloco verdadeiro; so a gravacao ve a copia.
//
// O x27 ainda e o de quem chamou naquele ponto: a funcao so o escreve
// depois (`mov w27, wzr` no laco). A troca e permanente, entao TODA chamada
// passa por bl_call_with_x27 — sem tile de mod, com o bloco verdadeiro.
// Instrucao diferente da esperada (outra versao do jogo): nada e trocado, e
// a gravacao faz o de antes (tira os tiles de mod do mundo e os devolve).

using SaveTilesFn = int32_t (*)(Il2CppObject*, const MethodInfo*);
SaveTilesFn g_origSave = nullptr;

constexpr size_t kLoadStaticsAt = 0x104;         // SaveWorldTilesFast + 0x104
constexpr uint32_t kLoadStatics = 0xF9405D08;    // ldr x8, [x8, #0xb8]
constexpr uint32_t kMovFromX27 = 0xAA1B03E8;     // mov x8, x27
constexpr size_t kClassStaticFields = 0xB8;      // Il2CppClass::static_fields (o #0xb8 acima)
constexpr size_t kStaticsSize = 0xF0;            // ate o TileBHeader3 (0xE8) + 8
constexpr size_t kDefaultDefinitions = 200000;   // TileData.TileBufferSize

struct SaveView {
    bool ok = false;
    Il2CppClass* tileData = nullptr;
    size_t typeAt = 0, sHeaderAt = 0;   // os campos no bloco de estaticos
    size_t wallAt = 0;
    alignas(16) uint8_t statics[kStaticsSize];
    std::vector<uint16_t> type;
    std::vector<int16_t> sHeader;
    std::vector<uint16_t> wall;
};
SaveView g_view;
std::mutex g_viewMx;   // uma gravacao por vez usa o bloco falso

} // namespace
} // namespace bl::runtime

// x0 = writer, x1 = MethodInfo, x2 = a funcao, x3 = o bloco de estaticos (vai
// em x27). Com CFI: excecao do jogo atravessa este quadro ate quem pega.
asm(R"(
    .text
    .p2align 2
    .globl bl_call_with_x27
    .hidden bl_call_with_x27
    .type bl_call_with_x27, %function
bl_call_with_x27:
    .cfi_startproc
    stp x29, x30, [sp, #-32]!
    .cfi_def_cfa_offset 32
    .cfi_offset w29, -32
    .cfi_offset w30, -24
    str x27, [sp, #16]
    .cfi_offset w27, -16
    mov x29, sp
    .cfi_def_cfa w29, 32
    mov x27, x3
    blr x2
    ldr x27, [sp, #16]
    ldp x29, x30, [sp], #32
    .cfi_def_cfa sp, 0
    .cfi_restore w27
    .cfi_restore w29
    .cfi_restore w30
    ret
    .cfi_endproc
    .size bl_call_with_x27, .-bl_call_with_x27
)");
extern "C" int32_t bl_call_with_x27(Il2CppObject* writer, const MethodInfo* m, void* fn, void* statics);

namespace bl::runtime {
namespace {

uint8_t* realStatics() {
    return *reinterpret_cast<uint8_t**>(reinterpret_cast<char*>(g_view.tileData) + kClassStaticFields);
}

/**
 * O bloco que a gravacao le: com tile de mod, a copia com eles como ar; sem,
 * o verdadeiro. Sob g_viewMx.
 */
void* statsForSave(bool withModTiles, bool withModWalls, size_t cells) {
    uint8_t* real = realStatics();
    if ((!withModTiles && !withModWalls) || !real) return real;
    auto* type = *reinterpret_cast<uint16_t**>(real + g_view.typeAt);
    auto* sHeader = *reinterpret_cast<int16_t**>(real + g_view.sHeaderAt);
    auto* wall = g_view.wallAt ? *reinterpret_cast<uint16_t**>(real + g_view.wallAt) : nullptr;
    if (!type || !sHeader || (withModWalls && !wall)) return real;
    // As tabelas por definicao ficam em sequencia no buffer do mundo
    // (TileData.Allocate): TileType e logo seguida da TileSHeader.
    size_t count = sHeader > reinterpret_cast<int16_t*>(type)
        ? static_cast<size_t>(reinterpret_cast<char*>(sHeader) - reinterpret_cast<char*>(type)) / sizeof(uint16_t)
        : 0;
    if (count < 1024 || count > (1u << 24)) count = kDefaultDefinitions;
    std::memcpy(g_view.statics, real, kStaticsSize);
    if (withModTiles) {
        g_view.type.assign(type, type + count);
        g_view.sHeader.assign(sHeader, sHeader + count);
        for (size_t d = 0; d < count; ++d) {
            if (g_view.type[d] < kVanillaTileCount) continue;
            g_view.type[d] = 0;
            g_view.sHeader[d] = static_cast<int16_t>(g_view.sHeader[d] & ~kTileActiveBit);
        }
        uint16_t* typeCopy = g_view.type.data();
        int16_t* sHeaderCopy = g_view.sHeader.data();
        std::memcpy(g_view.statics + g_view.typeAt, &typeCopy, sizeof(typeCopy));
        std::memcpy(g_view.statics + g_view.sHeaderAt, &sHeaderCopy, sizeof(sHeaderCopy));
    }
    if (withModWalls) {
        g_view.wall.assign(wall, wall + cells);
        for (uint16_t& w : g_view.wall) {
            if (w >= kVanillaWallCount) w = 0;
        }
        uint16_t* wallCopy = g_view.wall.data();
        std::memcpy(g_view.statics + g_view.wallAt, &wallCopy, sizeof(wallCopy));
    }
    return g_view.statics;
}

/** Troca a instrucao e confere os campos. Uma vez, depois do hook. */
void prepareSaveView(const MethodInfo* save) {
    Il2CppClass* data = il2cpp::findClass({"Terraria", "TileData", {}});
    FieldInfo* type = data ? il2cpp::findField(data, "TileType") : nullptr;
    FieldInfo* sHeader = data ? il2cpp::findField(data, "TileSHeader") : nullptr;
    if (!type || !sHeader) {
        BL_WARN("tiles de mod: TileData sem TileType/TileSHeader; o save tira os tiles de mod do mundo por um instante");
        return;
    }
    auto& a = il2cpp::api();
    g_view.tileData = data;
    g_view.typeAt = a.field_get_offset(type);
    g_view.sHeaderAt = a.field_get_offset(sHeader);
    FieldInfo* wall = il2cpp::findField(data, "TileWall");
    const size_t wallAt = wall ? a.field_get_offset(wall) : 0;
    if (wall && wallAt + 8 <= kStaticsSize) g_view.wallAt = wallAt;
    else BL_WARN("paredes de mod: TileData.TileWall fora do esperado (%zu); o save tira as paredes de mod do mundo "
                 "por um instante", wallAt);
    if (g_view.typeAt + 8 > kStaticsSize || g_view.sHeaderAt + 8 > kStaticsSize || !realStatics()) {
        BL_WARN("tiles de mod: estaticos do TileData fora do esperado (TileType %zu, TileSHeader %zu); "
                "o save tira os tiles de mod do mundo por um instante", g_view.typeAt, g_view.sHeaderAt);
        return;
    }
    if (!replaceInstruction(il2cpp::methodPointer(save), kLoadStaticsAt, kLoadStatics, kMovFromX27)) {
        BL_WARN("tiles de mod: SaveWorldTilesFast diferente do esperado; o save tira os tiles de mod do "
                "mundo por um instante");
        return;
    }
    g_view.ok = true;
    BL_DEBUG("tiles de mod: o save do mundo le uma copia das definicoes");
}

/** Os tiles de mod do mundo, para o arquivo ao lado. So leitura. */
std::vector<SavedTile> collectModTiles(const TileArrays& t) {
    std::vector<SavedTile> tiles;
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
    }
    return tiles;
}

/**
 * As paredes de mod do mundo. A parede nao carregada nao entra (no arquivo vai
 * a original, do g_keptWalls), mas conta em `placeholders`: ela tambem sai do
 * .wld, que senao a gravaria com um id que, com o mod de volta, e de outra parede.
 */
std::vector<SavedWall> collectModWalls(const TileArrays& t, size_t* placeholders) {
    std::vector<SavedWall> walls;
    *placeholders = 0;
    if (!t.wall) return walls;
    std::unordered_map<int, std::string> keys;
    const int unloaded = unloadedWallType();
    const int64_t n = static_cast<int64_t>(t.width) * t.height;
    for (int64_t pos = 0; pos < n; ++pos) {
        const int wall = t.wall[pos];
        if (wall < kVanillaWallCount) continue;
        if (wall == unloaded) {
            ++*placeholders;
            continue;
        }
        auto k = keys.find(wall);
        if (k == keys.end()) k = keys.emplace(wall, modWallKey(wall)).first;
        if (k->second.empty()) continue;
        SavedWall w;
        w.x = static_cast<int>(pos % t.width);
        w.y = static_cast<int>(pos / t.width);
        w.key = k->second;
        const Refs& r = g_refs;
        int32_t offset = static_cast<int32_t>(pos);
        if (r.getWallColor) w.color = r.getWallColor(&offset, r.mGetWallColor);
        if (r.getInvisible && r.getInvisible(&offset, r.mGetInvisible)) w.flags |= 1;
        if (r.getFullbright && r.getFullbright(&offset, r.mGetFullbright)) w.flags |= 2;
        walls.push_back(std::move(w));
    }
    return walls;
}

int32_t hkSaveWorldTiles(Il2CppObject* writer, const MethodInfo* m) {
    const std::string path = activeWorldPath();
    TileArrays t;
    const bool world = tileArrays(&t);
    const bool modTiles = world && tileTypeCount() > kVanillaTileCount;
    const bool modWalls = world && t.wall && wallTypeCount() > kVanillaWallCount;
    std::vector<SavedTile> tiles = modTiles ? collectModTiles(t) : std::vector<SavedTile>{};
    size_t placeholders = 0;
    std::vector<SavedWall> walls = modWalls ? collectModWalls(t, &placeholders) : std::vector<SavedWall>{};
    const bool stripWalls = !walls.empty() || placeholders > 0;
    int32_t r = 0;

    if (g_view.ok && (!stripWalls || g_view.wallAt)) {
        std::lock_guard<std::mutex> l(g_viewMx);
        r = bl_call_with_x27(writer, m, reinterpret_cast<void*>(g_origSave),
                             statsForSave(modTiles, stripWalls,
                                          static_cast<size_t>(t.width) * static_cast<size_t>(t.height)));
        g_view.type.clear();
        g_view.type.shrink_to_fit();
        g_view.sHeader.clear();
        g_view.sHeader.shrink_to_fit();
        g_view.wall.clear();
        g_view.wall.shrink_to_fit();
    } else {
        // O de antes: as definicoes de tipo de mod viram ar no mundo durante a
        // gravacao (o jogo ve os tiles de mod sumirem por um instante).
        std::unordered_map<uint32_t, std::pair<uint16_t, int16_t>> stripped;
        if (modTiles) {
            const int64_t n = static_cast<int64_t>(t.width) * t.height;
            for (int64_t pos = 0; pos < n; ++pos) {
                const uint32_t def = t.lookup[pos];
                if (t.type[def] >= kVanillaTileCount && (t.sHeader[def] & kTileActiveBit)) {
                    stripped.emplace(def, std::make_pair(t.type[def], t.sHeader[def]));
                }
            }
            for (const auto& [def, orig] : stripped) {
                t.type[def] = 0;
                t.sHeader[def] = static_cast<int16_t>(orig.second & ~kTileActiveBit);
            }
        }
        std::vector<std::pair<int64_t, uint16_t>> strippedWalls;
        if (stripWalls) {
            const int64_t n = static_cast<int64_t>(t.width) * t.height;
            for (int64_t pos = 0; pos < n; ++pos) {
                if (t.wall[pos] >= kVanillaWallCount) strippedWalls.push_back({pos, t.wall[pos]});
            }
            for (const auto& [pos, wall] : strippedWalls) t.wall[pos] = 0;
        }
        r = g_origSave(writer, m);
        for (const auto& [def, orig] : stripped) {
            t.type[def] = orig.first;
            t.sHeader[def] = orig.second;
        }
        for (const auto& [pos, wall] : strippedWalls) {
            if (t.wall[pos] == 0) t.wall[pos] = wall;
        }
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
        size_t keptWalls = 0;
        {
            // A original de mod ausente segue no arquivo enquanto a parede nao
            // carregada estiver no lugar; quebrada ou trocada, ela se perde
            // (como no tModLoader).
            const int unloaded = unloadedWallType();
            std::lock_guard<std::mutex> l(g_mx);
            auto it = g_keptWalls.find(path);
            if (it != g_keptWalls.end()) {
                std::vector<SavedWall>& kept = it->second;
                if (unloaded >= 0 && world && t.wall) {
                    kept.erase(std::remove_if(kept.begin(), kept.end(), [&](const SavedWall& w) {
                        const bool inside = w.x >= 0 && w.y >= 0 && w.x < t.width && w.y < t.height;
                        return !inside || t.wall[static_cast<int64_t>(t.width) * w.y + w.x] != unloaded;
                    }), kept.end());
                }
                keptWalls = kept.size();
                walls.insert(walls.end(), kept.begin(), kept.end());
            }
        }
        writeWallFile(path + kWallSuffix, walls);
        if (!walls.empty()) {
            BL_INFO("paredes de mod: mundo salvo sem parede de mod no .wld; %zu parede(s) em %s (%zu de mod "
                    "nao carregado)", walls.size(), (path + kWallSuffix).c_str(), keptWalls);
        }
    }
    return r;
}

// ------------------------------ carregar ------------------------------

void restoreModWalls(const std::string& path, const TileArrays* t) {
    const std::vector<SavedWall> saved = readWallFile(path + kWallSuffix);
    std::vector<SavedWall> kept;
    std::vector<std::pair<int, int>> placed;
    int builtOver = 0, placeholders = 0;
    const Refs& r = g_refs;
    const int unloaded = unloadedWallType();
    for (const SavedWall& s : saved) {
        int type = modWallTypeByKey(s.key);
        if (type < 0 || !t || !t->wall || !r.setWall) {
            kept.push_back(s);
            // Sem o mod: a parede nao carregada no lugar segura o que esta preso nela.
            if (unloaded < 0 || !t || !t->wall || !r.setWall) continue;
            if (s.x < 0 || s.y < 0 || s.x >= t->width || s.y >= t->height) continue;
            if (t->wall[t->width * s.y + s.x] != 0) continue;
            type = unloaded;
            ++placeholders;
        }
        if (s.x < 0 || s.y < 0 || s.x >= t->width || s.y >= t->height) continue;
        int32_t offset = t->width * s.y + s.x;
        if (t->wall[offset] != 0) {
            ++builtOver;
            continue;
        }
        r.setWall(&offset, static_cast<uint16_t>(type), r.mWall);
        if (r.setWallColor && s.color) r.setWallColor(&offset, static_cast<uint8_t>(s.color), r.mSetWallColor);
        if (r.setInvisible && (s.flags & 1)) r.setInvisible(&offset, true, r.mSetInvisible);
        if (r.setFullbright && (s.flags & 2)) r.setFullbright(&offset, true, r.mSetFullbright);
        placed.push_back({s.x, s.y});
    }
    if (r.wallFrame) {
        for (const auto& [x, y] : placed) r.wallFrame(x, y, true, r.mWallFrame);
    }
    {
        std::lock_guard<std::mutex> l(g_mx);
        if (kept.empty()) g_keptWalls.erase(path);
        else g_keptWalls[path] = kept;
    }
    if (!saved.empty()) {
        BL_INFO("paredes de mod: %zu parede(s) reposta(s), %zu de mod nao carregado (ficam no arquivo, %d com "
                "a parede nao carregada no lugar), %d com outra parede no lugar",
                placed.size() - static_cast<size_t>(placeholders), kept.size(), placeholders, builtOver);
    }
}

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
    restoreModWalls(path, world ? &t : nullptr);
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
    prepareSaveView(save);
    BL_DEBUG("tiles de mod: save do mundo pronto (%s)", kSuffix);
}

} // namespace bl::runtime
