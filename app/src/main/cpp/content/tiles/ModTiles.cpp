#include "content/tiles/ModTiles.h"
#include "content/tiles/ModTrees.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "hook/CodePatch.h"
#include "content/common/ContentAssets.h"
#include "content/common/GameRefs.h"
#include "content/common/TypeTables.h"
#include "content/tiles/ModTileMap.h"
#include "content/tiles/TileEvents.h"

#include <atomic>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <time.h>

namespace bl::runtime {

namespace {

struct Entry {
    ModTileDef def;
    uint32_t asset = 0;
};

std::mutex g_mx;
std::vector<Entry> g_regs;
std::atomic<TilesInstalledHook> g_managedInstalledHook{nullptr};
std::atomic<int> g_total{0};
std::atomic<int> g_installed{0};
std::atomic<bool> g_staticDone{false};
bool g_failed = false;

TypeTables g_tables("tiles de mod", kVanillaTileCount, {
    {"Terraria", "Main", ""},
    {"Terraria.ID", "TileID", "Sets"},
    {"Terraria.ID", "TileID", "Sets", "Conversion"},
    {"Terraria.ID", "TileID", "Sets", "TileCutIgnore"},
    {"Terraria.ID", "TileID", "Sets", "ForAdvancedCollision"},
    {"Terraria.ID", "TileID", "Sets", "RoomNeeds"},
    {"Terraria.ID", "TileID", "Sets", "Wiring"},
    {"Terraria.GameContent", "TextureAssets", ""},
    {"Terraria", "WorldGen", ""},
    {"Terraria.Map", "MapHelper", ""},
    {"Terraria", "Recipe", ""},
    {"Terraria.GameContent.Metadata", "TileMaterials", ""},
    {"Terraria", "WorldGen", "Skyblock"},
    {"Terraria.GameContent.Biomes", "CorruptionPitBiome", ""},
    {"Terraria.GameContent.Biomes.CaveHouse", "HouseUtils", ""},
}, TypeTables::Fill::Mode);

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* textures = nullptr;
    FieldInfo* solid = nullptr;
    FieldInfo* merge = nullptr;
    FieldInfo* players = nullptr;
    FieldInfo* guiActive = nullptr;
    int32_t guiCrafting = -1;
    int32_t adjTile = -1;
    int32_t oldAdjTile = -1;
    int32_t tileCounts = -1;
    const MethodInfo* playerCtor = nullptr;
    const MethodInfo* craftingCtor = nullptr;
    const MethodInfo* metricsCtor = nullptr;
    const MethodInfo* setupMerge = nullptr;
    const MethodInfo* sceneMetrics = nullptr;
    const MethodInfo* playerSceneMetrics = nullptr;
    const MethodInfo* cameraSceneMetrics = nullptr;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* textures = findClass({"Terraria.GameContent", "TextureAssets", {}});
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* player = findClass({"Terraria", "Player", {}});
    Il2CppClass* gui = findClassQuiet("", "GUIInstance");
    Il2CppClass* crafting = findClassQuiet("", "GUICrafting");
    Il2CppClass* metrics = findClass({"Terraria", "SceneMetrics", {}});
    r.textures = textures ? findField(textures, "Tile") : nullptr;
    r.solid = main ? findField(main, "tileSolid") : nullptr;
    r.merge = main ? findField(main, "tileMerge") : nullptr;
    r.players = main ? findField(main, "player") : nullptr;
    r.guiActive = gui ? findField(gui, "Active") : nullptr;
    r.guiCrafting = gui ? fieldOffset(gui, "GUICrafting") : -1;
    r.adjTile = player ? fieldOffset(player, "adjTile") : -1;
    r.oldAdjTile = crafting ? fieldOffset(crafting, "oldAdjTile") : -1;
    r.tileCounts = metrics ? fieldOffset(metrics, "_tileCounts") : -1;
    r.playerCtor = player ? a.class_get_method_from_name(player, ".ctor", 0) : nullptr;
    r.craftingCtor = crafting ? a.class_get_method_from_name(crafting, ".ctor", 0) : nullptr;
    r.metricsCtor = metrics ? a.class_get_method_from_name(metrics, ".ctor", 0) : nullptr;
    r.setupMerge = main ? a.class_get_method_from_name(main, "SetupTileMerge", 0) : nullptr;
    r.sceneMetrics = main ? a.class_get_method_from_name(main, "get_SceneMetrics", 0) : nullptr;
    r.playerSceneMetrics = main ? a.class_get_method_from_name(main, "get_PlayerSceneMetrics", 0) : nullptr;
    r.cameraSceneMetrics = main ? a.class_get_method_from_name(main, "get__cameraSceneMetrics", 0) : nullptr;
    r.ok = r.textures && r.solid && r.merge && r.players && r.adjTile >= 0 && r.tileCounts >= 0 &&
           r.playerCtor && r.metricsCtor && r.setupMerge;
    if (!r.ok) {
        BL_ERROR("tiles de mod: refs faltando (TextureAssets.Tile=%p Main.tileMerge=%p "
                 "Player.adjTile=%d SceneMetrics._tileCounts=%d)",
                 (void*)r.textures, (void*)r.merge, r.adjTile, r.tileCounts);
    }
    return r;
}

Il2CppArray* readStatic(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

void applyTexture(int type, const Entry& e) {
    Il2CppObject* asset = e.asset ? il2cpp::api().gchandle_get_target(e.asset) : nullptr;
    if (asset) content::setTableElement(refs().textures, type, asset);
}

bool fixTileMerge(int total) {
    const Refs& r = refs();
    Il2CppArray* outer = readStatic(r.merge);
    if (!outer || outer->length < static_cast<uintptr_t>(total)) return false;
    auto** rows = static_cast<Il2CppArray**>(arrayData(outer));
    Il2CppArray* last = rows[total - 1];
    if (last && last != rows[0] && last->length >= static_cast<uintptr_t>(total) &&
        rows[0] && rows[0]->length >= static_cast<uintptr_t>(total)) {
        return true;
    }
    static Il2CppClass* boolCls = nullptr;
    if (!boolCls) boolCls = il2cpp::findClass({"System", "Boolean", {}});
    if (!boolCls) return false;
    auto& a = il2cpp::api();
    Il2CppArray* dirt = rows[0];
    int fixed = 0;
    for (int i = 0; i < total; ++i) {
        Il2CppArray* row = rows[i];
        const bool own = row && (i < kVanillaTileCount || row != dirt);
        if (own && row->length >= static_cast<uintptr_t>(total)) continue;
        Il2CppArray* n = own ? TypeTables::resizedCopy(row, static_cast<uintptr_t>(total))
                             : a.array_new(boolCls, static_cast<uintptr_t>(total));
        if (!n) {
            BL_ERROR("tiles de mod: Main.tileMerge: nao deu para criar a linha %d", i);
            return false;
        }
        content::setTableElement(r.merge, i, reinterpret_cast<Il2CppObject*>(n));
        ++fixed;
    }
    if (fixed) BL_DEBUG("tiles de mod: Main.tileMerge: %d linha(s) com %d posicoes", fixed, total);
    return true;
}

using SetupMergeFn = void (*)(const MethodInfo*);
SetupMergeFn g_origSetupMerge = nullptr;

void hkSetupTileMerge(const MethodInfo* m) {
    const int total = tileTypeCount();
    const Refs& r = refs();
    std::vector<uint8_t> rows, cols;
    const int mods = total - kVanillaTileCount;
    Il2CppArray* outer = mods > 0 ? readStatic(r.merge) : nullptr;
    const bool had = outer && fixTileMerge(total);
    if (had) {
        auto** rw = static_cast<Il2CppArray**>(arrayData(outer));
        rows.resize(static_cast<size_t>(mods) * total);
        cols.resize(static_cast<size_t>(kVanillaTileCount) * mods);
        for (int t = 0; t < mods; ++t) {
            std::memcpy(&rows[static_cast<size_t>(t) * total], arrayData(rw[kVanillaTileCount + t]), total);
        }
        for (int v = 0; v < kVanillaTileCount; ++v) {
            std::memcpy(&cols[static_cast<size_t>(v) * mods],
                        static_cast<uint8_t*>(arrayData(rw[v])) + kVanillaTileCount, mods);
        }
    }
    g_origSetupMerge(m);
    if (mods <= 0) return;
    outer = readStatic(r.merge);
    BL_DEBUG("tiles de mod: o jogo montou o tileMerge (%d linhas)", outer ? static_cast<int>(outer->length) : -1);
    if (outer && outer->length < static_cast<uintptr_t>(total)) {
        if (Il2CppArray* bigger = TypeTables::resizedCopy(outer, static_cast<uintptr_t>(total))) {
            il2cpp::api().field_static_set_value(r.merge, bigger);
        }
    }
    if (!fixTileMerge(total) || !had) return;
    outer = readStatic(r.merge);
    auto** rw = static_cast<Il2CppArray**>(arrayData(outer));
    for (int t = 0; t < mods; ++t) {
        std::memcpy(arrayData(rw[kVanillaTileCount + t]), &rows[static_cast<size_t>(t) * total], total);
    }
    for (int v = 0; v < kVanillaTileCount; ++v) {
        std::memcpy(static_cast<uint8_t*>(arrayData(rw[v])) + kVanillaTileCount,
                    &cols[static_cast<size_t>(v) * mods], mods);
    }
    BL_DEBUG("tiles de mod: o jogo refez o tileMerge; o dos mods foi devolvido");
}

void onTableRegrown(FieldInfo* f, int size) {
    const Refs& r = refs();
    if (f == r.merge) fixTileMerge(size);
    if (f != r.textures) return;
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size() && static_cast<int>(i) < size - kVanillaTileCount; ++i) {
        applyTexture(kVanillaTileCount + static_cast<int>(i), g_regs[i]);
    }
}

Il2CppObject* objectDataList();
bool hasObjectDataField();

bool gameReady(int size) {
    const Refs& r = refs();
    if (!r.ok) return false;
    Il2CppArray* textures = readStatic(r.textures);
    Il2CppArray* solid = readStatic(r.solid);
    return textures && solid &&
           textures->length == static_cast<uintptr_t>(size) && solid->length == static_cast<uintptr_t>(size);
}

struct LimitPatch {
    const char* ns;
    const char* cls;
    const char* method;
    uint32_t vanilla;
    int offset;
    bool belowToo;
    bool shifted = false;
};

constexpr LimitPatch kLimitPatches[] = {
    {"Terraria", "WorldGen", "PlaceTile", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "PlaceObject", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "KillTile", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "KillWall", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "TileFrameImportant", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "WouldTileReplacementWork", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "CheckPile", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "Check2x1", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "Check2x2Style", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "placeTrap", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "BlockBelowMakesSandConvertIntoHardenedSand", kVanillaTileCount - 1, -1, false},
    {"Terraria", "DelegateMethods", "SpreadTile", kVanillaTileCount - 1, -1, false},
    {"Terraria", "Projectile", "AI_006", kVanillaTileCount - 1, -1, false},
    {"Terraria", "Player", "PlaceThing_Tiles_CheckRopeUsability", kVanillaTileCount - 1, -1, false},
    {"Terraria.GameContent", "SmartCursorHelper", "Step_Boulders", kVanillaTileCount - 1, -1, false},
    {"Terraria.GameContent", "SmartCursorHelper", "Step_PumpkinSeeds", kVanillaTileCount - 1, -1, false},
    {"Terraria", "WorldGen", "TileFrameCosmetic", kVanillaTileCount, 0, true},
    {"Terraria", "WorldGen", "ValidateTypes", kVanillaTileCount, 0, true},
    {"Terraria", "Collision", "IsWorldPointSolid", kVanillaTileCount, 0, false},
    {"Terraria", "TileData", "GetTileDefinition", kVanillaTileCount, 0, false, true},
};

void patchLimits(int total) {
    auto& a = il2cpp::api();
    int methodsOk = 0;
    for (const LimitPatch& p : kLimitPatches) {
        Il2CppClass* cls = il2cpp::findClass({p.ns, p.cls, {}});
        const uint32_t to = static_cast<uint32_t>(total + p.offset);
        int patched = 0;
        const MethodInfo* last = nullptr;
        void* it = nullptr;
        while (const MethodInfo* m = cls ? a.class_get_methods(cls, &it) : nullptr) {
            if (std::strcmp(a.method_get_name(m), p.method) != 0) continue;
            last = m;
            patched += patchCompareLimit(m, p.vanilla, to, p.belowToo, p.shifted);
        }
        if (patched > 0) {
            ++methodsOk;
            BL_DEBUG("tiles de mod: %s.%s: %d limite(s) de %u para %u", p.cls, p.method, patched, p.vanilla, to);
        } else {
            BL_ERROR("tiles de mod: %s.%s: limite %u nao trocado: %s", p.cls, p.method, p.vanilla,
                     describeCompareMiss(last, p.vanilla, to, p.belowToo, p.shifted).c_str());
        }
    }
    const int methods = static_cast<int>(sizeof(kLimitPatches) / sizeof(kLimitPatches[0]));
    BL_INFO("tiles de mod: limites do codigo trocados em %d de %d metodo(s)", methodsOk, methods);
}

using AllocateFn = void (*)(Il2CppObject*, int32_t, int32_t, const MethodInfo*);
AllocateFn g_origAllocate = nullptr;
FieldInfo* g_tileLists = nullptr;
uint32_t* g_lists = nullptr;
size_t g_listsEntries = 0;

bool setStaticPointer(FieldInfo* f, void* value) {
    auto& a = il2cpp::api();
    void* back = nullptr;
    a.field_static_set_value(f, value);
    a.field_static_get_value(f, &back);
    if (back == value) return true;
    a.field_static_set_value(f, &value);
    a.field_static_get_value(f, &back);
    return back == value;
}

void hkAllocate(Il2CppObject* self, int32_t x, int32_t y, const MethodInfo* m) {
    g_origAllocate(self, x, y, m);
    const int total = tileTypeCount();
    if (total <= kVanillaTileCount || !g_tileLists) return;
    const size_t entries = static_cast<size_t>(total) << 12;
    const size_t vanilla = static_cast<size_t>(kVanillaTileCount) << 12;
    if (!g_lists || g_listsEntries < entries) {
        g_lists = static_cast<uint32_t*>(std::malloc(entries * sizeof(uint32_t)));
        g_listsEntries = entries;
    }
    if (!g_lists) {
        BL_ERROR("tiles de mod: sem memoria para as listas de tile (%zu entradas)", entries);
        return;
    }
    uint32_t* old = nullptr;
    il2cpp::api().field_static_get_value(g_tileLists, &old);
    if (old == g_lists) return;
    std::memset(g_lists, 0xFF, entries * sizeof(uint32_t));
    if (old) std::memcpy(g_lists, old, vanilla * sizeof(uint32_t));
    if (!setStaticPointer(g_tileLists, g_lists)) {
        BL_ERROR("tiles de mod: nao deu para trocar TileData.TileLists; tile de mod corrompe o mundo");
        return;
    }
    BL_DEBUG("tiles de mod: listas de tile do mundo com %zu entradas (%d tipos)", entries, total);
}

void hookAllocate() {
    Il2CppClass* data = il2cpp::findClass({"Terraria", "TileData", {}});
    const MethodInfo* alloc = data ? il2cpp::api().class_get_method_from_name(data, "Allocate", 2) : nullptr;
    g_tileLists = data ? il2cpp::findField(data, "TileLists") : nullptr;
    if (!alloc || !g_tileLists || !hook::install(alloc, hkAllocate, &g_origAllocate)) {
        BL_ERROR("tiles de mod: sem hook no TileData.Allocate; tile de mod corrompe o mundo");
    }
}

bool fixGlowMask(int total) {
    static FieldInfo* f = [] {
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        return main ? il2cpp::findField(main, "tileGlowMask") : nullptr;
    }();
    static Il2CppArray* fixedFor = nullptr;
    Il2CppArray* glow = f ? readStatic(f) : nullptr;
    if (!glow || glow == fixedFor || glow->length < static_cast<uintptr_t>(total)) return false;
    auto* v = static_cast<int16_t*>(arrayData(glow));
    if (v[0] != -1 && v[1] != -1) return false;
    for (int t = kVanillaTileCount; t < total; ++t) v[t] = -1;
    fixedFor = glow;
    return true;
}

using MapInitFn = void (*)(const MethodInfo*);
MapInitFn g_origMapInit = nullptr;

void hkMapInitialize(const MethodInfo* m) {
    g_origMapInit(m);
    const int total = tileTypeCount();
    if (total <= kVanillaTileCount) return;
    g_tables.watch(total, onTableRegrown);
    applyModTileMap(total, true);
}

void hookMapInitialize() {
    Il2CppClass* map = il2cpp::findClass({"Terraria.Map", "MapHelper", {}});
    const MethodInfo* init = map ? il2cpp::api().class_get_method_from_name(map, "Initialize", 0) : nullptr;
    if (!init || !hook::install(init, hkMapInitialize, &g_origMapInit)) {
        BL_ERROR("tiles de mod: sem hook no MapHelper.Initialize; o mapa pode ler alem do fim ao entrar no mundo");
    }
}

struct ObjectDataRefs {
    bool tried = false;
    FieldInfo* data = nullptr;
    FieldInfo* readOnly = nullptr;
};

ObjectDataRefs& objectDataRefs() {
    static ObjectDataRefs r;
    if (r.tried) return r;
    r.tried = true;
    Il2CppClass* cls = il2cpp::findClass({"Terraria.ObjectData", "TileObjectData", {}});
    r.data = cls ? il2cpp::findField(cls, "_data") : nullptr;
    r.readOnly = cls ? il2cpp::findField(cls, "readOnlyData") : nullptr;
    if (!r.data || !r.readOnly) {
        BL_ERROR("tiles de mod: TileObjectData sem _data/readOnlyData; moveis de mod nao funcionam");
    }
    return r;
}

bool hasObjectDataField() {
    return objectDataRefs().data != nullptr;
}

Il2CppObject* objectDataList() {
    Il2CppObject* list = nullptr;
    if (FieldInfo* f = objectDataRefs().data) il2cpp::api().field_static_get_value(f, &list);
    return list;
}

std::vector<uint32_t> g_objectData;

bool padTileObjectData(int total) {
    auto& a = il2cpp::api();
    if (!objectDataRefs().data) return true;
    Il2CppObject* list = objectDataList();
    if (!list) return false;
    Il2CppClass* lc = a.object_get_class(list);
    const MethodInfo* count = a.class_get_method_from_name(lc, "get_Count", 0);
    const MethodInfo* add = a.class_get_method_from_name(lc, "Add", 1);
    const MethodInfo* set = a.class_get_method_from_name(lc, "set_Item", 2);
    if (!count || !add) return true;
    Il2CppObject* exc = nullptr;
    Il2CppObject* boxed = a.runtime_invoke(count, list, nullptr, &exc);
    int n = boxed && !exc ? *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject)) : -1;
    const int before = n;
    while (n >= 0 && n < total) {
        void* args[1] = {nullptr};
        a.runtime_invoke(add, list, args, &exc);
        if (exc) break;
        ++n;
    }
    if (before >= 0 && n > before) {
        BL_DEBUG("tiles de mod: TileObjectData._data de %d para %d", before, n);
        int restored = 0;
        for (size_t i = 0; set && i < g_objectData.size(); ++i) {
            Il2CppObject* obj = g_objectData[i] ? a.gchandle_get_target(g_objectData[i]) : nullptr;
            const int32_t type = kVanillaTileCount + static_cast<int32_t>(i);
            if (!obj || type >= n) continue;
            void* args[2] = {const_cast<int32_t*>(&type), obj};
            a.runtime_invoke(set, list, args, &exc);
            if (!exc) ++restored;
        }
        if (restored) BL_DEBUG("tiles de mod: %d forma(s) de tile de mod devolvida(s) ao TileObjectData", restored);
    }
    return true;
}

bool setObjectDataLocked(bool locked) {
    FieldInfo* f = objectDataRefs().readOnly;
    if (!f) return false;
    auto& a = il2cpp::api();
    bool was = false;
    a.field_static_get_value(f, &was);
    a.field_static_set_value(f, &locked);
    return was;
}

struct DrawShape {
    bool ok = false;
    int width = 16, yOffset = 0, padding = 2, rows = 1, fullHeight = 18;
    int cols = 1, fullWidth = 18;
    std::vector<int> heights;
};

std::vector<DrawShape> g_drawShapes;

constexpr size_t kMaxAnimated = 8192;
std::atomic<int> g_animHeight[kMaxAnimated];
FieldInfo* g_tileFrameField = nullptr;

using DrawDataFn = void (*)(Il2CppObject*, int32_t, int32_t, void*, uint16_t, int16_t*, int16_t*, int32_t*,
                            int32_t*, int32_t*, int32_t*, int32_t*, int32_t*, int32_t*, Il2CppObject**, void*,
                            void*, const MethodInfo*);
DrawDataFn g_origDrawData = nullptr;
DrawDataFn g_origSpecialDrawData = nullptr;

void applyTileDrawData(uint16_t type, int16_t* frameY, int32_t* width, int32_t* height, int32_t* top,
                       int32_t* addFrY) {
    if (type < kVanillaTileCount) return;
    const size_t i = static_cast<size_t>(type) - kVanillaTileCount;

    const int frameHeight = i < kMaxAnimated ? g_animHeight[i].load(std::memory_order_relaxed) : 0;
    int32_t tileFrame = 0;
    if (frameHeight > 0 && g_tileFrameField) {
        Il2CppArray* frames = readStatic(g_tileFrameField);
        if (frames && type < frames->length) tileFrame = static_cast<const int32_t*>(arrayData(frames))[type];
    }
    *addFrY = tileFrame * frameHeight;

    if (i < g_drawShapes.size() && g_drawShapes[i].ok) {
        const DrawShape& s = g_drawShapes[i];
        int row = 0;
        for (int rest = s.fullHeight > 0 ? *frameY % s.fullHeight : 0;
             row + 1 < s.rows && rest - s.heights[row] - s.padding >= 0; ++row) {
            rest -= s.heights[row] + s.padding;
        }
        *width = s.width;
        *top = s.yOffset;
        *height = s.heights[row];
    }
}

void hkGetTileDrawData(Il2CppObject* self, int32_t x, int32_t y, void* info, uint16_t type, int16_t* frameX,
                       int16_t* frameY, int32_t* width, int32_t* height, int32_t* top, int32_t* halfBrick,
                       int32_t* addFrX, int32_t* addFrY, int32_t* effects, Il2CppObject** glow, void* glowRect,
                       void* glowColor, const MethodInfo* m) {
    g_origDrawData(self, x, y, info, type, frameX, frameY, width, height, top, halfBrick, addFrX, addFrY, effects, glow, glowRect, glowColor, m);
    noteTreeDrawTile(x, y, type, *frameX, *frameY);
    applyTileDrawData(type, frameY, width, height, top, addFrY);
    if (hasManagedTileDrawData(type)) {
        TileDrawData managed{*frameX, *frameY, *width, *height, *top, *addFrX, *addFrY, *effects};
        managedTileDrawData(x, y, type, &managed, false);
        *frameX = managed.frameX; *frameY = managed.frameY; *width = managed.width; *height = managed.height;
        *top = managed.top; *addFrX = managed.addFrX; *addFrY = managed.addFrY; *effects = managed.effects;
    }
}

void hkGetSpecialTileDrawData(Il2CppObject* self, int32_t x, int32_t y, void* tile, uint16_t type, int16_t* frameX,
                       int16_t* frameY, int32_t* width, int32_t* height, int32_t* top, int32_t* halfBrick,
                       int32_t* addFrX, int32_t* addFrY, int32_t* effects, Il2CppObject** glow, void* glowRect,
                       void* glowColor, const MethodInfo* m) {
    g_origSpecialDrawData(self, x, y, tile, type, frameX, frameY, width, height, top, halfBrick, addFrX, addFrY, effects, glow, glowRect, glowColor, m);
    applyTileDrawData(type, frameY, width, height, top, addFrY);
    if (hasManagedTileDrawData(type)) {
        TileDrawData managed{*frameX, *frameY, *width, *height, *top, *addFrX, *addFrY, *effects};
        managedTileDrawData(x, y, type, &managed, true);
        *frameX = managed.frameX; *frameY = managed.frameY; *width = managed.width; *height = managed.height;
        *top = managed.top; *addFrX = managed.addFrX; *addFrY = managed.addFrY; *effects = managed.effects;
    }
}

int invokeInt(Il2CppObject* obj, const char* getter, bool* ok) {
    auto& a = il2cpp::api();
    const MethodInfo* m = a.class_get_method_from_name(a.object_get_class(obj), getter, 0);
    Il2CppObject* exc = nullptr;
    Il2CppObject* boxed = m ? a.runtime_invoke(m, obj, nullptr, &exc) : nullptr;
    if (!boxed || exc) {
        *ok = false;
        return 0;
    }
    return *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject));
}

DrawShape readDrawShape(Il2CppObject* data) {
    auto& a = il2cpp::api();
    DrawShape s;
    bool ok = true;
    s.width = invokeInt(data, "get_CoordinateWidth", &ok);
    s.yOffset = invokeInt(data, "get_DrawYOffset", &ok);
    s.padding = invokeInt(data, "get_CoordinatePadding", &ok);
    s.rows = invokeInt(data, "get_Height", &ok);
    s.fullHeight = invokeInt(data, "get_CoordinateFullHeight", &ok);
    s.cols = invokeInt(data, "get_Width", &ok);
    s.fullWidth = invokeInt(data, "get_CoordinateFullWidth", &ok);
    const MethodInfo* get = a.class_get_method_from_name(a.object_get_class(data), "get_CoordinateHeights", 0);
    Il2CppObject* exc = nullptr;
    auto* heights = get ? reinterpret_cast<Il2CppArray*>(a.runtime_invoke(get, data, nullptr, &exc)) : nullptr;
    if (!ok || exc || !heights || heights->length == 0 || s.rows <= 0) return {};
    const auto* h = static_cast<const int32_t*>(arrayData(heights));
    s.heights.assign(h, h + heights->length);
    while (static_cast<int>(s.heights.size()) < s.rows) s.heights.push_back(s.heights.back());
    s.ok = true;
    return s;
}

void hookDrawData() {
    static bool done = false;
    if (done) return;
    done = true;
    auto& a = il2cpp::api();
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    g_tileFrameField = main ? il2cpp::findField(main, "tileFrame") : nullptr;
    Il2CppClass* cls = il2cpp::findClass({"Terraria.GameContent.Drawing", "TileDrawing", {}});
    const MethodInfo* target = nullptr;
    const MethodInfo* special = nullptr;
    void* it = nullptr;
    while (const MethodInfo* m = cls ? a.class_get_methods(cls, &it) : nullptr) {
        if (std::strcmp(a.method_get_name(m), "GetTileDrawData") != 0 || a.method_get_param_count(m) != 16) continue;
        char* name = a.type_get_name(a.method_get_param(m, 2));
        const bool drawInfo = name && std::strstr(name, "TileDrawInfo");
        const bool drawTile = name && std::strcmp(name, "Terraria.Tile") == 0;
        std::free(name);
        if (drawInfo) target = m;
        if (drawTile) special = m;
    }

    if (!special || !hook::install(special, hkGetSpecialTileDrawData, &g_origSpecialDrawData))
        BL_ERROR("tiles de mod: sem hook no GetTileDrawData(Tile); desenho especial indisponivel");
    if (!target || !hook::install(target, hkGetTileDrawData, &g_origDrawData)) {
        BL_ERROR("tiles de mod: sem hook no TileDrawing.GetTileDrawData; a ultima linha de um movel de mod sai cortada");
        return;
    }
}

void keepObjectData(int first, int last) {
    auto& a = il2cpp::api();
    Il2CppObject* list = objectDataList();
    const MethodInfo* get = list ? a.class_get_method_from_name(a.object_get_class(list), "get_Item", 1) : nullptr;
    int shaped = 0;
    for (int type = first; type <= last; ++type) {
        const size_t i = static_cast<size_t>(type - kVanillaTileCount);
        if (g_objectData.size() <= i) g_objectData.resize(i + 1, 0);
        if (!get) continue;
        Il2CppObject* exc = nullptr;
        void* args[1] = {&type};
        Il2CppObject* obj = a.runtime_invoke(get, list, args, &exc);
        if (exc || !obj) continue;
        g_objectData[i] = a.gchandle_new(obj, false);
        if (g_drawShapes.size() <= i) g_drawShapes.resize(i + 1);
        g_drawShapes[i] = readDrawShape(obj);
        ++shaped;
    }
    hookDrawData();
    if (shaped) {
        BL_DEBUG("tiles de mod: %d tile(s) de mod com TileObjectData (moveis/objetos)", shaped);
    }
}

void runStaticDefaults(int first, int last) {
    if (hasObjectDataField() && !objectDataList()) return;
    padTileObjectData(last + 1);
    const bool wasLocked = setObjectDataLocked(false);
    if (TilesInstalledHook hook = g_managedInstalledHook.load(std::memory_order_acquire)) hook(first, last);
    setObjectDataLocked(wasLocked);
    keepObjectData(first, last);
    g_staticDone.store(true, std::memory_order_release);
}

using CtorFn = void (*)(Il2CppObject*, const MethodInfo*);
CtorFn g_origPlayerCtor = nullptr;
CtorFn g_origCraftingCtor = nullptr;
CtorFn g_origMetricsCtor = nullptr;

void growOne(Il2CppObject* obj, int32_t offset) {
    if (tileTypeCount() > kVanillaTileCount) {
        TypeTables::growInstanceTable(obj, offset, kVanillaTileCount, tileTypeCount(), nullptr);
    }
}

void hkPlayerCtor(Il2CppObject* self, const MethodInfo* m) {
    g_origPlayerCtor(self, m);
    growOne(self, refs().adjTile);
}

void hkCraftingCtor(Il2CppObject* self, const MethodInfo* m) {
    g_origCraftingCtor(self, m);
    growOne(self, refs().oldAdjTile);
}

void hkMetricsCtor(Il2CppObject* self, const MethodInfo* m) {
    g_origMetricsCtor(self, m);
    growOne(self, refs().tileCounts);
}

Il2CppObject* callStatic(const MethodInfo* m) {
    if (!m) return nullptr;
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = il2cpp::api().runtime_invoke(m, nullptr, nullptr, &exc);
    return exc ? nullptr : r;
}

void growInstances(int size) {
    static bool hooked = false;
    const Refs& r = refs();
    if (!hooked) {
        hooked = true;
        if (!hook::install(r.playerCtor, hkPlayerCtor, &g_origPlayerCtor)) {
            BL_ERROR("tiles de mod: sem hook no construtor de Player; jogador novo le fora de adjTile");
        }
        if (!hook::install(r.metricsCtor, hkMetricsCtor, &g_origMetricsCtor)) {
            BL_ERROR("tiles de mod: sem hook no construtor de SceneMetrics; bioma novo le fora da contagem");
        }
        if (r.craftingCtor && r.oldAdjTile >= 0 && !hook::install(r.craftingCtor, hkCraftingCtor, &g_origCraftingCtor)) {
            BL_ERROR("tiles de mod: sem hook no construtor de GUICrafting");
        }
    }
    int players = 0;
    Il2CppArray* list = readStatic(r.players);
    for (uintptr_t i = 0; list && i < list->length; ++i) {
        Il2CppObject* p = static_cast<Il2CppObject**>(arrayData(list))[i];
        if (!p) continue;
        TypeTables::growInstanceTable(p, r.adjTile, kVanillaTileCount, size, nullptr);
        ++players;
    }
    int metrics = 0;
    for (const MethodInfo* get : {r.sceneMetrics, r.playerSceneMetrics, r.cameraSceneMetrics}) {
        if (Il2CppObject* s = callStatic(get)) {
            TypeTables::growInstanceTable(s, r.tileCounts, kVanillaTileCount, size, nullptr);
            ++metrics;
        }
    }
    Il2CppObject* gui = nullptr;
    if (r.guiActive) il2cpp::api().field_static_get_value(r.guiActive, &gui);
    Il2CppObject* crafting = gui && r.guiCrafting >= 0 ? field<Il2CppObject*>(gui, r.guiCrafting) : nullptr;
    if (crafting) TypeTables::growInstanceTable(crafting, r.oldAdjTile, kVanillaTileCount, size, nullptr);
    BL_DEBUG("tiles de mod: adjTile em %d jogador(es), contagem de bioma em %d, menu de criacao %s",
             players, metrics, crafting ? "sim" : "ainda nao existe");
}

}

void ensureTileDrawHook() {
    hookDrawData();
}

int registerModTile(ModTileDef def) {
    std::lock_guard<std::mutex> l(g_mx);
    for (const Entry& e : g_regs) {
        if (e.def.mod == def.mod && e.def.name == def.name) return -1;
    }
    Entry e;
    e.def = std::move(def);
    g_regs.push_back(std::move(e));
    g_total.store(static_cast<int>(g_regs.size()), std::memory_order_release);
    return kVanillaTileCount + static_cast<int>(g_regs.size()) - 1;
}

bool isModTile(int type) {
    return type >= kVanillaTileCount && type < kVanillaTileCount + g_total.load(std::memory_order_acquire);
}

int tileTypeCount() {
    return kVanillaTileCount + g_installed.load(std::memory_order_acquire);
}

void tickModTiles() {
    const int total = g_total.load(std::memory_order_acquire);
    if (total == 0 || g_failed) return;
    const int installed = g_installed.load(std::memory_order_relaxed);
    const int from = kVanillaTileCount + installed;

    if (installed == total) {
        if (!g_staticDone.load(std::memory_order_relaxed)) runStaticDefaults(kVanillaTileCount, from - 1);
        g_tables.checkPending(from);
        fixTileMerge(from);
        if (fixGlowMask(from)) BL_DEBUG("tiles de mod: tileGlowMask dos tipos de mod em -1");
        applyModTileMap(from, false);
        static bool padded = false;
        static int frame = 0;
        ++frame;
        if (!padded || frame % 120 == 0) padded = padTileObjectData(from);
        if (frame % 120 == 0) g_tables.watch(from, onTableRegrown);
        return;
    }
    if (!gameReady(from)) return;
    if (installed > 0) {
        BL_ERROR("tiles de mod: %d registrado(s) depois da instalacao; ficam sem tabela", total - installed);
        g_failed = true;
        return;
    }

    const int to = kVanillaTileCount + total;
    if (!hook::install(refs().setupMerge, hkSetupTileMerge, &g_origSetupMerge)) {
        BL_ERROR("tiles de mod: sem hook no Main.SetupTileMerge; o tileMerge dos mods se perde quando o jogo o refizer");
    }
    hookAllocate();
    hookMapInitialize();
    installModTileMap();
    const bool mergeMissing = !readStatic(refs().merge);
    const int grown = g_tables.grow(from, to);
    if (grown < 0) {
        BL_ERROR("tiles de mod: nenhuma tabela de tile achada; tiles de mod desligados");
        g_failed = true;
        return;
    }
    g_installed.store(total, std::memory_order_release);
    BL_DEBUG("tiles de mod: tileMerge na instalacao: %s", mergeMissing ? "ainda nao existe" : "ja existe");
    if (mergeMissing) {
        Il2CppObject* exc = nullptr;
        il2cpp::api().runtime_invoke(refs().setupMerge, nullptr, nullptr, &exc);
        if (exc) BL_ERROR("tiles de mod: Main.SetupTileMerge lancou excecao");
    }
    fixTileMerge(to);
    fixGlowMask(to);
    padTileObjectData(to);
    growInstances(to);
    patchLimits(to);

    {
        std::lock_guard<std::mutex> l(g_mx);
        for (int i = installed; i < total; ++i) {
            Entry& e = g_regs[static_cast<size_t>(i)];
            int w = 0, h = 0;
            Il2CppObject* asset = content::loadTextureAsset(e.def.texture, nullptr, 0,
                                                            e.def.mod + "/" + e.def.name, &w, &h);
            if (asset) e.asset = il2cpp::api().gchandle_new(asset, false);
            applyTexture(kVanillaTileCount + i, e);
        }
    }
    BL_INFO("tiles de mod: %d instalado(s) (ids %d..%d), %d tabela(s) aumentadas de %d para %d",
            total - installed, from, to - 1, grown, from, to);
    runStaticDefaults(from, to - 1);
}

bool modTilesSettled() {
    const int total = g_total.load(std::memory_order_acquire);
    return total == 0 || g_failed || (g_installed.load(std::memory_order_relaxed) == total &&
                        g_staticDone.load(std::memory_order_acquire));
}

void readManagedTileDrawData(void* renderer, int x, int y, int offset, int type, void* data, int* halfBrick) {
    auto* d = static_cast<TileDrawData*>(data);
    if (!renderer || !d || !halfBrick || !g_origSpecialDrawData) return;
    int16_t frameX = d->frameX, frameY = d->frameY;
    Il2CppObject* glow = nullptr;
    int32_t rectangle[4]{}; uint32_t color = 0;
    g_origSpecialDrawData(static_cast<Il2CppObject*>(renderer), x, y, reinterpret_cast<void*>(static_cast<intptr_t>(offset)), type,
        &frameX, &frameY, &d->width, &d->height, &d->top, halfBrick, &d->addFrX, &d->addFrY, &d->effects, &glow, rectangle, &color, nullptr);
    d->frameX = frameX; d->frameY = frameY;
    applyTileDrawData(static_cast<uint16_t>(type), &frameY, &d->width, &d->height, &d->top, &d->addFrY);
    d->frameX = frameX; d->frameY = frameY;
    managedTileDrawData(x,y,type,d,true);
}

void setManagedTilesInstalledHook(TilesInstalledHook hook) {
    g_managedInstalledHook.store(hook, std::memory_order_release);
}

int modTileTypeByName(const std::string& mod, const std::string& name) {
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size(); ++i) {
        if (g_regs[i].def.mod == mod && g_regs[i].def.name == name) {
            return kVanillaTileCount + static_cast<int>(i);
        }
    }
    return -1;
}

bool modObjectShape(int type, ModObjectShape* out) {
    if (type < kVanillaTileCount) return false;
    const size_t i = static_cast<size_t>(type) - kVanillaTileCount;
    if (i >= g_drawShapes.size() || !g_drawShapes[i].ok) return false;
    const DrawShape& s = g_drawShapes[i];
    if (s.cols * s.rows <= 1 || s.fullWidth <= 0 || s.fullHeight <= 0) return false;
    out->cols = s.cols;
    out->rows = s.rows;
    out->coordWidth = s.width;
    out->padding = s.padding;
    out->fullWidth = s.fullWidth;
    out->fullHeight = s.fullHeight;
    out->heights = s.heights;
    return true;
}

std::string modTileKey(int type) {
    std::lock_guard<std::mutex> l(g_mx);
    const size_t i = static_cast<size_t>(type - kVanillaTileCount);
    if (type < kVanillaTileCount || i >= g_regs.size()) return {};
    return g_regs[i].def.mod + "/" + g_regs[i].def.name;
}

int modTileTypeByKey(const std::string& key) {
    const size_t slash = key.find('/');
    if (slash == std::string::npos) return -1;
    return modTileTypeByName(key.substr(0, slash), key.substr(slash + 1));
}

void setModTileAnimation(int type, int frameHeight) {
    const size_t i = static_cast<size_t>(type - kVanillaTileCount);
    if (type < kVanillaTileCount || i >= kMaxAnimated) return;
    g_animHeight[i].store(frameHeight > 0 ? frameHeight : 0, std::memory_order_relaxed);
}

}
