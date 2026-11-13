#include "content/projectiles/ProjectileDrawSplit.h"

#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <atomic>
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

struct Adjust {
    void* texture;
    int32_t kind;
    DrawAdjustFn fn;
};

struct Tint {
    void* texture;
    DrawTintFn fn;
};

struct Capture {
    void* texture;
    DrawCaptureFn fn;
};

struct Scopes {
    Split splits[kDepth];
    int depth = 0;
    Adjust adjusts[kDepth];
    int adjustDepth = 0;
    Tint tints[kDepth];
    int tintDepth = 0;
    Capture captures[kDepth];
    int captureDepth = 0;
    bool inCapture = false;
};

thread_local Scopes t_scopes;
std::atomic<int> g_openScopes{0};

bool anyScopeOpen() {
    return g_openScopes.load(std::memory_order_relaxed) != 0;
}

DrawTintFn tintOf(const Scopes& s, void* texture) {
    const Tint& t = s.tints[s.tintDepth - 1];
    return texture == t.texture ? t.fn : nullptr;
}

DrawCaptureFn captureOf(const Scopes& s, void* texture) {
    if (s.inCapture) return nullptr;
    const Capture& c = s.captures[s.captureDepth - 1];
    return !c.texture || texture == c.texture ? c.fn : nullptr;
}

bool runCapture(Scopes& s, DrawCaptureFn fn, DrawCapture& draw) {
    s.inCapture = true;
    const int32_t skip = fn(&draw);
    s.inCapture = false;
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

void adjust(const Scopes& s, void* texture, Vec2* position, int32_t* effects) {
    const Adjust& a = s.adjusts[s.adjustDepth - 1];
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

bool pass(Scopes& s, void* texture) {
    Split& split = s.splits[s.depth - 1];
    if (!split.reached && texture == split.own) split.reached = true;
    return split.reached ? split.sprite : split.extras;
}

void hkBatchFloat(void* self, void* texture, Vec2 position, NullableRect source, uint32_t color, float rotation, Vec2 origin, float scale,
                  int32_t effects, float depth, const MethodInfo* method) {
    if (!anyScopeOpen()) return g_batchFloat(self, texture, position, source, color, rotation, origin, scale, effects, depth, method);
    Scopes& s = t_scopes;
    if (s.adjustDepth > 0) adjust(s, texture, &position, &effects);
    if (s.depth > 0 && !pass(s, texture)) return;
    if (s.captureDepth > 0) {
        if (DrawCaptureFn fn = captureOf(s, texture)) {
            DrawCapture d = captured(texture, position.x, position.y, source.has != 0, &source.x, color, rotation, origin, scale, scale, effects, depth);
            if (runCapture(s, fn, d)) return;
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
    if (s.tintDepth > 0) {
        if (DrawTintFn fn = tintOf(s, texture)) fn(&color, 1);
    }
    g_batchFloat(self, texture, position, source, color, rotation, origin, scale, effects, depth, method);
}

void hkBatchVector(void* self, void* texture, Vec2 position, NullableRect source, uint32_t color, float rotation, Vec2 origin, Vec2 scale,
                   int32_t effects, float depth, const MethodInfo* method) {
    if (!anyScopeOpen()) return g_batchVector(self, texture, position, source, color, rotation, origin, scale, effects, depth, method);
    Scopes& s = t_scopes;
    if (s.adjustDepth > 0) adjust(s, texture, &position, &effects);
    if (s.depth > 0 && !pass(s, texture)) return;
    if (s.captureDepth > 0) {
        if (DrawCaptureFn fn = captureOf(s, texture)) {
            DrawCapture d = captured(texture, position.x, position.y, source.has != 0, &source.x, color, rotation, origin, scale.x, scale.y, effects, depth);
            if (runCapture(s, fn, d)) return;
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
    if (!anyScopeOpen()) return g_batchRefFloat(self, texture, position, source, color, rotation, origin, scale, effects, method);
    Scopes& s = t_scopes;
    if (s.adjustDepth > 0 && position) adjust(s, texture, position, &effects);
    if (s.depth > 0 && !pass(s, texture)) return;
    if (s.captureDepth > 0 && position && source && color) {
        if (DrawCaptureFn fn = captureOf(s, texture)) {
            DrawCapture d = captured(texture, position->x, position->y, true, static_cast<int32_t*>(source), *color, rotation, origin, scale, scale, effects, 0);
            if (runCapture(s, fn, d)) return;
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
    if (s.tintDepth > 0 && color) {
        if (DrawTintFn fn = tintOf(s, texture)) {
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
    if (!anyScopeOpen()) return g_batchRefVertex(self, texture, position, source, colors, rotation, origin, scale, effects, depth, method);
    const Scopes& s = t_scopes;
    if (s.tintDepth > 0 && colors) {
        if (DrawTintFn fn = tintOf(s, texture)) {
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
    if (!anyScopeOpen()) return g_batchRefVector(self, texture, position, source, color, rotation, origin, scale, effects, method);
    Scopes& s = t_scopes;
    if (s.adjustDepth > 0 && position) adjust(s, texture, position, &effects);
    if (s.depth > 0 && !pass(s, texture)) return;
    if (s.captureDepth > 0 && position && source && color) {
        if (DrawCaptureFn fn = captureOf(s, texture)) {
            DrawCapture d = captured(texture, position->x, position->y, true, static_cast<int32_t*>(source), *color, rotation, origin, scale.x, scale.y, effects, 0);
            if (runCapture(s, fn, d)) return;
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
    if (anyScopeOpen()) {
        Scopes& s = t_scopes;
        if (s.depth > 0 && !pass(s, *static_cast<void**>(self))) return;
    }
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

namespace {

template <typename T>
bool push(T* stack, int& depth, const T& value) {
    if (depth >= kDepth) return false;
    stack[depth++] = value;
    g_openScopes.fetch_add(1, std::memory_order_relaxed);
    return true;
}

void pop(int& depth) {
    if (depth <= 0) return;
    --depth;
    g_openScopes.fetch_sub(1, std::memory_order_relaxed);
}

}

bool beginDrawAdjust(void* texture, int32_t kind, DrawAdjustFn fn) {
    if (!texture || !fn || !drawFilterReady()) return false;
    Scopes& s = t_scopes;
    return push(s.adjusts, s.adjustDepth, Adjust{texture, kind, fn});
}

void endDrawAdjust() {
    pop(t_scopes.adjustDepth);
}

bool beginDrawTint(void* texture, DrawTintFn fn) {
    if (!texture || !fn || !drawFilterReady() || t_scopes.tintDepth >= kDepth) return false;
    {
        std::lock_guard<std::mutex> lock(g_mx);
        if (g_vertexState == 0) g_vertexState = installVertex() ? 1 : -1;
    }
    Scopes& s = t_scopes;
    return push(s.tints, s.tintDepth, Tint{texture, fn});
}

void endDrawTint() {
    pop(t_scopes.tintDepth);
}

bool beginDrawCapture(void* texture, DrawCaptureFn fn) {
    if (!fn || !drawFilterReady()) return false;
    Scopes& s = t_scopes;
    return push(s.captures, s.captureDepth, Capture{texture, fn});
}

void endDrawCapture() {
    pop(t_scopes.captureDepth);
}

bool beginProjectileDrawSplit(void* ownTexture, bool extras, bool sprite) {
    if (!drawFilterReady()) return false;
    Scopes& s = t_scopes;
    return push(s.splits, s.depth, Split{ownTexture, extras, sprite, false});
}

void endProjectileDrawSplit() {
    pop(t_scopes.depth);
}

}
