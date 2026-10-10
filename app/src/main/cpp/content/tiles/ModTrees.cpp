#include "content/tiles/ModTrees.h"
#include "content/tiles/ModTiles.h"
#include "content/tiles/TileAccess.h"
#include "content/common/GameRefs.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <atomic>
#include <cstdint>
#include <cstring>
#include <mutex>

namespace bl::runtime {

namespace {

constexpr int kTreeType = 5;            // TileID.Trees
constexpr size_t kMaxGroundTypes = 65536;
constexpr int kMaxTrees = 255;
constexpr int kMaxTrunk = 300;          // altura máxima procurada até o chão

struct Tree {
    uint32_t trunk = 0;   // gchandle do Texture2D do tronco
    int topStyle = -1;    // índice em TextureAssets.TreeTop/TreeBranch
};

// O chão -> 1 + índice em g_trees (0 = árvore do jogo).
std::atomic<uint8_t> g_treeOf[kMaxGroundTypes];
Tree g_trees[kMaxTrees];
std::atomic<int> g_treeCount{0};   // só a thread do jogo escreve; a árvore vale depois do g_treeOf

const Tree* treeOfGround(int ground) {
    if (ground < 0 || static_cast<size_t>(ground) >= kMaxGroundTypes) return nullptr;
    const uint8_t k = g_treeOf[ground].load(std::memory_order_acquire);
    return k ? &g_trees[k - 1] : nullptr;
}

int typeAt(const TileArrays& t, int x, int y) {
    if (x < 0 || y < 0 || x >= t.width || y >= t.height) return -1;
    const uint32_t def = t.lookup[static_cast<int64_t>(t.width) * y + x];
    return (t.sHeader[def] & kTileActiveBit) ? t.type[def] : -1;
}

// O chão debaixo do tronco que passa por (x, y), descendo pelo tronco.
int groundBelow(const TileArrays& t, int x, int y, int* floorY) {
    for (int n = 0; typeAt(t, x, y) == kTreeType && n < kMaxTrunk; ++n) ++y;
    if (floorY) *floorY = y;
    return typeAt(t, x, y);
}

// Galho e raiz ficam ao lado do tronco: a coluna dele (TileDrawing.GetTreeBiome).
int trunkColumn(int x, int frameX, int frameY) {
    if (frameX == 66 && frameY <= 45) return x + 1;
    if (frameX == 88 && frameY >= 66 && frameY <= 110) return x - 1;
    if (frameY >= 198) {
        if (frameX == 44) return x + 1;
        if (frameX == 66) return x - 1;
    } else if (frameY >= 132) {
        if (frameX == 22) return x - 1;
        if (frameX == 44) return x + 1;
    }
    return x;
}

// ---- tronco: GetTileDrawData marca a célula, SpriteBatch.Draw troca a textura ----
//
// O DrawTiles lê a textura do tipo (TextureAssets.Tile[5]) e desenha a célula
// com uma das quatro sobrecargas de SpriteBatch.Draw/Draw_Fast logo depois do
// GetTileDrawData dela, na mesma thread. A marca vale até o próximo
// GetTileDrawData desta thread; só a textura do tronco do jogo é trocada.

thread_local Il2CppObject* t_trunk = nullptr;
FieldInfo* g_tileAssets = nullptr;          // TextureAssets.Tile
std::atomic<int32_t> g_assetValue{-1};      // Asset<Texture2D>.Value (da classe instanciada)

// O tronco do jogo agora (um pacote de texturas pode trocar o Asset).
Il2CppObject* vanillaTrunk() {
    Il2CppArray* assets = nullptr;
    il2cpp::api().field_static_get_value(g_tileAssets, &assets);
    if (!assets || assets->length <= static_cast<uintptr_t>(kTreeType)) return nullptr;
    Il2CppObject* asset = static_cast<Il2CppObject**>(arrayData(assets))[kTreeType];
    if (!asset) return nullptr;
    int32_t offset = g_assetValue.load(std::memory_order_relaxed);
    if (offset < 0) {
        offset = il2cpp::fieldOffset(il2cpp::api().object_get_class(asset), "Value");
        if (offset < 0) return nullptr;
        g_assetValue.store(offset, std::memory_order_relaxed);
    }
    return field<Il2CppObject*>(asset, offset);
}

// O tipo do parâmetro `k` de `mi` termina com `suffix` (exato com `exact`).
bool paramIs(const MethodInfo* mi, int k, const char* text, bool exact) {
    auto& a = il2cpp::api();
    char* name = a.type_get_name(a.method_get_param(mi, k));
    bool ok = false;
    if (name) {
        const size_t n = std::strlen(name), t = std::strlen(text);
        ok = exact ? std::strcmp(name, text) == 0
                   : (n >= t && std::strcmp(name + n - t, text) == 0) || std::strncmp(name, text, t) == 0;
    }
    std::free(name);
    return ok;
}

// Os registradores de argumento passam como vieram (os floats nos d0-d7, sem
// conversão): serve às quatro sobrecargas, que só têm a textura em x1.
using DrawFn = void (*)(intptr_t, intptr_t, intptr_t, intptr_t, intptr_t, intptr_t, intptr_t, intptr_t,
                        double, double, double, double, double, double, double, double);
DrawFn g_origDraw[4] = {};

template <int K>
void hkDraw(intptr_t x0, intptr_t x1, intptr_t x2, intptr_t x3, intptr_t x4, intptr_t x5, intptr_t x6, intptr_t x7,
            double d0, double d1, double d2, double d3, double d4, double d5, double d6, double d7) {
    if (Il2CppObject* trunk = t_trunk) {
        if (reinterpret_cast<Il2CppObject*>(x1) == vanillaTrunk()) x1 = reinterpret_cast<intptr_t>(trunk);
    }
    g_origDraw[K](x0, x1, x2, x3, x4, x5, x6, x7, d0, d1, d2, d3, d4, d5, d6, d7);
}

// ---- o tronco em pé: WorldGen.CheckTree ----
//
// O CheckTree só aceita debaixo do tronco os chãos do jogo (compara com uma
// lista fixa, que vira "2") e quebra o resto. Durante ele, o chão de mod finge
// ser grama; o TileData.SetType reinterna o tile (escrever o tipo direto na
// definição mudaria todos os tiles iguais do mundo).

constexpr int kGrass = 2;
const MethodInfo* g_setType = nullptr;   // TileData.SetType(int tileIndex, ushort newType)

void setTypeAt(const TileArrays& t, int x, int y, int type) {
    using Fn = void (*)(int32_t, uint16_t, const MethodInfo*);
    reinterpret_cast<Fn>(il2cpp::methodPointer(g_setType))(t.width * y + x, static_cast<uint16_t>(type), g_setType);
}

using CheckTreeFn = void (*)(int32_t, int32_t, const MethodInfo*);
CheckTreeFn g_origCheckTree = nullptr;

void hkCheckTree(int32_t i, int32_t j, const MethodInfo* m) {
    TileArrays t;
    const int below = tileArrays(&t) ? typeAt(t, i, j + 1) : -1;
    if (below < 0 || !treeOfGround(below)) return g_origCheckTree(i, j, m);
    setTypeAt(t, i, j + 1, kGrass);
    g_origCheckTree(i, j, m);
    TileArrays after;
    if (tileArrays(&after) && typeAt(after, i, j + 1) == kGrass) setTypeAt(after, i, j + 1, below);
}

// ---- copa e galhos: o estilo da árvore de mod ----

using FoliageFn = bool (*)(int32_t, int32_t, int32_t, int32_t*, int32_t*, int32_t*, int32_t*, int32_t*,
                           const MethodInfo*);
FoliageFn g_origFoliage = nullptr;

bool hkGetCommonTreeFoliageData(int32_t i, int32_t j, int32_t xoffset, int32_t* treeFrame, int32_t* treeStyle,
                                int32_t* floorY, int32_t* topWidth, int32_t* topHeight, const MethodInfo* m) {
    TileArrays t;
    int y = j;
    const Tree* tree = tileArrays(&t) ? treeOfGround(groundBelow(t, i + xoffset, j, &y)) : nullptr;
    if (!tree) return g_origFoliage(i, j, xoffset, treeFrame, treeStyle, floorY, topWidth, topHeight, m);
    *treeStyle = tree->topStyle;
    *floorY = y;
    *topWidth = 80;
    *topHeight = 80;
    return true;
}

bool installHooks() {
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* textures = findClass({"Terraria.GameContent", "TextureAssets", {}});
    g_tileAssets = textures ? findField(textures, "Tile") : nullptr;
    if (!g_tileAssets) {
        BL_ERROR("árvores de mod: TextureAssets.Tile não achado; o tronco sai com a textura do jogo");
    } else {
        // As que o TileDrawing.DrawTiles usa (a textura sempre em x1):
        //   Draw(Texture2D, Vector2, Nullable<Rectangle>, Color, float, Vector2, float, SpriteEffects, float)
        //   Draw(Texture2D, Vector2, ref Rectangle, Color, float, Vector2, float, SpriteEffects, float)
        //   Draw_Fast(Texture2D, ref Vector2, ref Rectangle, ref VertexColors | ref Color, SpriteEffects)
        Il2CppClass* batch = findClass({"Microsoft.Xna.Framework.Graphics", "SpriteBatch", {}});
        const struct { const char* name; uint32_t params; bool (*match)(const MethodInfo*); } draws[] = {
            {"Draw", 9, [](const MethodInfo* mi) { return paramIs(mi, 2, "System.Nullable", false) && paramIs(mi, 3, "Graphics.Color", false) && paramIs(mi, 6, "System.Single", true); }},
            {"Draw", 9, [](const MethodInfo* mi) { return paramIs(mi, 2, "Rectangle&", false) && paramIs(mi, 3, "Graphics.Color", false) && paramIs(mi, 6, "System.Single", true); }},
            {"Draw_Fast", 5, [](const MethodInfo* mi) { return paramIs(mi, 3, "VertexColors&", false); }},
            {"Draw_Fast", 5, [](const MethodInfo* mi) { return paramIs(mi, 3, "Graphics.Color&", false); }},
        };
        DrawFn hooks[4] = {hkDraw<0>, hkDraw<1>, hkDraw<2>, hkDraw<3>};
        for (int k = 0; k < 4; ++k) {
            const MethodInfo* found = nullptr;
            void* it = nullptr;
            while (const MethodInfo* mi = batch ? a.class_get_methods(batch, &it) : nullptr) {
                if (std::strcmp(a.method_get_name(mi), draws[k].name) == 0 &&
                    a.method_get_param_count(mi) == draws[k].params && draws[k].match(mi)) found = mi;
            }
            if (!found || !hook::install(found, hooks[k], &g_origDraw[k])) {
                BL_ERROR("árvores de mod: sem hook no SpriteBatch.%s (%d); parte do tronco sai com a textura do jogo",
                         draws[k].name, k);
            }
        }
    }
    ensureTileDrawHook();

    Il2CppClass* worldGen = findClass({"Terraria", "WorldGen", {}});
    Il2CppClass* data = findClass({"Terraria", "TileData", {}});
    g_setType = data ? a.class_get_method_from_name(data, "SetType", 2) : nullptr;
    const MethodInfo* check = worldGen ? a.class_get_method_from_name(worldGen, "CheckTree", 2) : nullptr;
    if (!g_setType || !check || !hook::install(check, hkCheckTree, &g_origCheckTree)) {
        BL_ERROR("árvores de mod: sem hook no WorldGen.CheckTree; a árvore de mod se quebra ao crescer");
    }
    const MethodInfo* foliage = worldGen ? a.class_get_method_from_name(worldGen, "GetCommonTreeFoliageData", 8) : nullptr;
    if (!foliage || !hook::install(foliage, hkGetCommonTreeFoliageData, &g_origFoliage)) {
        BL_ERROR("árvores de mod: sem hook no WorldGen.GetCommonTreeFoliageData; copa e galhos saem como os da floresta");
    }
    return true;
}

} // namespace

void noteTreeDrawTile(int x, int y, int type, int frameX, int frameY) {
    if (type != kTreeType || g_treeCount.load(std::memory_order_relaxed) == 0) {
        t_trunk = nullptr;
        return;
    }
    TileArrays t;
    const Tree* tree = tileArrays(&t) ? treeOfGround(groundBelow(t, trunkColumn(x, frameX, frameY), y, nullptr)) : nullptr;
    t_trunk = tree ? il2cpp::api().gchandle_get_target(tree->trunk) : nullptr;
}

void setModTree(int groundType, Il2CppObject* trunk, int topStyle) {
    if (groundType < 0 || static_cast<size_t>(groundType) >= kMaxGroundTypes || !trunk) return;
    const int count = g_treeCount.load(std::memory_order_relaxed);
    if (count >= kMaxTrees) {
        BL_ERROR("árvores de mod: mais de %d árvores; a de chão %d ficou de fora", kMaxTrees, groundType);
        return;
    }
    static std::once_flag once;
    std::call_once(once, installHooks);

    Tree& tree = g_trees[count];
    tree.trunk = il2cpp::api().gchandle_new(trunk, false);
    tree.topStyle = topStyle;
    g_treeCount.store(count + 1, std::memory_order_release);
    g_treeOf[groundType].store(static_cast<uint8_t>(count + 1), std::memory_order_release);
}

} // namespace bl::runtime
