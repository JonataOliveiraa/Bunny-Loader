#include "content/tiles/TileAccess.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "content/common/GameRefs.h"

#include <mutex>

namespace bl::runtime {

namespace {

struct Fields {
    bool ok = false;
    FieldInfo* world = nullptr;   // Main.tile (o TileData do mundo)
    int32_t width = -1, height = -1;
    FieldInfo *lookup = nullptr, *type = nullptr, *sHeader = nullptr, *frameX = nullptr,
              *frameY = nullptr, *bHeader = nullptr, *bHeader2 = nullptr, *bHeader3 = nullptr;
};

const Fields& fields() {
    static Fields f;
    static std::once_flag once;
    std::call_once(once, [] {
        using namespace il2cpp;
        Il2CppClass* data = findClass({"Terraria", "TileData", {}});
        Il2CppClass* main = findClass({"Terraria", "Main", {}});
        if (!data || !main) return;
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
        f.ok = f.world && f.width >= 0 && f.height >= 0 && f.lookup && f.type && f.sHeader &&
               f.frameX && f.frameY && f.bHeader && f.bHeader2 && f.bHeader3;
        if (!f.ok) BL_ERROR("tiles: campos do TileData nao achados; tiles de mod desligados");
    });
    return f;
}

template <typename T>
T* staticPtr(FieldInfo* f) {
    void* p = nullptr;
    il2cpp::api().field_static_get_value(f, &p);
    return static_cast<T*>(p);
}

} // namespace

bool tileArrays(TileArrays* out) {
    const Fields& f = fields();
    if (!f.ok) return false;
    Il2CppObject* world = nullptr;
    il2cpp::api().field_static_get_value(f.world, &world);
    if (!world) return false;
    out->width = field<int32_t>(world, f.width);
    out->height = field<int32_t>(world, f.height);
    out->lookup = staticPtr<uint32_t>(f.lookup);
    out->type = staticPtr<uint16_t>(f.type);
    out->sHeader = staticPtr<int16_t>(f.sHeader);
    out->frameX = staticPtr<int16_t>(f.frameX);
    out->frameY = staticPtr<int16_t>(f.frameY);
    out->bHeader = staticPtr<uint8_t>(f.bHeader);
    out->bHeader2 = staticPtr<uint8_t>(f.bHeader2);
    out->bHeader3 = staticPtr<uint8_t>(f.bHeader3);
    return out->width > 0 && out->height > 0 && out->lookup && out->type && out->sHeader;
}

int tileTypeAtOffset(int32_t offset) {
    TileArrays t;
    if (offset < 0 || !tileArrays(&t)) return -1;
    if (static_cast<int64_t>(offset) >= static_cast<int64_t>(t.width) * t.height) return -1;
    const uint32_t def = t.lookup[offset];
    if (!(t.sHeader[def] & kTileActiveBit)) return -1;
    return t.type[def];
}

int tileTypeAt(int x, int y) {
    TileArrays t;
    if (x < 0 || y < 0 || !tileArrays(&t) || x >= t.width || y >= t.height) return -1;
    const uint32_t def = t.lookup[static_cast<int64_t>(t.width) * y + x];
    if (!(t.sHeader[def] & kTileActiveBit)) return -1;
    return t.type[def];
}

} // namespace bl::runtime
