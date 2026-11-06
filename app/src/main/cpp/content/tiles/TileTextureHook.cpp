#include "content/tiles/TileTextureHook.h"

#include "content/tiles/TileAccess.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <atomic>
#include <cstdint>
#include <mutex>

namespace bl::runtime
{
namespace
{
std::atomic<TileTextureCallback> g_callback{nullptr};
std::atomic<uint32_t> g_types[2];
std::mutex g_gate;
bool g_installed = false;

using InfoFn = void *(*)(void *, void *, int32_t, int32_t, void **, const MethodInfo *);
using TileFn = void *(*)(void *, uint64_t, int32_t, int32_t, void **, bool, const MethodInfo *);
InfoFn g_info = nullptr;
TileFn g_tile = nullptr;

void *lookup(int32_t x, int32_t y, void **pass)
{
    int type = tileTypeAt(x, y);
    if (static_cast<uint32_t>(type) != g_types[0].load(std::memory_order_relaxed) &&
        static_cast<uint32_t>(type) != g_types[1].load(std::memory_order_relaxed))
        return nullptr;
    auto callback = g_callback.load(std::memory_order_acquire);
    void *texture = callback ? callback(x, y, type) : nullptr;
    if (texture && pass) *pass = nullptr;
    return texture;
}

void *fromInfo(void *self, void *info, int32_t x, int32_t y, void **pass, const MethodInfo *method)
{
    void *texture = lookup(x, y, pass);
    return texture ? texture : g_info(self, info, x, y, pass, method);
}

void *fromTile(void *self, uint64_t tile, int32_t x, int32_t y, void **pass, bool nature, const MethodInfo *method)
{
    void *texture = lookup(x, y, pass);
    return texture ? texture : g_tile(self, tile, x, y, pass, nature, method);
}

bool attach(const char *signature, void *replacement, void **original)
{
    auto *cls = il2cpp::findClass({"Terraria.GameContent.Drawing", "TileDrawing", ""});
    auto *method = il2cpp::findMethodBySignature(cls, il2cpp::parseSignature(signature));
    if (method && hook::install(il2cpp::methodPointer(method), replacement, original)) return true;
    BL_ERROR("tile texture hook unavailable: %s", signature);
    return false;
}
}

bool installTileTextureHook(int32_t firstType, int32_t secondType, TileTextureCallback callback)
{
    if (!callback) return false;
    std::lock_guard<std::mutex> lock(g_gate);
    g_types[0].store(static_cast<uint32_t>(firstType), std::memory_order_relaxed);
    g_types[1].store(static_cast<uint32_t>(secondType), std::memory_order_relaxed);
    g_callback.store(callback, std::memory_order_release);
    if (g_installed) return true;
    if (!attach("Texture2D GetTileDrawTexture(TileDrawInfo drawInfo, int tileX, int tileY, out EffectPass pass)",
                reinterpret_cast<void *>(fromInfo), reinterpret_cast<void **>(&g_info)))
        return false;
    if (!attach("Texture2D GetTileDrawTexture(Tile tile, int tileX, int tileY, out EffectPass pass, bool nature)",
                reinterpret_cast<void *>(fromTile), reinterpret_cast<void **>(&g_tile)))
        return false;
    g_installed = true;
    return true;
}
}
