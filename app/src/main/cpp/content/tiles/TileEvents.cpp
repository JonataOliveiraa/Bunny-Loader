#include "content/tiles/TileEvents.h"
#include "content/tiles/ModTiles.h"
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

namespace bl::runtime
{
namespace
{
std::array<std::atomic<uint32_t>, 65536> g_flags{};
std::atomic<uint32_t> g_globals{0};
std::atomic<TileEventCallback> g_callback{nullptr};
std::mutex g_gate;
uint32_t g_installed = 0;

uint32_t flagsOf(int type)
{
    return type >= 0 && type < 65536
               ? g_flags[type].load(std::memory_order_relaxed) | g_globals.load(std::memory_order_relaxed)
               : 0;
}

int invoke(int kind, int x, int y, int type, void *args)
{
    auto callback = g_callback.load(std::memory_order_acquire);
    return callback ? callback(kind, x, y, type, args) : 1;
}

using FrameFn = void (*)(int32_t, int32_t, bool, bool, const MethodInfo *);
FrameFn g_frame = nullptr;

void frame(int32_t x, int32_t y, bool reset, bool keep, const MethodInfo *method)
{
    int type = tileTypeAt(x, y);
    if (flagsOf(type) & (1u << 17))
    {
        int32_t args[]{reset, keep};
        if (!invoke(17, x, y, type, args)) return;
        reset = args[0];
        keep = args[1];
    }
    g_frame(x, y, reset, keep, method);
}

using RandomFn = void (*)(int32_t, int32_t, bool, int32_t, const MethodInfo *);
RandomFn g_over = nullptr, g_under = nullptr;

void random(int32_t x, int32_t y, bool spawn, int32_t distance, const MethodInfo *method, RandomFn original,
            int underground)
{
    original(x, y, spawn, distance, method);
    int type = tileTypeAt(x, y);
    if (flagsOf(type) & (1u << 8)) invoke(8, x, y, type, &underground);
}

void over(int32_t x, int32_t y, bool spawn, int32_t distance, const MethodInfo *method)
{
    random(x, y, spawn, distance, method, g_over, 0);
}

void under(int32_t x, int32_t y, bool spawn, int32_t distance, const MethodInfo *method)
{
    random(x, y, spawn, distance, method, g_under, 1);
}

using ImportantFn = void (*)(int32_t, int32_t, int32_t, int32_t, bool, const MethodInfo *);
ImportantFn g_important = nullptr;

void important(int32_t x, int32_t y, int32_t type, int32_t tile, bool reset, const MethodInfo *method)
{
    if ((flagsOf(type) & (1u << 24)) && invoke(24, x, y, type, nullptr)) return;
    g_important(x, y, type, tile, reset, method);
}

struct HighlightArgs
{
    void *player;
    int32_t result;
};

struct Float2
{
    float x, y;
};

thread_local void *t_drawInfo = nullptr;
using ThreadedFn = void (*)(void *, Float2, Float2, int32_t, int32_t, int32_t, int32_t, bool, bool, bool, int32_t,
                            const MethodInfo *);
ThreadedFn g_threaded = nullptr;
using GetterFn = void *(*)(void *, const MethodInfo *);
GetterFn g_threadData = nullptr;
int g_tileIndexOffset = -1;

void threaded(void *self, Float2 screen, Float2 offset, int32_t firstX, int32_t firstY, int32_t lastX, int32_t lastY,
              bool solid, bool targets, bool intoTargets, int32_t water, const MethodInfo *method)
{
    void *previous = t_drawInfo;
    auto *local = static_cast<Il2CppObject *>(g_threadData(self, nullptr));
    if (local)
    {
        auto &api = il2cpp::api();
        const auto *getter = api.class_get_method_from_name(api.object_get_class(local), "get_Value", 0);
        Il2CppObject *exception = nullptr;
        t_drawInfo = getter ? api.runtime_invoke(getter, local, nullptr, &exception) : nullptr;
        if (exception) t_drawInfo = nullptr;
    }
    g_threaded(self, screen, offset, firstX, firstY, lastX, lastY, solid, targets, intoTargets, water, method);
    t_drawInfo = previous;
}

bool highlight(int kind, int type, void *player, bool result, int32_t offset)
{
    if (!(flagsOf(type) & (1u << kind)) || offset < 0) return result;
    TileArrays arrays;
    if (!tileArrays(&arrays) || arrays.width <= 0) return result;
    HighlightArgs args{player, result};
    invoke(kind, offset % arrays.width, offset / arrays.width, type, &args);
    return args.result != 0;
}

using DangerFn = bool (*)(void *, int16_t, uint16_t, int16_t, const MethodInfo *);
DangerFn g_danger = nullptr;

bool danger(void *player, int16_t header, uint16_t type, int16_t frame, const MethodInfo *method)
{
    bool result = g_danger(player, header, type, frame, method);
    return highlight(19, type, player, result,
                     t_drawInfo ? *reinterpret_cast<int32_t *>(static_cast<uint8_t *>(t_drawInfo) + g_tileIndexOffset)
                                : -1);
}

using SpelunkFn = bool (*)(uint16_t, int16_t, int16_t, const MethodInfo *);
SpelunkFn g_spelunk = nullptr;

bool spelunk(uint16_t type, int16_t x, int16_t y, const MethodInfo *method)
{
    bool result = g_spelunk(type, x, y, method);
    return highlight(20, type, nullptr, result,
                     t_drawInfo ? *reinterpret_cast<int32_t *>(static_cast<uint8_t *>(t_drawInfo) + g_tileIndexOffset)
                                : -1);
}

bool attachTo(const char *ns, const char *name, const char *signature, void *replacement, void **original)
{
    auto *cls = il2cpp::findClass({ns, name, ""});
    auto *method = il2cpp::findMethodBySignature(cls, il2cpp::parseSignature(signature));
    if (method && hook::install(il2cpp::methodPointer(method), replacement, original)) return true;
    BL_ERROR("C# tile hook unavailable: %s.%s", name, signature);
    return false;
}

bool attach(const char *signature, void *replacement, void **original)
{
    auto *cls = il2cpp::findClass({"Terraria", "WorldGen", ""});
    auto *method = il2cpp::findMethodBySignature(cls, il2cpp::parseSignature(signature));
    if (method && hook::install(il2cpp::methodPointer(method), replacement, original)) return true;
    BL_ERROR("C# tile hook unavailable: %s", signature);
    return false;
}
}

bool installTileEvents(int32_t type, uint32_t flags, TileEventCallback callback)
{
    if (!callback || type < -1 || type >= 65536) return false;
    std::lock_guard<std::mutex> lock(g_gate);
    g_callback.store(callback, std::memory_order_release);
    if (type < 0)
        g_globals.fetch_or(flags, std::memory_order_release);
    else
        g_flags[type].fetch_or(flags, std::memory_order_release);
    uint32_t missing = flags & ~g_installed;
    if (missing & (1u << 17))
    {
        if (!attach("void TileFrame(int i, int j, bool resetFrame, bool noBreak)", reinterpret_cast<void *>(frame),
                    reinterpret_cast<void **>(&g_frame)))
            return false;
    }
    if (missing & (1u << 8))
    {
        if (!attach("void UpdateWorld_OvergroundTile(int i, int j, bool checkNPCSpawns, int wallDist)",
                    reinterpret_cast<void *>(over), reinterpret_cast<void **>(&g_over)))
            return false;
        if (!attach("void UpdateWorld_UndergroundTile(int i, int j, bool checkNPCSpawns, int wallDist)",
                    reinterpret_cast<void *>(under), reinterpret_cast<void **>(&g_under)))
            return false;
    }
    if (missing & (1u << 24))
    {
        if (!attach("void TileFrameImportant(int i, int j, int type, Tile tileCache, bool resetFrame)",
                    reinterpret_cast<void *>(important), reinterpret_cast<void **>(&g_important)))
            return false;
    }
    if ((missing & ((1u << 19) | (1u << 20))) && !g_threaded)
    {
        auto *cls = il2cpp::findClass({"Terraria.GameContent.Drawing", "TileDrawing", ""});
        const auto *getter = il2cpp::api().class_get_method_from_name(cls, "get__currentTileDrawInfo", 0);
        auto *info = il2cpp::findClass({"Terraria.DataStructures", "TileDrawInfo", ""});
        auto *field = info ? il2cpp::api().class_get_field_from_name(info, "tileIndex") : nullptr;
        if (!getter || !field) return false;
        g_threadData = reinterpret_cast<GetterFn>(il2cpp::methodPointer(getter));
        g_tileIndexOffset = il2cpp::api().field_get_offset(field);
        if (!attachTo("Terraria.GameContent.Drawing", "TileDrawing",
                      "void DrawTiles(Vector2 screenPosition, Vector2 offSet, int firstTileX, int firstTileY, int "
                      "lastTileX, int lastTileY, bool solidLayer, bool forRenderTargets, bool intoRenderTargets, int "
                      "waterStyleOverride)",
                      reinterpret_cast<void *>(threaded), reinterpret_cast<void **>(&g_threaded)))
            return false;
    }
    if ((missing & (1u << 19)) &&
        !attachTo("Terraria.GameContent.Drawing", "TileDrawing",
                  "bool IsTileDangerous(Player localPlayer, short sTileHeader, ushort typeCache, short tileFrameX)",
                  reinterpret_cast<void *>(danger), reinterpret_cast<void **>(&g_danger)))
        return false;
    if ((missing & (1u << 20)) &&
        !attachTo("Terraria", "Main", "bool IsTileSpelunkable(ushort typeCache, short tileFrameX, short tileFrameY)",
                  reinterpret_cast<void *>(spelunk), reinterpret_cast<void **>(&g_spelunk)))
        return false;
    if (flags & ((1u << 12) | (1u << 13))) ensureTileDrawHook();
    g_installed |= flags;
    return true;
}

int32_t scanTileEvents(int32_t x, int32_t y, int32_t width, int32_t height, uint32_t flags, int32_t *cells,
                       int32_t capacity)
{
    if (!cells || capacity <= 0 || width <= 0 || height <= 0) return 0;
    TileArrays arrays;
    if (!tileArrays(&arrays)) return 0;
    int right = static_cast<int>(std::min<int64_t>(arrays.width, static_cast<int64_t>(x) + width));
    int bottom = static_cast<int>(std::min<int64_t>(arrays.height, static_cast<int64_t>(y) + height));
    int count = 0;
    for (int j = std::max(y, 0); j < bottom; ++j)
        for (int i = std::max(x, 0); i < right; ++i)
        {
            auto definition = arrays.lookup[arrays.width * j + i];
            if (!(arrays.sHeader[definition] & kTileActiveBit)) continue;
            int type = arrays.type[definition];
            if (!(flagsOf(type) & flags)) continue;
            if (count == capacity) return -count;
            cells[count * 3] = i;
            cells[count * 3 + 1] = j;
            cells[count * 3 + 2] = type;
            ++count;
        }
    return count;
}

bool hasManagedTileDrawData(int32_t type)
{
    return (flagsOf(type) & ((1u << 12) | (1u << 13))) != 0;
}

void managedTileDrawData(int32_t x, int32_t y, int32_t type, TileDrawData *data, bool special)
{
    uint32_t flags = flagsOf(type);
    if (flags & (1u << 12)) invoke(12, x, y, type, data);
    if (!special && (flags & (1u << 13))) data->width = data->height = 0;
}
}
