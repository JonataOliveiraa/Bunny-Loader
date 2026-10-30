#include "content/buffs/BuffIconLook.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

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

using DrawFn = void (*)(void*, void*, Vec2, NullableRect, uint32_t, float, Vec2, float, int32_t, float, const MethodInfo*);
using DrawStringFn = void (*)(void*, void*, void*, Vec2, uint32_t, float, Vec2, float, int32_t, float, const MethodInfo*);

BuffIconLook g_look;
DrawFn g_draw = nullptr;
DrawStringFn g_drawString = nullptr;
std::mutex g_mx;
int g_state = 0;
bool g_logged = false;

void hkDraw(void* self, void* texture, Vec2 position, NullableRect source, uint32_t color, float rotation, Vec2 origin, float scale,
            int32_t effects, float depth, const MethodInfo* method) {
    BuffIconLook& look = g_look;
    if (look.active.load(std::memory_order_relaxed) && texture == look.texture) {
        if (!g_logged) {
            g_logged = true;
            BL_INFO("buff icon: drawn at %.1f,%.1f origin %.1f,%.1f scale %.2f source %d %d,%d %dx%d color %08x", position.x, position.y, origin.x,
                    origin.y, scale, source.has, source.x, source.y, source.w, source.h, color);
        }
        if (look.newTexture) texture = look.newTexture;
        position.x += look.dx;
        position.y += look.dy;
        if (look.hasSource) {
            source.has = 1;
            source.x = look.source[0];
            source.y = look.source[1];
            source.w = look.source[2];
            source.h = look.source[3];
        }
        if (look.hasColor) color = look.color;
        look.applied++;
    }
    g_draw(self, texture, position, source, color, rotation, origin, scale, effects, depth, method);
}

void hkDrawString(void* self, void* font, void* text, Vec2 position, uint32_t color, float rotation, Vec2 origin, float scale, int32_t effects,
                  float depth, const MethodInfo* method) {
    BuffIconLook& look = g_look;
    if (look.active.load(std::memory_order_relaxed)) {
        position.x += look.tx;
        position.y += look.ty;
    }
    g_drawString(self, font, text, position, color, rotation, origin, scale, effects, depth, method);
}

bool install() {
    using namespace bl::il2cpp;
    auto* cls = findClass({"Microsoft.Xna.Framework.Graphics", "SpriteBatch", ""});
    auto* draw = cls ? findMethodBySignature(cls, parseSignature(
        "void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)")) : nullptr;
    auto* drawString = cls ? findMethodBySignature(cls, parseSignature(
        "void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)")) : nullptr;
    if (!draw || !drawString) {
        BL_ERROR("buff icon: SpriteBatch.Draw or DrawString not found");
        return false;
    }
    if (!hook::install(draw, hkDraw, &g_draw) || !hook::install(drawString, hkDrawString, &g_drawString)) {
        BL_ERROR("buff icon: the SpriteBatch hooks were refused");
        return false;
    }
    return true;
}

}

BuffIconLook* buffIconLook() {
    std::lock_guard<std::mutex> l(g_mx);
    if (g_state == 0) g_state = install() ? 1 : -1;
    return g_state == 1 ? &g_look : nullptr;
}

}
