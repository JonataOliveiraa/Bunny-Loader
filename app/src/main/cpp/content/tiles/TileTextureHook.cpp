#include "content/tiles/TileTextureHook.h"

#include "content/tiles/TileAccess.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <array>
#include <atomic>
#include <cstdint>
#include <mutex>

namespace bl::runtime {
namespace {
constexpr int kCactus = 80;
constexpr int kDyePlants = 227;
constexpr int16_t kFruitFrame = 204;
constexpr int16_t kFruitFrameAlt = 202;
constexpr uintptr_t kNoTexture = 1;

std::atomic<TileTextureCallback> g_callback{nullptr};
std::atomic<uint32_t> g_types[2];
std::array<std::atomic<uint8_t>, 65536> g_plainGround{};
std::atomic<int32_t> g_typeCacheAt{-1};
std::mutex g_gate;
bool g_installed = false;

using InfoFn = void* (*)(void*, void*, int32_t, int32_t, void**, const MethodInfo*);
using TileFn = void* (*)(void*, uint64_t, int32_t, int32_t, void**, bool, const MethodInfo*);
InfoFn g_info = nullptr;
TileFn g_tile = nullptr;

bool hooked(int type) {
    return static_cast<uint32_t>(type) == g_types[0].load(std::memory_order_relaxed) ||
           static_cast<uint32_t>(type) == g_types[1].load(std::memory_order_relaxed);
}

int activeType(const TileArrays& t, int x, int y) {
    if (x < 0 || y < 0 || x >= t.width || y >= t.height) return -1;
    const uint32_t def = t.lookup[static_cast<int64_t>(t.width) * y + x];
    return (t.sHeader[def] & kTileActiveBit) ? t.type[def] : -1;
}

int cactusGround(const TileArrays& t, const TileTable& solid, int maxX, int maxY, int tileX, int tileY, int frameX,
                 int frameY) {
    const auto inWorld = [&](int px, int py) {
        return px >= 2 && py >= 2 && px < maxX - 2 && py < maxY - 2 && px < t.width && py < t.height;
    };
    int x = tileX;
    if (frameX == 36) --x;
    if (frameX == 54) ++x;
    if (frameX == 108) x += frameY == 18 ? -1 : 1;
    int y = tileY;
    bool found = false;
    if (!inWorld(x, y)) return 0;
    while (true) {
        const int type = activeType(t, x, y);
        if (type == kCactus)
            found = true;
        else if (found && type >= 0 && type < solid.count && solid.data[type])
            return type;
        ++y;
        if (y > tileY + 20 || !inWorld(x, y)) return 0;
    }
}

int groundOf(const TileArrays& t, int x, int y, int type) {
    TileTable solid;
    int maxX = 0, maxY = 0;
    if (!tileSolidTable(&solid) || !worldTileSize(&maxX, &maxY)) return -1;
    const uint32_t def = t.lookup[static_cast<int64_t>(t.width) * y + x];
    const int frameX = t.frameX[def], frameY = t.frameY[def];
    if (type != kDyePlants) return cactusGround(t, solid, maxX, maxY, x, y, frameX, frameY);
    if (frameX != kFruitFrame && frameX != kFruitFrameAlt) return 0;
    return cactusGround(t, solid, maxX, maxY, x, y + 1, 0, 0);
}

void* lookup(int32_t x, int32_t y, int type, void** pass) {
    if (type >= 0 && !hooked(type)) return nullptr;
    auto callback = g_callback.load(std::memory_order_acquire);
    if (!callback) return nullptr;
    TileArrays t;
    if (!tileArrays(&t)) return nullptr;
    if (type < 0) {
        type = activeType(t, x, y);
        if (!hooked(type)) return nullptr;
    } else if (x < 0 || y < 0 || x >= t.width || y >= t.height) {
        return nullptr;
    }
    int ground = 0;
    if (type == kCactus || type == kDyePlants) {
        ground = groundOf(t, x, y, type);
        if (ground == 0) return nullptr;
        if (ground > 0 && g_plainGround[ground].load(std::memory_order_relaxed)) return nullptr;
    }
    const int32_t packed =
        ground > 0 ? static_cast<int32_t>(static_cast<uint32_t>(type) | (static_cast<uint32_t>(ground) << 16)) : type;
    void* texture = callback(x, y, packed);
    if (reinterpret_cast<uintptr_t>(texture) == kNoTexture) {
        if (ground > 0) g_plainGround[ground].store(1, std::memory_order_relaxed);
        return nullptr;
    }
    if (texture && pass) *pass = nullptr;
    return texture;
}

void* fromInfo(void* self, void* info, int32_t x, int32_t y, void** pass, const MethodInfo* method) {
    const int32_t at = g_typeCacheAt.load(std::memory_order_relaxed);
    const int type = info && at >= 0 ? *reinterpret_cast<const uint16_t*>(static_cast<const uint8_t*>(info) + at) : -1;
    void* texture = lookup(x, y, type, pass);
    return texture ? texture : g_info(self, info, x, y, pass, method);
}

void* fromTile(void* self, uint64_t tile, int32_t x, int32_t y, void** pass, bool nature, const MethodInfo* method) {
    void* texture = lookup(x, y, -1, pass);
    return texture ? texture : g_tile(self, tile, x, y, pass, nature, method);
}

bool attach(const char* signature, void* replacement, void** original) {
    auto* cls = il2cpp::findClass({"Terraria.GameContent.Drawing", "TileDrawing", ""});
    auto* method = il2cpp::findMethodBySignature(cls, il2cpp::parseSignature(signature));
    if (method && hook::install(il2cpp::methodPointer(method), replacement, original)) return true;
    BL_ERROR("tile texture hook unavailable: %s", signature);
    return false;
}
}

bool installTileTextureHook(int32_t firstType, int32_t secondType, TileTextureCallback callback) {
    if (!callback) return false;
    std::lock_guard<std::mutex> lock(g_gate);
    g_types[0].store(static_cast<uint32_t>(firstType), std::memory_order_relaxed);
    g_types[1].store(static_cast<uint32_t>(secondType), std::memory_order_relaxed);
    g_callback.store(callback, std::memory_order_release);
    if (g_installed) return true;
    auto* info = il2cpp::findClass({"Terraria.DataStructures", "TileDrawInfo", ""});
    auto* typeCache = info ? il2cpp::api().class_get_field_from_name(info, "typeCache") : nullptr;
    g_typeCacheAt.store(typeCache ? static_cast<int32_t>(il2cpp::api().field_get_offset(typeCache)) : -1,
                        std::memory_order_relaxed);
    if (!attach("Texture2D GetTileDrawTexture(TileDrawInfo drawInfo, int tileX, int tileY, out EffectPass pass)",
                reinterpret_cast<void*>(fromInfo), reinterpret_cast<void**>(&g_info)))
        return false;
    if (!attach("Texture2D GetTileDrawTexture(Tile tile, int tileX, int tileY, out EffectPass pass, bool nature)",
                reinterpret_cast<void*>(fromTile), reinterpret_cast<void**>(&g_tile)))
        return false;
    g_installed = true;
    return true;
}
}
