#include "content/tiles/ModTileSave.h"
#include "core/Log.h"
#include "hook/CodePatch.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Types.h"
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
#include <tuple>
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
    int flags = 0;
    std::string key;
};

struct SavedTile {
    int x = 0, y = 0;
    std::string key;
    int frameX = 0, frameY = 0, sHeader = 0, bHeader = 0, bHeader2 = 0, bHeader3 = 0;
};

std::mutex g_mx;
std::map<std::string, std::vector<SavedTile>> g_kept;
std::map<std::string, std::vector<SavedWall>> g_keptWalls;

struct Refs {
    bool ok = false;
    FieldInfo* activeWorld = nullptr;
    FieldInfo* tileCut = nullptr;
    int32_t path = -1, cloud = -1;
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
    *fn = *reinterpret_cast<Fn const*>(*m);
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
    r.tileCut = findField(main, "tileCut");
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
        BL_ERROR("mod walls: Tile.set_wall not found; mod walls do not come back from the file");
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
    if (!r.getWallColor || !r.setWallColor) BL_WARN("mod walls: Tile.wallColor not found; mod wall paint does not come back");
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
                BL_ERROR("mod tiles: %s has an unknown header; ignored", path.c_str());
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

void writeFile(const std::string& path, const std::vector<SavedTile>& tiles) {
    if (tiles.empty()) {
        std::remove(path.c_str());
        return;
    }
    const std::string tmp = path + ".tmp";
    FILE* f = std::fopen(tmp.c_str(), "wb");
    if (!f) {
        BL_ERROR("mod tiles: could not write %s", tmp.c_str());
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
        BL_ERROR("mod tiles: failed to save %s", path.c_str());
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
                BL_ERROR("mod walls: %s has an unknown header; ignored", path.c_str());
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
        BL_ERROR("mod walls: could not write %s", tmp.c_str());
        return;
    }
    std::fprintf(f, "%s\n", kWallHeader);
    for (const SavedWall& w : walls) std::fprintf(f, "%d\t%d\t%d\t%d\t%s\n", w.x, w.y, w.color, w.flags, w.key.c_str());
    const bool ok = std::fflush(f) == 0;
    std::fclose(f);
    if (!ok || std::rename(tmp.c_str(), path.c_str()) != 0) {
        BL_ERROR("mod walls: failed to save %s", path.c_str());
        std::remove(tmp.c_str());
    }
}

using SaveTilesFn = int32_t (*)(Il2CppObject*, const MethodInfo*);
SaveTilesFn g_origSave = nullptr;

constexpr size_t kLoadStaticsAt = 0x104;
constexpr uint32_t kLoadStatics = 0xF9405D08;
constexpr uint32_t kMovFromX27 = 0xAA1B03E8;
constexpr size_t kClassStaticFields = 0xB8;
constexpr size_t kStaticsSize = 0xF0;
constexpr size_t kDefaultDefinitions = 200000;

struct SaveView {
    bool ok = false;
    Il2CppClass* tileData = nullptr;
    size_t typeAt = 0, sHeaderAt = 0;
    size_t wallAt = 0;
    alignas(16) uint8_t statics[kStaticsSize];
    std::vector<uint16_t> type;
    std::vector<int16_t> sHeader;
    std::vector<uint16_t> wall;
};
SaveView g_view;
std::mutex g_viewMx;
}
}

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

void* statsForSave(bool withModTiles, bool withModWalls, size_t cells) {
    uint8_t* real = realStatics();
    if ((!withModTiles && !withModWalls) || !real) return real;
    auto* type = *reinterpret_cast<uint16_t**>(real + g_view.typeAt);
    auto* sHeader = *reinterpret_cast<int16_t**>(real + g_view.sHeaderAt);
    auto* wall = g_view.wallAt ? *reinterpret_cast<uint16_t**>(real + g_view.wallAt) : nullptr;
    if (!type || !sHeader || (withModWalls && !wall)) return real;
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

void prepareSaveView(const MethodInfo* save) {
    Il2CppClass* data = il2cpp::findClass({"Terraria", "TileData", {}});
    FieldInfo* type = data ? il2cpp::findField(data, "TileType") : nullptr;
    FieldInfo* sHeader = data ? il2cpp::findField(data, "TileSHeader") : nullptr;
    if (!type || !sHeader) {
        BL_WARN("mod tiles: TileData has no TileType/TileSHeader; saving removes the mod tiles from the world for a moment");
        return;
    }
    auto& a = il2cpp::api();
    g_view.tileData = data;
    g_view.typeAt = a.field_get_offset(type);
    g_view.sHeaderAt = a.field_get_offset(sHeader);
    FieldInfo* wall = il2cpp::findField(data, "TileWall");
    const size_t wallAt = wall ? a.field_get_offset(wall) : 0;
    if (wall && wallAt + 8 <= kStaticsSize) g_view.wallAt = wallAt;
    else BL_WARN("mod walls: TileData.TileWall is not as expected (%zu); saving removes the mod walls from the world "
                 "for a moment", wallAt);
    if (g_view.typeAt + 8 > kStaticsSize || g_view.sHeaderAt + 8 > kStaticsSize || !realStatics()) {
        BL_WARN("mod tiles: TileData statics are not as expected (TileType %zu, TileSHeader %zu); "
                "saving removes the mod tiles from the world for a moment", g_view.typeAt, g_view.sHeaderAt);
        return;
    }
    if (!replaceInstruction(il2cpp::methodPointer(save), kLoadStaticsAt, kLoadStatics, kMovFromX27)) {
        BL_WARN("mod tiles: SaveWorldTilesFast differs from the expected; saving removes the mod tiles from the "
                "world for a moment");
        return;
    }
    g_view.ok = true;
    BL_DEBUG("mod tiles: the world save reads a copy of the definitions");
}

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
            BL_INFO("mod tiles: world saved with no mod tile in the .wld; %zu tile(s) in %s (%zu from a mod "
                    "that is not loaded)", tiles.size(), (path + kSuffix).c_str(), kept);
        }
        size_t keptWalls = 0;
        {
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
            BL_INFO("mod walls: world saved with no mod wall in the .wld; %zu wall(s) in %s (%zu from a mod "
                    "that is not loaded)", walls.size(), (path + kWallSuffix).c_str(), keptWalls);
        }
    }
    return r;
}

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
        BL_INFO("mod walls: %zu wall(s) restored, %zu from a mod that is not loaded (kept in the file, %d with "
                "the unloaded wall in place), %d with another wall in place",
                placed.size() - static_cast<size_t>(placeholders), kept.size(), placeholders, builtOver);
    }
}

using LoadTilesFn = void (*)(Il2CppObject*, Il2CppArray*, const MethodInfo*);
LoadTilesFn g_origLoad = nullptr;

int objectRow(const ModObjectShape& o, int frameY) {
    int row = 0;
    for (int rest = frameY % o.fullHeight; row + 1 < o.rows && rest - o.heights[row] - o.padding >= 0; ++row) {
        rest -= o.heights[row] + o.padding;
    }
    return row;
}

int rowTop(const ModObjectShape& o, int row) {
    int y = 0;
    for (int k = 0; k < row; ++k) y += o.heights[k] + o.padding;
    return y;
}

void hkLoadWorldTiles(Il2CppObject* reader, Il2CppArray* importance, const MethodInfo* m) {
    g_origLoad(reader, importance, m);
    const std::string path = activeWorldPath();
    if (path.empty()) return;
    const std::vector<SavedTile> saved = readFile(path + kSuffix);
    std::vector<SavedTile> kept;
    int restored = 0, builtOver = 0, objectsKept = 0, plantsCleared = 0, cellsRebuilt = 0;
    TileArrays t;
    const bool world = tileArrays(&t);
    const Refs& r = g_refs;
    const bool* cut = nullptr;
    uintptr_t cutLength = 0;
    if (r.tileCut) {
        Il2CppArray* arr = nullptr;
        il2cpp::api().field_static_get_value(r.tileCut, &arr);
        if (arr) {
            cut = static_cast<const bool*>(arrayData(arr));
            cutLength = arr->length;
        }
    }
    const auto write = [&](const SavedTile& s, int type, int32_t offset) {
        r.setSHeader(&offset, static_cast<int16_t>(s.sHeader), r.mSHeader);
        r.setType(&offset, static_cast<uint16_t>(type), r.mType);
        r.setFrameX(&offset, static_cast<int16_t>(s.frameX), r.mFrameX);
        r.setFrameY(&offset, static_cast<int16_t>(s.frameY), r.mFrameY);
        r.setB1(&offset, static_cast<uint8_t>(s.bHeader), r.mB1);
        r.setB2(&offset, static_cast<uint8_t>(s.bHeader2), r.mB2);
        r.setB3(&offset, static_cast<uint8_t>(s.bHeader3), r.mB3);
        ++restored;
    };

    struct Object { int type; ModObjectShape shape; std::vector<SavedTile> cells; };
    std::map<std::tuple<int, int, int>, Object> objects;
    for (const SavedTile& s : saved) {
        const int type = modTileTypeByKey(s.key);
        if (type < 0 || !world) {
            kept.push_back(s);
            continue;
        }
        if (s.x < 0 || s.y < 0 || s.x >= t.width || s.y >= t.height) continue;
        const int32_t offset = t.width * s.y + s.x;
        ModObjectShape shape;
        if (modObjectShape(type, &shape)) {
            const int col = (s.frameX % shape.fullWidth) / (shape.coordWidth + shape.padding);
            Object& o = objects[{type, s.x - col, s.y - objectRow(shape, s.frameY)}];
            o.type = type;
            o.shape = shape;
            o.cells.push_back(s);
            continue;
        }
        if (t.sHeader[t.lookup[offset]] & kTileActiveBit) {
            ++builtOver;
            continue;
        }
        write(s, type, offset);
    }
    for (auto& [where, o] : objects) {
        const auto [type, left, top] = where;
        const ModObjectShape& sh = o.shape;
        const SavedTile& ref = o.cells.front();
        const int refCol = ref.x - left, refRow = ref.y - top;
        const int baseX = ref.frameX - refCol * (sh.coordWidth + sh.padding);
        const int baseY = ref.frameY - rowTop(sh, refRow);
        int rebuilt = 0;
        for (int cx = 0; cx < sh.cols; ++cx) {
            for (int cy = 0; cy < sh.rows; ++cy) {
                const int x = left + cx, y = top + cy;
                const bool have = std::any_of(o.cells.begin(), o.cells.end(), [&](const SavedTile& c) { return c.x == x && c.y == y; });
                if (have) continue;
                SavedTile c = ref;
                c.x = x;
                c.y = y;
                c.frameX = baseX + cx * (sh.coordWidth + sh.padding);
                c.frameY = baseY + rowTop(sh, cy);
                o.cells.push_back(c);
                ++rebuilt;
            }
        }
        bool blocked = false;
        int plants = 0;
        for (const SavedTile& c : o.cells) {
            if (c.x < 0 || c.y < 0 || c.x >= t.width || c.y >= t.height) {
                blocked = true;
                break;
            }
            const uint32_t def = t.lookup[t.width * c.y + c.x];
            if (!(t.sHeader[def] & kTileActiveBit)) continue;
            const uint16_t occupant = t.type[def];
            if (cut && occupant < cutLength && cut[occupant]) ++plants;
            else blocked = true;
        }
        if (blocked) {
            for (size_t k = 0; k + rebuilt < o.cells.size(); ++k) kept.push_back(o.cells[k]);
            ++objectsKept;
            continue;
        }
        for (const SavedTile& c : o.cells) write(c, type, t.width * c.y + c.x);
        plantsCleared += plants;
        cellsRebuilt += rebuilt;
    }
    {
        std::lock_guard<std::mutex> l(g_mx);
        if (kept.empty()) g_kept.erase(path);
        else g_kept[path] = kept;
    }
    if (!saved.empty()) {
        BL_INFO("mod tiles: %d tile(s) restored, %zu kept in the file (%d object(s) with something built in place; "
                "the rest from a mod that is not loaded), %d plant(s) removed from object spots, %d half object cell(s) "
                "rebuilt, %d tile(s) with something built in place",
                restored, kept.size(), objectsKept, plantsCleared, cellsRebuilt, builtOver);
    }
    restoreModWalls(path, world ? &t : nullptr);
}

}

void installModTileSave() {
    Il2CppClass* worldFile = il2cpp::findClass({"Terraria.IO", "WorldFile", {}});
    auto& a = il2cpp::api();
    const MethodInfo* save = worldFile ? a.class_get_method_from_name(worldFile, "SaveWorldTilesFast", 1) : nullptr;
    const MethodInfo* load = worldFile ? a.class_get_method_from_name(worldFile, "LoadWorldTiles", 2) : nullptr;
    if (!resolve() || !save || !load || !hook::install(save, hkSaveWorldTiles, &g_origSave) ||
        !hook::install(load, hkLoadWorldTiles, &g_origLoad)) {
        BL_ERROR("mod tiles: no world save hook; a mod tile would go into the .wld by number");
        return;
    }
    prepareSaveView(save);
    BL_DEBUG("mod tiles: world save ready (%s)", kSuffix);
}

}
