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
using BatchRefVertexFn = void (*)(void*, void*, Vec2, void*, uint32_t*, float, Vec2, float, int32_t, float, const MethodInfo*);
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

struct Tint {
    void* texture;
    DrawTintFn fn;
};

thread_local Tint t_tints[kDepth];
thread_local int t_tintDepth = 0;

DrawTintFn tintOf(void* texture) {
    const Tint& t = t_tints[t_tintDepth - 1];
    return texture == t.texture ? t.fn : nullptr;
}

struct Capture {
    void* texture;
    DrawCaptureFn fn;
};

thread_local Capture t_captures[kDepth];
thread_local int t_captureDepth = 0;
thread_local bool t_inCapture = false;

DrawCaptureFn captureOf(void* texture) {
    if (t_inCapture) return nullptr;
    const Capture& c = t_captures[t_captureDepth - 1];
    return !c.texture || texture == c.texture ? c.fn : nullptr;
}

bool runCapture(DrawCaptureFn fn, DrawCapture& draw) {
    t_inCapture = true;
    const int32_t skip = fn(&draw);
    t_inCapture = false;
    return skip != 0;
}

DrawCapture captured(void* texture, float x, float y, bool hasSource, const int32_t* rect, uint32_t color, float rotation, Vec2 origin,
                     float scaleX, float scaleY, int32_t effects, float depth) {
    DrawCapture d{};
    d.texture = texture;
    d.x = x;
    d.y = y;
    d.hasSource = hasSource ? 1 : 0;
    if (hasSource) {
        d.sx = rect[0];
        d.sy = rect[1];
        d.sw = rect[2];
        d.sh = rect[3];
    }
    d.color = color;
    d.rotation = rotation;
    d.ox = origin.x;
    d.oy = origin.y;
    d.scaleX = scaleX;
    d.scaleY = scaleY;
    d.effects = effects;
    d.depth = depth;
    return d;
}

void fromCapture(const DrawCapture& d, NullableRect& source) {
    source.has = d.hasSource ? 1 : 0;
    source.x = d.sx;
    source.y = d.sy;
    source.w = d.sw;
    source.h = d.sh;
}

void adjust(void* texture, Vec2* position, int32_t* effects) {
    const Adjust& a = t_adjusts[t_adjustDepth - 1];
    if (texture == a.texture && a.fn) a.fn(a.kind, &position->x, effects);
}

BatchFloatFn g_batchFloat = nullptr;
BatchVectorFn g_batchVector = nullptr;
BatchRefFloatFn g_batchRefFloat = nullptr;
BatchRefVectorFn g_batchRefVector = nullptr;
BatchRefVertexFn g_batchRefVertex = nullptr;
DrawDataFn g_drawData = nullptr;
std::mutex g_mx;
int g_state = 0;
int g_vertexState = 0;

bool pass(void* texture) {
    Split& split = t_splits[t_depth - 1];
    if (!split.reached && texture == split.own) split.reached = true;
    return split.reached ? split.sprite : split.extras;
}

void hkBatchFloat(void* self, void* texture, Vec2 position, NullableRect source, uint32_t color, float rotation, Vec2 origin, float scale,
                  int32_t effects, float depth, const MethodInfo* method) {
    if (t_adjustDepth > 0) adjust(texture, &position, &effects);
    if (t_depth > 0 && !pass(texture)) return;
    if (t_captureDepth > 0) {
        if (DrawCaptureFn fn = captureOf(texture)) {
            DrawCapture d = captured(texture, position.x, position.y, source.has != 0, &source.x, color, rotation, origin, scale, scale, effects, depth);
            if (runCapture(fn, d)) return;
            position = Vec2{d.x, d.y};
            fromCapture(d, source);
            color = d.color;
            rotation = d.rotation;
            origin = Vec2{d.ox, d.oy};
            scale = d.scaleX;
            effects = d.effects;
            depth = d.depth;
        }
    }
    if (t_tintDepth > 0) {
        if (DrawTintFn fn = tintOf(texture)) fn(&color, 1);
    }
    g_batchFloat(self, texture, position, source, color, rotation, origin, scale, effects, depth, method);
}

void hkBatchVector(void* self, void* texture, Vec2 position, NullableRect source, uint32_t color, float rotation, Vec2 origin, Vec2 scale,
                   int32_t effects, float depth, const MethodInfo* method) {
    if (t_adjustDepth > 0) adjust(texture, &position, &effects);
    if (t_depth > 0 && !pass(texture)) return;
    if (t_captureDepth > 0) {
        if (DrawCaptureFn fn = captureOf(texture)) {
            DrawCapture d = captured(texture, position.x, position.y, source.has != 0, &source.x, color, rotation, origin, scale.x, scale.y, effects, depth);
            if (runCapture(fn, d)) return;
            position = Vec2{d.x, d.y};
            fromCapture(d, source);
            color = d.color;
            rotation = d.rotation;
            origin = Vec2{d.ox, d.oy};
            scale = Vec2{d.scaleX, d.scaleY};
            effects = d.effects;
            depth = d.depth;
        }
    }
    g_batchVector(self, texture, position, source, color, rotation, origin, scale, effects, depth, method);
}

void hkBatchRefFloat(void* self, void* texture, Vec2* position, void* source, uint32_t* color, float rotation, Vec2 origin, float scale,
                     int32_t effects, const MethodInfo* method) {
    if (t_adjustDepth > 0 && position) adjust(texture, position, &effects);
    if (t_depth > 0 && !pass(texture)) return;
    if (t_captureDepth > 0 && position && source && color) {
        if (DrawCaptureFn fn = captureOf(texture)) {
            DrawCapture d = captured(texture, position->x, position->y, true, static_cast<int32_t*>(source), *color, rotation, origin, scale, scale, effects, 0);
            if (runCapture(fn, d)) return;
            const Vec2 savedPosition = *position;
            int32_t* rect = static_cast<int32_t*>(source);
            const int32_t savedRect[4] = {rect[0], rect[1], rect[2], rect[3]};
            const uint32_t savedColor = *color;
            *position = Vec2{d.x, d.y};
            rect[0] = d.sx;
            rect[1] = d.sy;
            rect[2] = d.sw;
            rect[3] = d.sh;
            *color = d.color;
            g_batchRefFloat(self, texture, position, source, color, d.rotation, Vec2{d.ox, d.oy}, d.scaleX, d.effects, method);
            *position = savedPosition;
            for (int k = 0; k < 4; ++k) rect[k] = savedRect[k];
            *color = savedColor;
            return;
        }
    }
    if (t_tintDepth > 0 && color) {
        if (DrawTintFn fn = tintOf(texture)) {
            const uint32_t saved = *color;
            fn(color, 1);
            g_batchRefFloat(self, texture, position, source, color, rotation, origin, scale, effects, method);
            *color = saved;
            return;
        }
    }
    g_batchRefFloat(self, texture, position, source, color, rotation, origin, scale, effects, method);
}

void hkBatchRefVertex(void* self, void* texture, Vec2 position, void* source, uint32_t* colors, float rotation, Vec2 origin, float scale,
                      int32_t effects, float depth, const MethodInfo* method) {
    if (t_tintDepth > 0 && colors) {
        if (DrawTintFn fn = tintOf(texture)) {
            uint32_t saved[4] = {colors[0], colors[1], colors[2], colors[3]};
            fn(colors, 4);
            g_batchRefVertex(self, texture, position, source, colors, rotation, origin, scale, effects, depth, method);
            for (int k = 0; k < 4; ++k) colors[k] = saved[k];
            return;
        }
    }
    g_batchRefVertex(self, texture, position, source, colors, rotation, origin, scale, effects, depth, method);
}

void hkBatchRefVector(void* self, void* texture, Vec2* position, void* source, uint32_t* color, float rotation, Vec2 origin, Vec2 scale,
                      int32_t effects, const MethodInfo* method) {
    if (t_adjustDepth > 0 && position) adjust(texture, position, &effects);
    if (t_depth > 0 && !pass(texture)) return;
    if (t_captureDepth > 0 && position && source && color) {
        if (DrawCaptureFn fn = captureOf(texture)) {
            DrawCapture d = captured(texture, position->x, position->y, true, static_cast<int32_t*>(source), *color, rotation, origin, scale.x, scale.y, effects, 0);
            if (runCapture(fn, d)) return;
            const Vec2 savedPosition = *position;
            int32_t* rect = static_cast<int32_t*>(source);
            const int32_t savedRect[4] = {rect[0], rect[1], rect[2], rect[3]};
            const uint32_t savedColor = *color;
            *position = Vec2{d.x, d.y};
            rect[0] = d.sx;
            rect[1] = d.sy;
            rect[2] = d.sw;
            rect[3] = d.sh;
            *color = d.color;
            g_batchRefVector(self, texture, position, source, color, d.rotation, Vec2{d.ox, d.oy}, Vec2{d.scaleX, d.scaleY}, d.effects, method);
            *position = savedPosition;
            for (int k = 0; k < 4; ++k) rect[k] = savedRect[k];
            *color = savedColor;
            return;
        }
    }
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

bool installVertex() {
    using namespace bl::il2cpp;
    auto* batch = findClass({"Microsoft.Xna.Framework.Graphics", "SpriteBatch", ""});
    auto* draw = batch ? findMethodBySignature(batch, parseSignature(
        "void Draw(Texture2D texture, Vector2 position, ref Rectangle srcRect, ref VertexColors color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)")) : nullptr;
    if (!draw || !hook::install(draw, hkBatchRefVertex, &g_batchRefVertex)) {
        BL_ERROR("draw tint: the vertex colors draw is not hooked");
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

bool beginDrawTint(void* texture, DrawTintFn fn) {
    if (!texture || !fn || !drawFilterReady() || t_tintDepth >= kDepth) return false;
    {
        std::lock_guard<std::mutex> lock(g_mx);
        if (g_vertexState == 0) g_vertexState = installVertex() ? 1 : -1;
    }
    t_tints[t_tintDepth++] = Tint{texture, fn};
    return true;
}

void endDrawTint() {
    if (t_tintDepth > 0) --t_tintDepth;
}

bool beginDrawCapture(void* texture, DrawCaptureFn fn) {
    if (!fn || !drawFilterReady() || t_captureDepth >= kDepth) return false;
    t_captures[t_captureDepth++] = Capture{texture, fn};
    return true;
}

void endDrawCapture() {
    if (t_captureDepth > 0) --t_captureDepth;
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
