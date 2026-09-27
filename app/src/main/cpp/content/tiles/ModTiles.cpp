#include "content/tiles/ModTiles.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "hook/CodePatch.h"
#include "content/common/ContentAssets.h"
#include "content/common/GameRefs.h"
#include "content/common/TypeTables.h"

#include <atomic>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <time.h>

namespace bl::runtime {

namespace {

struct Entry {
    ModTileDef def;
    uint32_t asset = 0;   // gchandle do Asset<Texture2D>: reaplicado se a tabela for refeita
    int mapColor = -1;    // 0xRRGGBB do AddMapEntry; -1 = fora do mapa
};

std::mutex g_mx;
std::vector<Entry> g_regs;   // indice = type - kVanillaTileCount
std::atomic<TilesInstalledHook> g_installedHook{nullptr};
std::atomic<int> g_total{0};
std::atomic<int> g_installed{0};
// O SetStaticDefaults do lote ja rodou (ele espera o TileObjectData existir).
std::atomic<bool> g_staticDone{false};
bool g_failed = false;
// Sobe quando o mundo muda: o que o cache do desenho guardou deixa de valer.
std::atomic<uint32_t> g_drawEpoch{1};

// Conferida contra toda alocacao de 753 posicoes na libil2cpp (`mov #0x2f1`):
// as estaticas estao em Main (a maioria), TileID.Sets (e as aninhadas),
// TextureAssets (Tile e HighlightMask), WorldGen, MapHelper, Recipe e
// TileMaterials; as por instancia, em Player, GUICrafting e SceneMetrics
// (abaixo). As da geracao de mundo (Skyblock, SecretSeed) ficam de fora: tile
// de mod nao entra na geracao.
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
    // As do tModLoader (TileLoader.ResizeArrays) que a varredura do `mov
    // #753` nao pega: nascem pela fabrica do TileID.Sets.
    {"Terraria", "WorldGen", "Skyblock"},
    {"Terraria.GameContent.Biomes", "CorruptionPitBiome", ""},
    {"Terraria.GameContent.Biomes.CaveHouse", "HouseUtils", ""},
}, TypeTables::Fill::Mode);

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* textures = nullptr;     // TextureAssets.Tile
    FieldInfo* solid = nullptr;        // Main.tileSolid (so para saber se o jogo ja criou)
    FieldInfo* merge = nullptr;        // Main.tileMerge (bool[][])
    FieldInfo* players = nullptr;      // Main.player
    FieldInfo* guiActive = nullptr;    // GUIInstance.Active
    int32_t guiCrafting = -1;          // GUIInstance.GUICrafting
    int32_t adjTile = -1;              // Player.adjTile
    int32_t oldAdjTile = -1;           // GUICrafting.oldAdjTile
    int32_t tileCounts = -1;           // SceneMetrics._tileCounts
    const MethodInfo* playerCtor = nullptr;
    const MethodInfo* craftingCtor = nullptr;
    const MethodInfo* metricsCtor = nullptr;
    const MethodInfo* setupMerge = nullptr;           // Main.SetupTileMerge
    const MethodInfo* sceneMetrics = nullptr;         // Main.get_SceneMetrics
    const MethodInfo* playerSceneMetrics = nullptr;   // Main.get_PlayerSceneMetrics
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

// ---- Main.tileMerge: bool[753][753] ----
//
// A TypeTables cresce a tabela de FORA e enche o que falta com o [0] — a MESMA
// linha da terra em todo tipo novo, e curta: `tileMerge[mod][mod] = true`
// escreveria alem do fim dela. Cada linha vira um array proprio de `total`.

bool fixTileMerge(int total) {
    const Refs& r = refs();
    Il2CppArray* outer = readStatic(r.merge);
    if (!outer || outer->length < static_cast<uintptr_t>(total)) return false;
    auto** rows = static_cast<Il2CppArray**>(arrayData(outer));
    // Ja feito: a ultima linha e propria e tem o tamanho certo.
    Il2CppArray* last = rows[total - 1];
    if (last && last != rows[0] && last->length >= static_cast<uintptr_t>(total) &&
        rows[0] && rows[0]->length >= static_cast<uintptr_t>(total)) {
        return true;
    }
    static Il2CppClass* boolCls = nullptr;
    if (!boolCls) boolCls = il2cpp::findClass({"System", "Boolean", {}});
    if (!boolCls) return false;
    auto& a = il2cpp::api();
    // A linha da terra ANTES de trocar: os tipos novos apontam para ela.
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

// ---- Main.SetupTileMerge ----
//
// O jogo refaz o tileMerge INTEIRO (new bool[753][]) quando chega ao menu —
// depois da instalacao, e de novo em outros momentos. O que os mods
// escreveram (tileMerge[ore][ore] = true) sumiria: o hook guarda as linhas e
// colunas dos tipos de mod, deixa o jogo refazer, aumenta e devolve.

using SetupMergeFn = void (*)(const MethodInfo*);
SetupMergeFn g_origSetupMerge = nullptr;

void hkSetupTileMerge(const MethodInfo* m) {
    const int total = tileTypeCount();
    const Refs& r = refs();
    std::vector<uint8_t> rows, cols;   // [t][*] e [*][t] dos tipos de mod
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

/** Tabela que o jogo refez: reaplica o nosso. */
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

// ---- limites compilados no codigo ----

struct LimitPatch {
    const char* ns;
    const char* cls;
    const char* method;
    uint32_t vanilla;   // o limite como esta no codigo
    int offset;         // novo limite = total + offset
    bool belowToo;      // tambem `tipo < limite` / `tipo >= limite`
    bool shifted = false;   // `cmp #limite, lsl #12`: a chave tipo << 12
};

// Achados varrendo a libil2cpp por `cmp #751..#753` + desvio de ordem
// (tools/disasm/scan_limits.py), e conferidos um a um: todos leem o tipo do
// tile (TileType[TileLookup[..]]) ou o `Type` do PlaceTile, e nenhum e a
// guarda de uma tabela de desvio (switch). Fica de fora o Main.DrawProjDirect
// (752 ali e tipo de PROJETIL) e a geracao de mundo.
constexpr LimitPatch kLimitPatches[] = {
    // `if (Type > 752) return false` / tile com tipo alto desviado.
    {"Terraria", "WorldGen", "PlaceTile", kVanillaTileCount - 1, -1, false},
    // O `WorldGen.PlaceObject(x, y, tipo)` dos moveis (TileObjectData).
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
    // `tipo < 753` / `tipo >= 753`: o quadro cosmetico, e a validacao do mundo
    // (que APAGA tile com tipo >= 753).
    {"Terraria", "WorldGen", "TileFrameCosmetic", kVanillaTileCount, 0, true},
    {"Terraria", "WorldGen", "ValidateTypes", kVanillaTileCount, 0, true},
    {"Terraria", "Collision", "IsWorldPointSolid", kVanillaTileCount, 0, false},
    // Os tiles internados: a chave (tipo << 12 | ...) acima de 753 << 12 e
    // "Out of bounds" (o jogo loga e segue). O buffer e o do hook no Allocate.
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

// ---- TileData.TileLists: as listas de definicao por chave ----
//
// O mundo do celular guarda os tiles internados: GetTileDefinition acha a
// definicao igual pela chave (tipo << 12 | bHeader2 << 4 | quadro), numa
// tabela de listas com 753 * 4096 entradas DENTRO do bloco que o Allocate monta
// ao carregar o mundo. Tile de mod teria a chave alem do fim — lida e ESCRITA.
// Depois de cada Allocate, a tabela passa a ser nossa, com espaco para todos
// os tipos (-1 = lista vazia, como o jogo inicia).

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

// ---- Main.tileGlowMask ----
//
// -1 = sem brilho (716 dos 753 tipos do jogo). A tabela pode crescer antes de
// o jogo encher os -1 — o tipo novo ficaria com 0, e desenharia o brilho 0 por
// cima do tile. Como o tModLoader: -1 nos tipos de mod, em toda tabela nova
// (o mod que quiser brilho escreve depois, no SetStaticDefaults).

bool fixGlowMask(int total) {
    static FieldInfo* f = [] {
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        return main ? il2cpp::findField(main, "tileGlowMask") : nullptr;
    }();
    static Il2CppArray* fixedFor = nullptr;
    Il2CppArray* glow = f ? readStatic(f) : nullptr;
    if (!glow || glow == fixedFor || glow->length < static_cast<uintptr_t>(total)) return false;
    auto* v = static_cast<int16_t*>(arrayData(glow));
    // Tabela ainda sem os -1 do jogo: espera ele encher.
    if (v[0] != -1 && v[1] != -1) return false;
    for (int t = kVanillaTileCount; t < total; ++t) v[t] = -1;
    fixedFor = glow;
    return true;
}

// ---- mapa: a cor do jogo mais proxima ----

struct MapRefs {
    bool tried = false, ok = false;
    FieldInfo *lookup = nullptr, *options = nullptr, *colors = nullptr, *wallPosition = nullptr;
};

MapRefs& mapRefs() {
    static MapRefs m;
    if (m.tried) return m;
    m.tried = true;
    Il2CppClass* map = il2cpp::findClass({"Terraria.Map", "MapHelper", {}});
    m.lookup = map ? il2cpp::findField(map, "tileLookup") : nullptr;
    m.options = map ? il2cpp::findField(map, "tileOptionCounts") : nullptr;
    m.colors = map ? il2cpp::findField(map, "colorLookup") : nullptr;
    m.wallPosition = map ? il2cpp::findField(map, "wallPosition") : nullptr;
    m.ok = m.lookup && m.options && m.colors && m.wallPosition;
    if (!m.ok) BL_ERROR("tiles de mod: MapHelper sem as tabelas de cor; tile de mod fora do mapa");
    return m;
}

std::atomic<bool> g_mapDirty{true};

/** Aponta cada tile de mod para a cor do jogo mais proxima. Refaz se o jogo refizer as tabelas. */
void applyMapColors(int total) {
    static Il2CppArray* doneFor = nullptr;
    const MapRefs& m = mapRefs();
    if (!m.ok) return;
    Il2CppArray* lookup = readStatic(m.lookup);
    Il2CppArray* options = readStatic(m.options);
    Il2CppArray* colors = readStatic(m.colors);
    uint16_t wallPosition = 0;
    il2cpp::api().field_static_get_value(m.wallPosition, &wallPosition);
    if (!lookup || !options || !colors || wallPosition <= 1 ||
        lookup->length < static_cast<uintptr_t>(total) || options->length < static_cast<uintptr_t>(total) ||
        colors->length < wallPosition) {
        static int waited = 0;
        if (++waited == 600) {
            BL_DEBUG("tiles de mod: mapa ainda sem tabelas (lookup %d, opcoes %d, cores %d, paredes em %d)",
                     lookup ? static_cast<int>(lookup->length) : -1, options ? static_cast<int>(options->length) : -1,
                     colors ? static_cast<int>(colors->length) : -1, wallPosition);
        }
        return;
    }
    if (lookup == doneFor && !g_mapDirty.exchange(false)) return;
    auto* lk = static_cast<uint16_t*>(arrayData(lookup));
    auto* op = static_cast<int32_t*>(arrayData(options));
    // O Color deste jogo fica na memoria como A, B, G, R (o packedValue ao contrario).
    const auto* col = static_cast<const uint8_t*>(arrayData(colors));
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size() && static_cast<int>(i) < total - kVanillaTileCount; ++i) {
        const int type = kVanillaTileCount + static_cast<int>(i);
        const int want = g_regs[i].mapColor;
        if (want < 0) {
            lk[type] = 0;
            op[type] = 0;
            continue;
        }
        const int r = (want >> 16) & 255, g = (want >> 8) & 255, b = want & 255;
        int best = 1, bestDist = 1 << 30;
        // As cores de tile ficam em [1, wallPosition): as de parede vem depois.
        for (int c = 1; c < wallPosition; ++c) {
            const uint8_t* p = col + static_cast<size_t>(c) * 4;
            const int dr = p[3] - r, dg = p[2] - g, db = p[1] - b;
            const int d = dr * dr + dg * dg + db * db;
            if (d < bestDist) { bestDist = d; best = c; }
        }
        lk[type] = static_cast<uint16_t>(best);
        op[type] = 1;
    }
    doneFor = lookup;
    BL_DEBUG("tiles de mod: cores de mapa aplicadas (%zu tile(s))", g_regs.size());
}

// O MapHelper.Initialize refaz tileLookup/tileOptionCounts/colorLookup com o
// tamanho de fabrica ao entrar no mundo: aumenta e aplica as cores na hora, e
// nao 2 s depois pela vigia (o mapa leria alem do fim nesse meio tempo).
using MapInitFn = void (*)(const MethodInfo*);
MapInitFn g_origMapInit = nullptr;

void hkMapInitialize(const MethodInfo* m) {
    g_origMapInit(m);
    g_drawEpoch.fetch_add(1, std::memory_order_relaxed);   // outro mundo: o cache do desenho recomeca
    const int total = tileTypeCount();
    if (total <= kVanillaTileCount) return;
    g_tables.watch(total, onTableRegrown);
    g_mapDirty.store(true);
    applyMapColors(total);
}

void hookMapInitialize() {
    Il2CppClass* map = il2cpp::findClass({"Terraria.Map", "MapHelper", {}});
    const MethodInfo* init = map ? il2cpp::api().class_get_method_from_name(map, "Initialize", 0) : nullptr;
    if (!init || !hook::install(init, hkMapInitialize, &g_origMapInit)) {
        BL_ERROR("tiles de mod: sem hook no MapHelper.Initialize; o mapa pode ler alem do fim ao entrar no mundo");
    }
}

// ---- TileObjectData ----
//
// _data: uma List com uma entrada por tipo (a forma de moveis, portas...). O
// GetTileData(tipo) le _data[tipo]: com tipo alem do fim, a List lanca. Como no
// tModLoader, entradas nulas ate o total (bloco 1x1 nao tem forma), e o
// `TileObjectData.addTile(Type)` do SetStaticDefaults poe a do tile de mod.
//
// O jogo tranca tudo no fim do TileObjectData.Initialize (readOnlyData): dali
// em diante, todo setter do newTile lanca "Tile data is locked". O tModLoader
// tira a trava; aqui ela sai so enquanto o SetStaticDefaults dos tiles roda.

struct ObjectDataRefs {
    bool tried = false;
    FieldInfo* data = nullptr;       // _data
    FieldInfo* readOnly = nullptr;   // readOnlyData
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

// A forma que cada tile de mod registrou (gchandle; 0 = 1x1). Se o jogo refizer
// a List, ela volta.
std::vector<uint32_t> g_objectData;

/** true quando a List ja tem o total (ou nao ha o que fazer). A cada quadro ate la. */
bool padTileObjectData(int total) {
    auto& a = il2cpp::api();
    if (!objectDataRefs().data) return true;
    Il2CppObject* list = objectDataList();
    if (!list) return false;   // o jogo cria no TileObjectData.Initialize
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
        // List refeita depois do SetStaticDefaults: as formas dos mods voltam.
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

/** Destrava (false) ou trava (true) o TileObjectData; devolve como estava. */
bool setObjectDataLocked(bool locked) {
    FieldInfo* f = objectDataRefs().readOnly;
    if (!f) return false;
    auto& a = il2cpp::api();
    bool was = false;
    a.field_static_get_value(f, &was);
    a.field_static_set_value(f, &locked);
    return was;
}

// ---- desenho dos objetos: TileDrawing.GetTileDrawData ----
//
// O jogo decide a altura de cada celula por tipo (um switch com os moveis dele:
// `tileHeight = 18`...); tile de mod ficaria sempre com 16, e a ultima linha da
// pia (18 de altura na textura) sairia cortada. Como o SetDrawPositions do
// tModLoader: com TileObjectData, a largura, o deslocamento e a altura da
// linha vem dele. Nativo: roda para toda celula de mod na tela, todo quadro.

struct DrawShape {
    bool ok = false;
    int width = 16, yOffset = 0, padding = 2, rows = 1, fullHeight = 18;
    std::vector<int> heights;
};

// Indice = tipo - kVanillaTileCount. Escrito uma vez na instalacao, antes do hook.
std::vector<DrawShape> g_drawShapes;

// AnimationFrameHeight por tipo de mod (indice = tipo - kVanillaTileCount).
// Fixo e atomico: as threads do desenho leem enquanto o JS escreve.
constexpr size_t kMaxAnimated = 8192;
std::atomic<int> g_animHeight[kMaxAnimated];
FieldInfo* g_tileFrameField = nullptr;   // Main.tileFrame
std::atomic<DrawDataHook> g_drawDataHook{nullptr};
std::atomic<const std::atomic<uint8_t>*> g_drawDataMarks{nullptr};
std::atomic<const std::atomic<uint8_t>*> g_drawDataUncached{nullptr};   // CacheDrawData = false

using DrawDataFn = void (*)(Il2CppObject*, int32_t, int32_t, void*, uint16_t, int16_t*, int16_t*, int32_t*,
                            int32_t*, int32_t*, int32_t*, int32_t*, int32_t*, int32_t*, Il2CppObject**, void*,
                            void*, const MethodInfo*);
DrawDataFn g_origDrawData = nullptr;

// ---- cache do SetDrawPositions & cia. ----
//
// Cada ida ao JS no desenho espera o motor, que a thread do jogo segura: ~30 a
// 50 us por celula no MuMu, contra ~5 us do proprio metodo do mod. O jogo
// redesenha a mesma celula varias vezes, igual: o resultado fica guardado, com
// tudo o que o mod recebe na chave (posicao, tipo, o `d` de entrada e o
// Main.tileFrame do tipo, que o AnimateIndividualTile costuma ler). O mundo em
// volta nao esta na chave (a tocha olha o bloco de cima): quando o jogo
// reenquadra tiles, a epoca daquela area (16x16 tiles) sobe, e a entrada vale
// no maximo 5 s. Uma epoca so para o mundo todo nao serve: a grama e as
// plantas crescendo reenquadram algum tile quase todo quadro.

struct DrawKey {
    int32_t x = 0, y = 0, type = 0, tileFrame = 0;
    TileDrawData in;
};
static_assert(sizeof(DrawKey) == 12 * sizeof(int32_t), "DrawKey sem preenchimento (comparada com memcmp)");

struct DrawEpoch {
    uint32_t world = 0, area = 0;
    bool operator==(const DrawEpoch& o) const { return world == o.world && area == o.area; }
};

struct DrawSlot {
    std::atomic_flag busy = ATOMIC_FLAG_INIT;
    bool used = false;
    DrawEpoch epoch;
    int64_t stamp = 0;
    DrawKey key;
    TileDrawData out;
};

constexpr size_t kDrawCacheSlots = 4096;   // potencia de 2
constexpr int64_t kDrawCacheTtlNs = 5000000000LL;
DrawSlot g_drawCache[kDrawCacheSlots];

// A epoca de cada area de 16x16 tiles, por espalhamento (duas areas na mesma
// posicao so invalidam uma a mais).
constexpr int kAreaShift = 4;
constexpr size_t kAreaEpochs = 65536;   // potencia de 2
std::atomic<uint32_t> g_areaEpoch[kAreaEpochs];

std::atomic<uint32_t>& areaEpochOf(int32_t areaX, int32_t areaY) {
    uint32_t h = static_cast<uint32_t>(areaX) * 73856093u ^ static_cast<uint32_t>(areaY) * 19349663u;
    h ^= h >> 16;
    return g_areaEpoch[h & (kAreaEpochs - 1)];
}

DrawEpoch drawEpochAt(int32_t x, int32_t y) {
    return {g_drawEpoch.load(std::memory_order_relaxed),
            areaEpochOf(x >> kAreaShift, y >> kAreaShift).load(std::memory_order_relaxed)};
}

/** Os tiles de [x0, x1] x [y0, y1] mudaram; area grande demais vira o mundo todo. */
void touchDrawArea(int32_t x0, int32_t y0, int32_t x1, int32_t y1) {
    const int32_t ax0 = x0 >> kAreaShift, ax1 = x1 >> kAreaShift;
    const int32_t ay0 = y0 >> kAreaShift, ay1 = y1 >> kAreaShift;
    if (ax1 < ax0 || ay1 < ay0) return;
    if (static_cast<int64_t>(ax1 - ax0 + 1) * (ay1 - ay0 + 1) > 1024) {
        g_drawEpoch.fetch_add(1, std::memory_order_relaxed);
        return;
    }
    for (int32_t ax = ax0; ax <= ax1; ++ax) {
        for (int32_t ay = ay0; ay <= ay1; ++ay) areaEpochOf(ax, ay).fetch_add(1, std::memory_order_relaxed);
    }
}

int64_t coarseNowNs() {
    timespec ts;
    clock_gettime(CLOCK_MONOTONIC_COARSE, &ts);
    return static_cast<int64_t>(ts.tv_sec) * 1000000000LL + ts.tv_nsec;
}

DrawSlot& drawSlotOf(const DrawKey& k) {
    uint32_t h = static_cast<uint32_t>(k.x) * 73856093u ^ static_cast<uint32_t>(k.y) * 19349663u ^
                 static_cast<uint32_t>(k.type) * 83492791u ^ static_cast<uint32_t>(k.tileFrame) * 2654435761u ^
                 static_cast<uint32_t>(k.in.frameX) * 40503u ^ static_cast<uint32_t>(k.in.frameY) * 2246822519u;
    h ^= h >> 15;
    return g_drawCache[h & (kDrawCacheSlots - 1)];
}

/** Outra thread mexendo na mesma posicao: conta como falta (vai ao JS). */
bool drawCacheGet(const DrawKey& k, DrawEpoch epoch, int64_t now, TileDrawData* out) {
    DrawSlot& s = drawSlotOf(k);
    if (s.busy.test_and_set(std::memory_order_acquire)) return false;
    const bool hit = s.used && s.epoch == epoch && now - s.stamp < kDrawCacheTtlNs &&
                     std::memcmp(&s.key, &k, sizeof(DrawKey)) == 0;
    if (hit) *out = s.out;
    s.busy.clear(std::memory_order_release);
    return hit;
}

void drawCachePut(const DrawKey& k, DrawEpoch epoch, int64_t now, const TileDrawData& out) {
    DrawSlot& s = drawSlotOf(k);
    if (s.busy.test_and_set(std::memory_order_acquire)) return;
    s.key = k;
    s.out = out;
    s.epoch = epoch;
    s.stamp = now;
    s.used = true;
    s.busy.clear(std::memory_order_release);
}

void invalidateDrawCache() {
    g_drawEpoch.fetch_add(1, std::memory_order_relaxed);
}

// Um bloco mudou (colocado, quebrado, enquadrado, vindo da rede): o vizinho de
// mod pode desenhar diferente.
using SquareFrameFn = void (*)(int32_t, int32_t, bool, const MethodInfo*);
using RangeFrameFn = void (*)(int32_t, int32_t, int32_t, int32_t, const MethodInfo*);
SquareFrameFn g_origSquareFrame = nullptr;
RangeFrameFn g_origRangeFrame = nullptr;

// O SquareTileFrame enquadra o 3x3 em volta; mais 1 de folga (a tocha olha o
// bloco de cima dela, que pode estar fora do 3x3 de quem mudou).
void hkSquareTileFrame(int32_t i, int32_t j, bool resetFrame, const MethodInfo* m) {
    g_origSquareFrame(i, j, resetFrame, m);
    touchDrawArea(i - 2, j - 2, i + 2, j + 2);
}

void hkRangeFrame(int32_t x0, int32_t y0, int32_t x1, int32_t y1, const MethodInfo* m) {
    g_origRangeFrame(x0, y0, x1, y1, m);
    touchDrawArea(x0 - 2, y0 - 2, x1 + 2, y1 + 2);
}

void hkGetTileDrawData(Il2CppObject* self, int32_t x, int32_t y, void* info, uint16_t type, int16_t* frameX,
                       int16_t* frameY, int32_t* width, int32_t* height, int32_t* top, int32_t* halfBrick,
                       int32_t* addFrX, int32_t* addFrY, int32_t* effects, Il2CppObject** glow, void* glowRect,
                       void* glowColor, const MethodInfo* m) {
    g_origDrawData(self, x, y, info, type, frameX, frameY, width, height, top, halfBrick, addFrX, addFrY, effects,
                   glow, glowRect, glowColor, m);
    if (type < kVanillaTileCount) return;
    const size_t i = static_cast<size_t>(type) - kVanillaTileCount;

    const DrawDataHook hook = g_drawDataHook.load(std::memory_order_acquire);
    const std::atomic<uint8_t>* marks = g_drawDataMarks.load(std::memory_order_acquire);
    const bool toMod = hook && marks && marks[type].load(std::memory_order_relaxed);

    // O AnimationFrameHeight do tModLoader: o quadro do tipo (Main.tileFrame,
    // que o AnimateTile avanca) desce a textura. Tambem vai na chave do cache.
    const int frameHeight = i < kMaxAnimated ? g_animHeight[i].load(std::memory_order_relaxed) : 0;
    int32_t tileFrame = 0;
    if ((frameHeight > 0 || toMod) && g_tileFrameField) {
        Il2CppArray* frames = readStatic(g_tileFrameField);
        if (frames && type < frames->length) tileFrame = static_cast<const int32_t*>(arrayData(frames))[type];
        *addFrY += tileFrame * frameHeight;
    }

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

    // O SetDrawPositions & cia. do mod, so para os tipos que os sobrescrevem.
    if (!toMod) return;
    DrawKey key;
    key.x = x;
    key.y = y;
    key.type = type;
    key.tileFrame = tileFrame;
    key.in.frameX = *frameX;
    key.in.frameY = *frameY;
    key.in.width = *width;
    key.in.height = *height;
    key.in.top = *top;
    key.in.addFrX = *addFrX;
    key.in.addFrY = *addFrY;
    key.in.effects = *effects;
    // A epoca de antes do JS: se o mundo mudar enquanto ele roda, a entrada ja nasce velha.
    const DrawEpoch epoch = drawEpochAt(x, y);
    const int64_t now = coarseNowNs();
    const std::atomic<uint8_t>* uncached = g_drawDataUncached.load(std::memory_order_acquire);
    const bool cached = !uncached || !uncached[type].load(std::memory_order_relaxed);
    TileDrawData d;
    if (!cached || !drawCacheGet(key, epoch, now, &d)) {
        d = key.in;
        if (hook(x, y, type, &d) && cached) drawCachePut(key, epoch, now, d);
    }
    *frameX = static_cast<int16_t>(d.frameX);
    *frameY = static_cast<int16_t>(d.frameY);
    *width = d.width;
    *height = d.height;
    *top = d.top;
    *addFrX = d.addFrX;
    *addFrY = d.addFrY;
    *effects = d.effects;
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
    const MethodInfo* get = a.class_get_method_from_name(a.object_get_class(data), "get_CoordinateHeights", 0);
    Il2CppObject* exc = nullptr;
    auto* heights = get ? reinterpret_cast<Il2CppArray*>(a.runtime_invoke(get, data, nullptr, &exc)) : nullptr;
    if (!ok || exc || !heights || heights->length == 0 || s.rows <= 0) return {};
    const auto* h = static_cast<const int32_t*>(arrayData(heights));
    s.heights.assign(h, h + heights->length);
    // Menos alturas que linhas (o jogo tolera): a ultima vale para o resto.
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
    void* it = nullptr;
    // Tres sobrecargas: a de TileDrawInfo e a do DrawTiles (as outras sao de grama e vinhas).
    while (const MethodInfo* m = cls ? a.class_get_methods(cls, &it) : nullptr) {
        if (std::strcmp(a.method_get_name(m), "GetTileDrawData") != 0 || a.method_get_param_count(m) != 16) continue;
        char* name = a.type_get_name(a.method_get_param(m, 2));
        const bool drawInfo = name && std::strstr(name, "TileDrawInfo");
        std::free(name);
        if (drawInfo) target = m;
    }
    if (!target || !hook::install(target, hkGetTileDrawData, &g_origDrawData)) {
        BL_ERROR("tiles de mod: sem hook no TileDrawing.GetTileDrawData; a ultima linha de um movel de mod sai cortada");
        return;
    }

    Il2CppClass* worldGen = il2cpp::findClass({"Terraria", "WorldGen", {}});
    const MethodInfo* square = worldGen ? a.class_get_method_from_name(worldGen, "SquareTileFrame", 3) : nullptr;
    const MethodInfo* range = worldGen ? a.class_get_method_from_name(worldGen, "RangeFrame", 4) : nullptr;
    if (!square || !range || !hook::install(square, hkSquareTileFrame, &g_origSquareFrame) ||
        !hook::install(range, hkRangeFrame, &g_origRangeFrame)) {
        BL_ERROR("tiles de mod: sem hook no WorldGen.SquareTileFrame/RangeFrame; o desenho de tile de mod "
                 "pode levar ate 1 s para ver um bloco vizinho mudar");
    }
}

/** Guarda a forma (_data[tipo]) de cada tile de [first, last] depois do SetStaticDefaults. */
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
    // O hook serve tambem a animacao (AnimationFrameHeight) de qualquer tile de mod.
    hookDrawData();
    if (shaped) {
        BL_DEBUG("tiles de mod: %d tile(s) de mod com TileObjectData (moveis/objetos)", shaped);
    }
}

/**
 * O SetStaticDefaults do lote [first, last]. Espera o TileObjectData.Initialize
 * (o newTile, o Style2x2 e o _data so existem depois dele), mas as tabelas
 * crescem ANTES, como sempre: crescidas depois de o jogo preenche-las, o "mais
 * comum" dos tipos novos seria o valor dos moveis do jogo (tileFrameImportant
 * vira true, e o bloco de mod nunca e enquadrado).
 */
void runStaticDefaults(int first, int last) {
    if (hasObjectDataField() && !objectDataList()) return;   // a proxima volta tenta de novo
    padTileObjectData(last + 1);
    if (TilesInstalledHook hook = g_installedHook.load(std::memory_order_acquire)) {
        const bool wasLocked = setObjectDataLocked(false);
        hook(first, last);
        setObjectDataLocked(wasLocked);
    }
    keepObjectData(first, last);
    g_staticDone.store(true, std::memory_order_release);
}

// ---- tabelas por instancia ----
//
// Player.adjTile (as estacoes de criacao por perto), GUICrafting.oldAdjTile
// (a copia do menu de criacao) e SceneMetrics._tileCounts (a contagem dos
// biomas): o construtor cria com 753. Os que ja existem crescem na instalacao;
// os que nascerem depois, no construtor.

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
    for (const MethodInfo* get : {r.sceneMetrics, r.playerSceneMetrics}) {
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

} // namespace

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
        applyMapColors(from);
        static bool padded = false;
        static int frame = 0;
        ++frame;
        if (!padded || frame % 120 == 0) padded = padTileObjectData(from);
        if (frame % 120 == 0) g_tables.watch(from, onTableRegrown);
        return;
    }
    if (!gameReady(from)) return;
    // Os limites compilados valem para UM total: os mods registram antes da
    // tela de titulo, entao na pratica ha um lote so.
    if (installed > 0) {
        BL_ERROR("tiles de mod: %d registrado(s) depois da instalacao; ficam sem tabela", total - installed);
        g_failed = true;
        return;
    }

    const int to = kVanillaTileCount + total;
    // O tileMerge nasce no menu, as vezes depois daqui: sem ele o
    // SetStaticDefaults nao teria onde escrever. O hook primeiro (ele aumenta
    // o que o jogo refizer), e o jogo monta agora se ainda nao montou.
    if (!hook::install(refs().setupMerge, hkSetupTileMerge, &g_origSetupMerge)) {
        BL_ERROR("tiles de mod: sem hook no Main.SetupTileMerge; o tileMerge dos mods se perde quando o jogo o refizer");
    }
    hookAllocate();
    hookMapInitialize();
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
    return g_failed || (g_installed.load(std::memory_order_relaxed) == g_total.load(std::memory_order_acquire) &&
                        g_staticDone.load(std::memory_order_acquire));
}

void setTilesInstalledHook(TilesInstalledHook hook) {
    g_installedHook.store(hook, std::memory_order_release);
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

void setModTileMapColor(int type, int r, int g, int b) {
    std::lock_guard<std::mutex> l(g_mx);
    const size_t i = static_cast<size_t>(type - kVanillaTileCount);
    if (type < kVanillaTileCount || i >= g_regs.size()) return;
    g_regs[i].mapColor = ((r & 255) << 16) | ((g & 255) << 8) | (b & 255);
    g_mapDirty.store(true);
}

void setModTileAnimation(int type, int frameHeight) {
    const size_t i = static_cast<size_t>(type - kVanillaTileCount);
    if (type < kVanillaTileCount || i >= kMaxAnimated) return;
    g_animHeight[i].store(frameHeight > 0 ? frameHeight : 0, std::memory_order_relaxed);
}

void setDrawDataHook(DrawDataHook hook, const std::atomic<uint8_t>* marks, const std::atomic<uint8_t>* uncached) {
    g_drawDataUncached.store(uncached, std::memory_order_release);
    g_drawDataMarks.store(marks, std::memory_order_release);
    g_drawDataHook.store(hook, std::memory_order_release);
    invalidateDrawCache();
}

std::vector<ModTileInfo> modTiles() {
    std::lock_guard<std::mutex> l(g_mx);
    std::vector<ModTileInfo> out;
    for (size_t i = 0; i < g_regs.size(); ++i) {
        const ModTileDef& d = g_regs[i].def;
        out.push_back({kVanillaTileCount + static_cast<int>(i), d.mod, d.name, d.texture});
    }
    return out;
}

} // namespace bl::runtime
