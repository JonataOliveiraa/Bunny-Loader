#include "content/projectiles/ProjectileDrawSplit.h"

#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <cstdint>
#include <mutex>

namespace bl::runtime {

namespace {

struct Vec2 {
    float x, y;
};

struct NullableRect {
    uint8_t has;
    int32_t x, y, w, h;
};

using BatchFloatFn = void (*)(void*, void*, Vec2, NullableRect, uint32_t, float, Vec2, float, int32_t, float, const MethodInfo*);
using BatchVectorFn = void (*)(void*, void*, Vec2, NullableRect, uint32_t, float, Vec2, Vec2, int32_t, float, const MethodInfo*);
using BatchRefFloatFn = void (*)(void*, void*, Vec2*, void*, uint32_t*, float, Vec2, float, int32_t, const MethodInfo*);
using BatchRefVectorFn = void (*)(void*, void*, Vec2*, void*, uint32_t*, float, Vec2, Vec2, int32_t, const MethodInfo*);
using DrawDataFn = void (*)(void*, void*, const MethodInfo*);

struct Split {
    void* own;
    bool extras, sprite, reached;
};

constexpr int kDepth = 8;
thread_local Split t_splits[kDepth];
thread_local int t_depth = 0;

struct Adjust {
    void* texture;
    int32_t kind;
    DrawAdjustFn fn;
};

thread_local Adjust t_adjusts[kDepth];
thread_local int t_adjustDepth = 0;

void adjust(void* texture, Vec2* position, int32_t* effects) {
    const Adjust& a = t_adjusts[t_adjustDepth - 1];
    if (texture == a.texture && a.fn) a.fn(a.kind, &position->x, effects);
}

BatchFloatFn g_batchFloat = nullptr;
BatchVectorFn g_batchVector = nullptr;
BatchRefFloatFn g_batchRefFloat = nullptr;
BatchRefVectorFn g_batchRefVector = nullptr;
DrawDataFn g_drawData = nullptr;
std::mutex g_mx;
int g_state = 0;

bool pass(void* texture) {
    Split& split = t_splits[t_depth - 1];
    if (!split.reached && texture == split.own) split.reached = true;
    return split.reached ? split.sprite : split.extras;
}

void hkBatchFloat(void* self, void* texture, Vec2 position, NullableRect source, uint32_t color, float rotation, Vec2 origin, float scale,
                  int32_t effects, float depth, const MethodInfo* method) {
    if (t_adjustDepth > 0) adjust(texture, &position, &effects);
    if (t_depth > 0 && !pass(texture)) return;
    g_batchFloat(self, texture, position, source, color, rotation, origin, scale, effects, depth, method);
}

void hkBatchVector(void* self, void* texture, Vec2 position, NullableRect source, uint32_t color, float rotation, Vec2 origin, Vec2 scale,
                   int32_t effects, float depth, const MethodInfo* method) {
    if (t_adjustDepth > 0) adjust(texture, &position, &effects);
    if (t_depth > 0 && !pass(texture)) return;
    g_batchVector(self, texture, position, source, color, rotation, origin, scale, effects, depth, method);
}

void hkBatchRefFloat(void* self, void* texture, Vec2* position, void* source, uint32_t* color, float rotation, Vec2 origin, float scale,
                     int32_t effects, const MethodInfo* method) {
    if (t_adjustDepth > 0 && position) adjust(texture, position, &effects);
    if (t_depth > 0 && !pass(texture)) return;
    g_batchRefFloat(self, texture, position, source, color, rotation, origin, scale, effects, method);
}

void hkBatchRefVector(void* self, void* texture, Vec2* position, void* source, uint32_t* color, float rotation, Vec2 origin, Vec2 scale,
                      int32_t effects, const MethodInfo* method) {
    if (t_adjustDepth > 0 && position) adjust(texture, position, &effects);
    if (t_depth > 0 && !pass(texture)) return;
    g_batchRefVector(self, texture, position, source, color, rotation, origin, scale, effects, method);
}

void hkDrawData(void* self, void* spriteBatch, const MethodInfo* method) {
    if (t_depth > 0 && !pass(*static_cast<void**>(self))) return;
    g_drawData(self, spriteBatch, method);
}

bool install() {
    using namespace bl::il2cpp;
    auto* batch = findClass({"Microsoft.Xna.Framework.Graphics", "SpriteBatch", ""});
    auto* data = findClass({"Terraria.DataStructures", "DrawData", ""});
    auto find = [](Il2CppClass* cls, const char* signature) { return cls ? findMethodBySignature(cls, parseSignature(signature)) : nullptr; };
    auto* batchFloat = find(batch,
        "void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)");
    auto* batchVector = find(batch,
        "void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)");
    auto* batchRefFloat = find(batch,
        "void Draw(Texture2D texture, ref Vector2 position, ref Rectangle srcRect, ref Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects)");
    auto* batchRefVector = find(batch,
        "void Draw(Texture2D texture, ref Vector2 position, ref Rectangle srcRect, ref Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects)");
    auto* drawData = find(data, "void Draw(SpriteBatch sb)");
    if (!batchFloat || !batchVector || !batchRefFloat || !batchRefVector || !drawData) {
        BL_ERROR("projectile draw split: the draw methods were not found");
        return false;
    }
    if (!hook::install(batchFloat, hkBatchFloat, &g_batchFloat) || !hook::install(batchVector, hkBatchVector, &g_batchVector) ||
        !hook::install(batchRefFloat, hkBatchRefFloat, &g_batchRefFloat) || !hook::install(batchRefVector, hkBatchRefVector, &g_batchRefVector) ||
        !hook::install(drawData, hkDrawData, &g_drawData)) {
        BL_ERROR("projectile draw split: the draw hooks were refused");
        return false;
    }
    return true;
}

}

bool drawFilterReady() {
    std::lock_guard<std::mutex> lock(g_mx);
    if (g_state == 0) g_state = install() ? 1 : -1;
    return g_state == 1;
}

bool beginDrawAdjust(void* texture, int32_t kind, DrawAdjustFn fn) {
    if (!texture || !fn || !drawFilterReady() || t_adjustDepth >= kDepth) return false;
    t_adjusts[t_adjustDepth++] = Adjust{texture, kind, fn};
    return true;
}

void endDrawAdjust() {
    if (t_adjustDepth > 0) --t_adjustDepth;
}

bool beginProjectileDrawSplit(void* ownTexture, bool extras, bool sprite) {
    if (!drawFilterReady()) return false;
    if (t_depth >= kDepth) return false;
    t_splits[t_depth++] = Split{ownTexture, extras, sprite, false};
    return true;
}

void endProjectileDrawSplit() {
    if (t_depth > 0) --t_depth;
}

}
