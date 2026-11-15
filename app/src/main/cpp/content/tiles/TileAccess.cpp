#include "content/tiles/TileAccess.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "content/common/GameRefs.h"

#include <atomic>
#include <cstddef>
#include <mutex>

namespace bl::runtime {

namespace {

constexpr size_t kClassStaticFields = 0xB8;
constexpr size_t kNoOffset = static_cast<size_t>(-1);

struct Fields {
    bool ok = false;
    Il2CppClass* data = nullptr;
    Il2CppClass* main = nullptr;
    Il2CppClass* worldGen = nullptr;
    FieldInfo* world = nullptr;
    FieldInfo* grassUnderground = nullptr;
    size_t grassUndergroundAt = kNoOffset;
    int32_t width = -1, height = -1;
    FieldInfo *lookup = nullptr, *type = nullptr, *sHeader = nullptr, *frameX = nullptr,
              *frameY = nullptr, *bHeader = nullptr, *bHeader2 = nullptr, *bHeader3 = nullptr, *wall = nullptr;
    FieldInfo *lighted = nullptr, *solid = nullptr, *maxX = nullptr, *maxY = nullptr;
    size_t worldAt = kNoOffset, lookupAt = kNoOffset, typeAt = kNoOffset, sHeaderAt = kNoOffset,
           frameXAt = kNoOffset, frameYAt = kNoOffset, bHeaderAt = kNoOffset, bHeader2At = kNoOffset,
           bHeader3At = kNoOffset, wallAt = kNoOffset, lightedAt = kNoOffset, solidAt = kNoOffset,
           maxXAt = kNoOffset, maxYAt = kNoOffset;
};

std::atomic<int> g_fast{0};

size_t staticOffset(FieldInfo* f) {
    return f ? il2cpp::api().field_get_offset(f) : kNoOffset;
}

const Fields& fields() {
    static Fields f;
    static std::once_flag once;
    std::call_once(once, [] {
        using namespace il2cpp;
        Il2CppClass* data = findClass({"Terraria", "TileData", {}});
        Il2CppClass* main = findClass({"Terraria", "Main", {}});
        if (!data || !main) return;
        f.data = data;
        f.main = main;
        f.world = findField(main, "tile");
        f.width = fieldOffset(data, "_width");
        f.height = fieldOffset(data, "_height");
        f.lookup = findField(data, "TileLookup");
        f.type = findField(data, "TileType");
        f.sHeader = findField(data, "TileSHeader");
        f.frameX = findField(data, "TileFrameX");
        f.frameY = findField(data, "TileFrameY");
        f.bHeader = findField(data, "TileBHeader");
        f.bHeader2 = findField(data, "TileBHeader2");
        f.bHeader3 = findField(data, "TileBHeader3");
        f.wall = findField(data, "TileWall");
        f.lighted = findField(main, "tileLighted");
        f.solid = findField(main, "tileSolid");
        f.maxX = findField(main, "maxTilesX");
        f.maxY = findField(main, "maxTilesY");
        f.worldAt = staticOffset(f.world);
        f.lookupAt = staticOffset(f.lookup);
        f.typeAt = staticOffset(f.type);
        f.sHeaderAt = staticOffset(f.sHeader);
        f.frameXAt = staticOffset(f.frameX);
        f.frameYAt = staticOffset(f.frameY);
        f.bHeaderAt = staticOffset(f.bHeader);
        f.bHeader2At = staticOffset(f.bHeader2);
        f.bHeader3At = staticOffset(f.bHeader3);
        f.wallAt = staticOffset(f.wall);
        f.lightedAt = staticOffset(f.lighted);
        f.solidAt = staticOffset(f.solid);
        f.maxXAt = staticOffset(f.maxX);
        f.maxYAt = staticOffset(f.maxY);
        f.worldGen = findClass({"Terraria", "WorldGen", {}});
        f.grassUnderground = f.worldGen ? findField(f.worldGen, "growGrassUnderground") : nullptr;
        f.grassUndergroundAt = staticOffset(f.grassUnderground);
        if (!f.grassUnderground) BL_WARN("tiles: WorldGen.growGrassUnderground not found; RandomUpdate is not batched");
        f.ok = f.world && f.width >= 0 && f.height >= 0 && f.lookup && f.type && f.sHeader &&
               f.frameX && f.frameY && f.bHeader && f.bHeader2 && f.bHeader3 && f.wall;
        if (!f.ok) BL_ERROR("tiles: TileData fields not found; mod tiles disabled");
    });
    return f;
}

uint8_t* staticsOf(Il2CppClass* cls) {
    return *reinterpret_cast<uint8_t**>(reinterpret_cast<uint8_t*>(cls) + kClassStaticFields);
}

template <typename T>
T fastRead(uint8_t* statics, size_t at) {
    return *reinterpret_cast<T*>(statics + at);
}

template <typename T>
T slowRead(FieldInfo* f) {
    T value{};
    if (f) il2cpp::api().field_static_get_value(f, &value);
    return value;
}

bool complete(const TileArrays& t) {
    return t.width > 0 && t.height > 0 && t.lookup && t.type && t.sHeader;
}

bool readSlow(const Fields& f, TileArrays* out, Il2CppObject** world) {
    *world = slowRead<Il2CppObject*>(f.world);
    if (!*world) return false;
    out->width = field<int32_t>(*world, f.width);
    out->height = field<int32_t>(*world, f.height);
    out->lookup = slowRead<uint32_t*>(f.lookup);
    out->type = slowRead<uint16_t*>(f.type);
    out->sHeader = slowRead<int16_t*>(f.sHeader);
    out->frameX = slowRead<int16_t*>(f.frameX);
    out->frameY = slowRead<int16_t*>(f.frameY);
    out->bHeader = slowRead<uint8_t*>(f.bHeader);
    out->bHeader2 = slowRead<uint8_t*>(f.bHeader2);
    out->bHeader3 = slowRead<uint8_t*>(f.bHeader3);
    out->wall = slowRead<uint16_t*>(f.wall);
    return complete(*out);
}

bool readFast(const Fields& f, TileArrays* out, Il2CppObject** world) {
    *world = nullptr;
    uint8_t* mainStatics = staticsOf(f.main);
    uint8_t* dataStatics = staticsOf(f.data);
    if (!mainStatics || !dataStatics) return false;
    *world = fastRead<Il2CppObject*>(mainStatics, f.worldAt);
    if (!*world) return false;
    out->width = field<int32_t>(*world, f.width);
    out->height = field<int32_t>(*world, f.height);
    out->lookup = fastRead<uint32_t*>(dataStatics, f.lookupAt);
    out->type = fastRead<uint16_t*>(dataStatics, f.typeAt);
    out->sHeader = fastRead<int16_t*>(dataStatics, f.sHeaderAt);
    out->frameX = fastRead<int16_t*>(dataStatics, f.frameXAt);
    out->frameY = fastRead<int16_t*>(dataStatics, f.frameYAt);
    out->bHeader = fastRead<uint8_t*>(dataStatics, f.bHeaderAt);
    out->bHeader2 = fastRead<uint8_t*>(dataStatics, f.bHeader2At);
    out->bHeader3 = fastRead<uint8_t*>(dataStatics, f.bHeader3At);
    out->wall = fastRead<uint16_t*>(dataStatics, f.wallAt);
    return complete(*out);
}

void verify(const Fields& f, const TileArrays& slow, Il2CppObject* world) {
    if (!world || !slow.lookup) return;
    uint8_t* mainStatics = staticsOf(f.main);
    uint8_t* dataStatics = staticsOf(f.data);
    const bool same = mainStatics && dataStatics && fastRead<Il2CppObject*>(mainStatics, f.worldAt) == world &&
                      fastRead<uint32_t*>(dataStatics, f.lookupAt) == slow.lookup &&
                      fastRead<uint16_t*>(dataStatics, f.typeAt) == slow.type &&
                      fastRead<int16_t*>(dataStatics, f.sHeaderAt) == slow.sHeader &&
                      fastRead<int16_t*>(dataStatics, f.frameXAt) == slow.frameX &&
                      fastRead<int16_t*>(dataStatics, f.frameYAt) == slow.frameY &&
                      fastRead<uint8_t*>(dataStatics, f.bHeaderAt) == slow.bHeader &&
                      fastRead<uint8_t*>(dataStatics, f.bHeader2At) == slow.bHeader2 &&
                      fastRead<uint8_t*>(dataStatics, f.bHeader3At) == slow.bHeader3 &&
                      fastRead<uint16_t*>(dataStatics, f.wallAt) == slow.wall;
    if (!same) {
        TileArrays again;
        Il2CppObject* againWorld = nullptr;
        readSlow(f, &again, &againWorld);
        if (againWorld != world || again.lookup != slow.lookup || again.type != slow.type ||
            again.sHeader != slow.sHeader || again.wall != slow.wall)
            return;
    }
    int expected = 0;
    if (g_fast.compare_exchange_strong(expected, same ? 1 : -1, std::memory_order_acq_rel) && !same)
        BL_WARN("tiles: TileData statics are not at Il2CppClass+0x%zx; tile reads use the slow path", kClassStaticFields);
}

bool readTable(FieldInfo* info, size_t at, TileTable* out) {
    const Fields& f = fields();
    if (!f.ok || !info || at == kNoOffset) return false;
    Il2CppArray* array = nullptr;
    if (g_fast.load(std::memory_order_acquire) > 0) {
        uint8_t* statics = staticsOf(f.main);
        array = statics ? fastRead<Il2CppArray*>(statics, at) : nullptr;
    } else {
        array = slowRead<Il2CppArray*>(info);
    }
    if (!array) return false;
    out->data = static_cast<const uint8_t*>(arrayData(array));
    out->count = static_cast<int>(array->length);
    return out->count > 0;
}

}

bool tileArrays(TileArrays* out) {
    const Fields& f = fields();
    if (!f.ok) return false;
    Il2CppObject* world = nullptr;
    const int fast = g_fast.load(std::memory_order_acquire);
    if (fast > 0) return readFast(f, out, &world);
    const bool result = readSlow(f, out, &world);
    if (fast == 0) verify(f, *out, world);
    return result;
}

bool tileLightedTable(TileTable* out) {
    const Fields& f = fields();
    return readTable(f.lighted, f.lightedAt, out);
}

bool tileSolidTable(TileTable* out) {
    const Fields& f = fields();
    return readTable(f.solid, f.solidAt, out);
}

bool worldTileSize(int* width, int* height) {
    const Fields& f = fields();
    if (!f.ok || !f.maxX || !f.maxY || f.maxXAt == kNoOffset || f.maxYAt == kNoOffset) return false;
    if (g_fast.load(std::memory_order_acquire) > 0) {
        uint8_t* statics = staticsOf(f.main);
        if (!statics) return false;
        *width = fastRead<int32_t>(statics, f.maxXAt);
        *height = fastRead<int32_t>(statics, f.maxYAt);
    } else {
        *width = slowRead<int32_t>(f.maxX);
        *height = slowRead<int32_t>(f.maxY);
    }
    return *width > 0 && *height > 0;
}

bool worldGrowsGrassUnderground() {
    const Fields& f = fields();
    if (!f.worldGen || !f.grassUnderground || f.grassUndergroundAt == kNoOffset) return true;
    if (g_fast.load(std::memory_order_acquire) > 0) {
        uint8_t* statics = staticsOf(f.worldGen);
        return !statics || fastRead<uint8_t>(statics, f.grassUndergroundAt) != 0;
    }
    return slowRead<uint8_t>(f.grassUnderground) != 0;
}

int tileTypeAt(int x, int y) {
    TileArrays t;
    if (x < 0 || y < 0 || !tileArrays(&t) || x >= t.width || y >= t.height) return -1;
    const uint32_t def = t.lookup[static_cast<int64_t>(t.width) * y + x];
    if (!(t.sHeader[def] & kTileActiveBit)) return -1;
    return t.type[def];
}

int wallTypeAt(int x, int y) {
    TileArrays t;
    if (x < 0 || y < 0 || !tileArrays(&t) || !t.wall || x >= t.width || y >= t.height) return -1;
    const uint16_t w = t.wall[static_cast<int64_t>(t.width) * y + x];
    return w ? w : -1;
}

}
