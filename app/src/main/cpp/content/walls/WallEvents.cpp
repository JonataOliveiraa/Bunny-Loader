#include "content/walls/WallEvents.h"
#include "content/tiles/TileAccess.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Api.h"
#include "il2cpp/Signature.h"
#include "core/Log.h"
#include <algorithm>
#include <array>
#include <atomic>
#include <mutex>

namespace bl::runtime {
namespace {
std::array<std::atomic<uint32_t>, 65536> g_flags{};
std::atomic<uint32_t> g_typeFlags{0};
std::atomic<int32_t> g_lowType{65536};
std::atomic<int32_t> g_highType{-1};
std::atomic<WallEventCallback> g_callback{nullptr};
std::mutex g_gate;
uint32_t g_installed = 0;

uint32_t flagsOf(int type) {
    return type > 0 && type < 65536 ? g_flags[type].load(std::memory_order_relaxed) : 0;
}

int invoke(int kind, int x, int y, int type, void* args) {
    auto callback = g_callback.load(std::memory_order_acquire);
    return callback ? callback(kind, x, y, type, args) : 1;
}

using FrameFn = void (*)(int32_t, int32_t, bool, const MethodInfo*);
FrameFn g_frame = nullptr;

void frame(int32_t x, int32_t y, bool reset, const MethodInfo* method) {
    int type = wallTypeAt(x, y);
    if (!(flagsOf(type) & 1u)) {
        g_frame(x, y, reset, method);
        return;
    }
    int32_t args[]{reset};
    invoke(0, x, y, type, args);
    g_frame(x, y, reset, method);
    invoke(1, x, y, type, args);
}

using RandomFn = void (*)(int32_t, int32_t, bool, int32_t, const MethodInfo*);
RandomFn g_over = nullptr, g_under = nullptr;

void random(int32_t x, int32_t y, bool spawn, int32_t distance, const MethodInfo* method, RandomFn original,
            int underground) {
    original(x, y, spawn, distance, method);
    int type = wallTypeAt(x, y);
    if (flagsOf(type) & 2u) invoke(2, x, y, type, &underground);
}

void over(int32_t x, int32_t y, bool spawn, int32_t distance, const MethodInfo* method) {
    random(x, y, spawn, distance, method, g_over, 0);
}

void under(int32_t x, int32_t y, bool spawn, int32_t distance, const MethodInfo* method) {
    random(x, y, spawn, distance, method, g_under, 1);
}

bool attach(const char* ns, const char* name, const char* signature, void* replacement, void** original) {
    auto* cls = il2cpp::findClass({ns, name, ""});
    auto* method = il2cpp::findMethodBySignature(cls, il2cpp::parseSignature(signature));
    if (method && hook::install(il2cpp::methodPointer(method), replacement, original)) return true;
    BL_ERROR("C# wall hook unavailable: %s.%s", name, signature);
    return false;
}
}

bool installWallEvents(int32_t type, uint32_t flags, WallEventCallback callback) {
    if (!callback || type <= 0 || type >= 65536) return false;
    std::lock_guard<std::mutex> lock(g_gate);
    g_callback.store(callback, std::memory_order_release);
    if (flags) {
        if (type < g_lowType.load(std::memory_order_relaxed)) g_lowType.store(type, std::memory_order_relaxed);
        if (type > g_highType.load(std::memory_order_relaxed)) g_highType.store(type, std::memory_order_relaxed);
        g_flags[type].fetch_or(flags, std::memory_order_release);
        g_typeFlags.fetch_or(flags, std::memory_order_release);
    }
    uint32_t missing = flags & ~g_installed;
    if ((missing & 1u) && !attach("Terraria", "Framing", "void WallFrame(int i, int j, bool resetFrame)",
                                  reinterpret_cast<void*>(frame), reinterpret_cast<void**>(&g_frame)))
        return false;
    if (missing & 2u) {
        if (!attach("Terraria", "WorldGen",
                    "void UpdateWorld_OvergroundTile(int i, int j, bool checkNPCSpawns, int wallDist)",
                    reinterpret_cast<void*>(over), reinterpret_cast<void**>(&g_over)))
            return false;
        if (!attach("Terraria", "WorldGen",
                    "void UpdateWorld_UndergroundTile(int i, int j, bool checkNPCSpawns, int wallDist)",
                    reinterpret_cast<void*>(under), reinterpret_cast<void**>(&g_under)))
            return false;
    }
    g_installed |= flags & 3u;
    return true;
}

int32_t scanWallEvents(int32_t x, int32_t y, int32_t width, int32_t height, uint32_t flags, int32_t* cells,
                       int32_t capacity) {
    if (!cells || capacity <= 0 || width <= 0 || height <= 0) return 0;
    if (!(g_typeFlags.load(std::memory_order_acquire) & flags)) return 0;
    TileArrays arrays;
    if (!tileArrays(&arrays) || !arrays.wall) return 0;
    const int low = g_lowType.load(std::memory_order_relaxed);
    const int high = g_highType.load(std::memory_order_relaxed);
    const int left = std::max(x, 0);
    const int right = static_cast<int>(std::min<int64_t>(arrays.width, static_cast<int64_t>(x) + width));
    const int bottom = static_cast<int>(std::min<int64_t>(arrays.height, static_cast<int64_t>(y) + height));
    int count = 0;
    for (int j = std::max(y, 0); j < bottom; ++j) {
        const uint16_t* row = arrays.wall + static_cast<int64_t>(arrays.width) * j;
        for (int i = left; i < right; ++i) {
            const int type = row[i];
            if (type < low || type > high || !(g_flags[type].load(std::memory_order_relaxed) & flags)) continue;
            if (count == capacity) return -count;
            cells[count * 3] = i;
            cells[count * 3 + 1] = j;
            cells[count * 3 + 2] = type;
            ++count;
        }
    }
    return count;
}
}
