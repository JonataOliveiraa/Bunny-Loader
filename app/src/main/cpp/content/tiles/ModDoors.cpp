#include "content/tiles/ModDoors.h"
#include "content/tiles/ModTiles.h"
#include "content/tiles/TileAccess.h"
#include "content/common/GameRefs.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <atomic>
#include <cstdint>
#include <mutex>
#include <vector>

namespace bl::runtime {

namespace {

constexpr int kClosedDoor = 10;
constexpr int kOpenDoor = 11;
constexpr size_t kMaxDoorTypes = 8192;

std::atomic<uint16_t> g_partner[kMaxDoorTypes];
std::atomic<uint8_t> g_isOpen[kMaxDoorTypes];

int partnerOf(int type) {
    if (type < kVanillaTileCount) return 0;
    const size_t i = static_cast<size_t>(type - kVanillaTileCount);
    return i < kMaxDoorTypes ? g_partner[i].load(std::memory_order_relaxed) : 0;
}

int vanillaOf(int modDoor) {
    return g_isOpen[modDoor - kVanillaTileCount].load(std::memory_order_relaxed) ? kOpenDoor : kClosedDoor;
}

struct Refs {
    int32_t position = -1, width = -1, height = -1;
    const MethodInfo* setType = nullptr;
    const MethodInfo* killTile = nullptr;
};
Refs g_refs;

void setType(int32_t index, int type) {
    using Fn = void (*)(int32_t, uint16_t, const MethodInfo*);
    reinterpret_cast<Fn>(il2cpp::methodPointer(g_refs.setType))(index, static_cast<uint16_t>(type), g_refs.setType);
}

int typeAtIndex(const TileArrays& t, int32_t index) {
    const uint32_t def = t.lookup[index];
    return (t.sHeader[def] & kTileActiveBit) ? t.type[def] : -1;
}

class Disguise {
public:
    Disguise(int cx, int cy, int rx, int ry) : prev_(s_active) {
        s_active = this;
        TileArrays t;
        if (!tileArrays(&t)) return;
        width_ = t.width;
        const int x0 = cx - rx < 1 ? 1 : cx - rx, x1 = cx + rx > t.width - 2 ? t.width - 2 : cx + rx;
        const int y0 = cy - ry < 0 ? 0 : cy - ry, y1 = cy + ry > t.height - 1 ? t.height - 1 : cy + ry;
        for (int y = y0; y <= y1; ++y) {
            for (int x = x0; x <= x1; ++x) {
                const int32_t index = t.width * y + x;
                const int type = typeAtIndex(t, index);
                if (type < 0 || !partnerOf(type)) continue;
                for (int dx = -1; dx <= 1; ++dx) {
                    cells_.push_back({index + dx, typeAtIndex(t, index + dx), static_cast<uint16_t>(type), dx == 0});
                }
            }
        }
        if (!cells_.empty()) dress();
    }

    ~Disguise() {
        s_active = prev_;
        if (cells_.empty()) return;
        TileArrays t;
        if (!tileArrays(&t)) return;

        std::vector<std::pair<int32_t, int>> writes;
        for (const Cell& c : cells_) {
            const int now = typeAtIndex(t, c.index);
            const int other = partnerOf(c.owner);
            const int asOwner = vanillaOf(c.owner), asOther = vanillaOf(other);
            if (c.own) {
                if (now == asOwner) writes.push_back({c.index, c.owner});
                else if (now == asOther) writes.push_back({c.index, other});
            } else if (now == asOther && now != c.prev && c.prev != kClosedDoor && c.prev != kOpenDoor &&
                       !partnerOf(c.prev)) {
                writes.push_back({c.index, other});
            }
        }
        for (const auto& w : writes) setType(w.first, w.second);
    }

    Disguise(const Disguise&) = delete;
    Disguise& operator=(const Disguise&) = delete;

    static Disguise* active() { return s_active; }

    bool covers(int x, int y) const {
        const int32_t index = width_ * y + x;
        for (const Cell& c : cells_) {
            if (c.own && c.index == index) return true;
        }
        return false;
    }

    void undress() {
        TileArrays t;
        if (!tileArrays(&t)) return;
        std::vector<std::pair<int32_t, int>> writes;
        for (const Cell& c : cells_) {
            if (c.own && typeAtIndex(t, c.index) == vanillaOf(c.owner)) writes.push_back({c.index, c.owner});
        }
        for (const auto& w : writes) setType(w.first, w.second);
    }

    void dress() {
        TileArrays t;
        if (!tileArrays(&t)) return;
        std::vector<std::pair<int32_t, int>> writes;
        for (const Cell& c : cells_) {
            if (c.own && typeAtIndex(t, c.index) == c.owner) writes.push_back({c.index, vanillaOf(c.owner)});
        }
        for (const auto& w : writes) setType(w.first, w.second);
    }

private:
    struct Cell {
        int32_t index;
        int prev;
        uint16_t owner;
        bool own;
    };

    static inline Disguise* s_active = nullptr;
    Disguise* prev_;
    int width_ = 0;
    std::vector<Cell> cells_;
};

void centerOf(Il2CppObject* entity, int* x, int* y) {
    const float px = field<float>(entity, g_refs.position);
    const float py = field<float>(entity, g_refs.position + 4);
    *x = static_cast<int>((px + field<int32_t>(entity, g_refs.width) / 2.0f) / 16.0f);
    *y = static_cast<int>((py + field<int32_t>(entity, g_refs.height) / 2.0f) / 16.0f);
}

using HelperUpdateFn = void (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
HelperUpdateFn g_origHelperUpdate = nullptr;

void hkHelperUpdate(Il2CppObject* self, Il2CppObject* player, const MethodInfo* m) {
    if (!player) return g_origHelperUpdate(self, player, m);
    int x = 0, y = 0;
    centerOf(player, &x, &y);
    Disguise d(x, y, 6, 4);
    g_origHelperUpdate(self, player, m);
}

using AiFn = void (*)(Il2CppObject*, const MethodInfo*);
AiFn g_origTownAi = nullptr, g_origFighterAi = nullptr, g_origWalkerAi = nullptr;

template <AiFn* Orig>
void hkNpcAi(Il2CppObject* self, const MethodInfo* m) {
    int x = 0, y = 0;
    centerOf(self, &x, &y);
    Disguise d(x, y, 5, 5);
    (*Orig)(self, m);
}

using KillTileFn = void (*)(int32_t, int32_t, bool, bool, bool, const MethodInfo*);
KillTileFn g_origKillTile = nullptr;
bool g_inKill = false;

void hkKillTile(int32_t i, int32_t j, bool fail, bool effectOnly, bool noItem, const MethodInfo* m) {
    Disguise* d = Disguise::active();
    if (!d || g_inKill || !d->covers(i, j)) return g_origKillTile(i, j, fail, effectOnly, noItem, m);
    d->undress();
    g_inKill = true;
    const MethodInfo* entry = g_refs.killTile;
    reinterpret_cast<KillTileFn>(il2cpp::methodPointer(entry))(i, j, fail, effectOnly, noItem, entry);
    g_inKill = false;
    d->dress();
}

const MethodInfo* methodOf(Il2CppClass* cls, const char* name, int params) {
    return cls ? il2cpp::api().class_get_method_from_name(cls, name, params) : nullptr;
}

void installHooks() {
    using namespace il2cpp;
    Il2CppClass* entity = findClass({"Terraria", "Entity", {}});
    Il2CppClass* data = findClass({"Terraria", "TileData", {}});
    Il2CppClass* helper = findClass({"Terraria.GameContent", "DoorOpeningHelper", {}});
    Il2CppClass* npc = findClass({"Terraria", "NPC", {}});
    Il2CppClass* worldGen = findClass({"Terraria", "WorldGen", {}});
    g_refs.position = entity ? fieldOffset(entity, "position") : -1;
    g_refs.width = entity ? fieldOffset(entity, "width") : -1;
    g_refs.height = entity ? fieldOffset(entity, "height") : -1;
    g_refs.setType = methodOf(data, "SetType", 2);
    if (g_refs.position < 0 || g_refs.width < 0 || g_refs.height < 0 || !g_refs.setType) {
        BL_ERROR("portas de mod: Entity.position/width/height ou TileData.SetType nao achados; "
                 "jogador e NPCs nao abrem porta de mod sozinhos");
        return;
    }

    g_refs.killTile = methodOf(worldGen, "KillTile", 5);
    if (!g_refs.killTile || !hook::install(g_refs.killTile, hkKillTile, &g_origKillTile)) {
        BL_ERROR("portas de mod: sem hook no WorldGen.KillTile; NPCs nao abrem porta de mod sozinhos");
        return;
    }
    if (!hook::install(methodOf(helper, "Update", 1), hkHelperUpdate, &g_origHelperUpdate)) {
        BL_ERROR("portas de mod: sem hook no DoorOpeningHelper.Update; a porta de mod nao abre ao encostar");
    }
    const struct { const char* name; AiFn hook; AiFn* orig; } ais[] = {
        {"AI_007_TownEntities", hkNpcAi<&g_origTownAi>, &g_origTownAi},
        {"AI_003_Fighters", hkNpcAi<&g_origFighterAi>, &g_origFighterAi},
        {"AI_107_ImprovedWalkers", hkNpcAi<&g_origWalkerAi>, &g_origWalkerAi},
    };
    for (const auto& ai : ais) {
        if (!hook::install(methodOf(npc, ai.name, 0), ai.hook, ai.orig)) {
            BL_ERROR("portas de mod: sem hook no NPC.%s; esses NPCs nao abrem porta de mod", ai.name);
        }
    }
}

}

void setModDoorPair(int closedType, int openType) {
    if (closedType < kVanillaTileCount || openType < kVanillaTileCount) return;
    const size_t c = static_cast<size_t>(closedType - kVanillaTileCount);
    const size_t o = static_cast<size_t>(openType - kVanillaTileCount);
    if (c >= kMaxDoorTypes || o >= kMaxDoorTypes) return;
    g_isOpen[c].store(0, std::memory_order_relaxed);
    g_isOpen[o].store(1, std::memory_order_relaxed);
    g_partner[c].store(static_cast<uint16_t>(openType), std::memory_order_relaxed);
    g_partner[o].store(static_cast<uint16_t>(closedType), std::memory_order_release);

    static std::once_flag once;
    std::call_once(once, installHooks);
}

}
