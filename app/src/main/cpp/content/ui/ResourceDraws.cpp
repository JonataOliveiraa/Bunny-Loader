#include "content/ui/ResourceDraws.h"

#include "content/common/ContentAssets.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <atomic>
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

using DrawFn = void (*)(void*, void*, Vec2, NullableRect, uint32_t, float, Vec2, Vec2, int32_t, float, const MethodInfo*);
using DrawFloatFn = void (*)(void*, void*, Vec2, NullableRect, uint32_t, float, Vec2, float, int32_t, float, const MethodInfo*);
using DrawRefFn = void (*)(void*, void*, Vec2*, uint32_t*, float, Vec2, float, const MethodInfo*);

std::atomic<ResourceDrawCallback> g_callback{nullptr};
std::atomic<bool> g_active{false};
thread_local bool t_inside = false;
DrawFn g_drawVector = nullptr;
const MethodInfo* g_drawVectorInfo = nullptr;
DrawFloatFn g_drawFloat = nullptr;
DrawRefFn g_drawRef = nullptr;
std::mutex g_mx;
int g_state = 0;

bool Intercept(void* self, ResourceDrawArgs& args, ResourceDrawCallback& callback) {
    callback = g_callback.load(std::memory_order_acquire);
    if (!callback || t_inside || !g_active.load(std::memory_order_relaxed)) return false;
    t_inside = true;
    const int32_t draw = callback(0, self, &args);
    if (draw && g_drawVector && args.texture) {
        NullableRect source{static_cast<uint8_t>(args.hasSource ? 1 : 0), args.source[0], args.source[1], args.source[2], args.source[3]};
        g_drawVector(self, args.texture, Vec2{args.x, args.y}, source, args.color, args.rotation, Vec2{args.originX, args.originY},
                     Vec2{args.scaleX, args.scaleY}, args.effects, 0.0f, g_drawVectorInfo);
    }
    callback(1, self, &args);
    t_inside = false;
    return true;
}

void hkDrawFloat(void* self, void* texture, Vec2 position, NullableRect source, uint32_t color, float rotation, Vec2 origin, float scale,
                 int32_t effects, float depth, const MethodInfo* method) {
    ResourceDrawArgs args{texture, position.x, position.y, source.has, {source.x, source.y, source.w, source.h}, color, rotation, origin.x, origin.y,
                          scale, scale, effects};
    ResourceDrawCallback callback;
    if (Intercept(self, args, callback)) return;
    g_drawFloat(self, texture, position, source, color, rotation, origin, scale, effects, depth, method);
}

void hkDrawRef(void* self, void* texture, Vec2* position, uint32_t* color, float rotation, Vec2 origin, float scale, const MethodInfo* method) {
    ResourceDrawArgs args{texture, position->x, position->y, 0, {0, 0, 0, 0}, *color, rotation, origin.x, origin.y, scale, scale, 0};
    ResourceDrawCallback callback;
    if (Intercept(self, args, callback)) return;
    g_drawRef(self, texture, position, color, rotation, origin, scale, method);
}

bool install() {
    using namespace bl::il2cpp;
    auto* cls = findClass({"Microsoft.Xna.Framework.Graphics", "SpriteBatch", ""});
    auto* drawFloat = cls ? findMethodBySignature(cls, parseSignature(
        "void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)")) : nullptr;
    auto* drawVector = cls ? findMethodBySignature(cls, parseSignature(
        "void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)")) : nullptr;
    auto* drawRef = cls ? findMethodBySignature(cls, parseSignature(
        "void Draw(Texture2D texture, ref Vector2 position, ref Color color, float rotation, Vector2 origin, float scale)")) : nullptr;
    if (!drawFloat || !drawVector || !drawRef) {
        BL_ERROR("resource overlay: the SpriteBatch.Draw overloads were not found");
        return false;
    }
    g_drawVector = reinterpret_cast<DrawFn>(methodPointer(drawVector));
    g_drawVectorInfo = drawVector;
    if (!hook::install(drawFloat, hkDrawFloat, &g_drawFloat) || !hook::install(drawRef, hkDrawRef, &g_drawRef)) {
        BL_ERROR("resource overlay: the SpriteBatch hooks were refused");
        return false;
    }
    return true;
}

}

bool setResourceDrawCallback(ResourceDrawCallback callback) {
    std::lock_guard<std::mutex> lock(g_mx);
    if (g_state == 0) g_state = install() ? 1 : -1;
    if (g_state != 1) return false;
    g_callback.store(callback, std::memory_order_release);
    return true;
}

void setResourceDrawActive(bool active) { g_active.store(active, std::memory_order_relaxed); }

void* assetOfTexture(void* texture) {
    if (!texture) return nullptr;
    for (Il2CppObject* asset : content::textureAssets()) {
        if (content::textureAssetValue(asset) == static_cast<Il2CppObject*>(texture)) return asset;
    }
    return nullptr;
}

}
