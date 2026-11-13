#include "content/npcs/ModTownNpcs.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "hook/CodePatch.h"
#include "content/common/ContentAssets.h"
#include "content/common/GameRefs.h"
#include "content/npcs/ModNpcs.h"
#include "content/common/TypeTables.h"

#include <cstring>
#include <mutex>
#include <unordered_map>
#include <vector>

namespace bl::runtime {

namespace {

constexpr uint32_t kArrayHeader = 32;

constexpr int kVanillaHeads = 81;

struct Head {
    int type;
    int variant;
    std::string texture, assetName;
    int slot = -1;
    uint32_t asset = 0;
};

std::mutex g_mx;
std::vector<Head> g_heads;
std::unordered_map<int, int> g_slotOf;
int g_vanillaHeads = 0;
int g_headTotal = 0;
bool g_installed = false;

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* npcHead = nullptr;
    FieldInfo* cannotDraw = nullptr;
    FieldInfo* headOrder = nullptr;
    FieldInfo* mainInstance = nullptr;
    FieldInfo* townManager = nullptr;
    int32_t whoHoldsHead = -1;
    int32_t hasRoom = -1;
    const MethodInfo* headIndex = nullptr;
    Il2CppClass* headSets = nullptr;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* textures = findClass({"Terraria.GameContent", "TextureAssets", {}});
    Il2CppClass* headSets = findClass({"Terraria.ID", "NPCHeadID", "Sets"});
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* worldGen = findClass({"Terraria", "WorldGen", {}});
    Il2CppClass* rooms = findClass({"Terraria.GameContent", "TownRoomManager", {}});
    Il2CppClass* npc = findClass({"Terraria", "NPC", {}});
    r.npcHead = textures ? findField(textures, "NpcHead") : nullptr;
    r.cannotDraw = headSets ? findField(headSets, "CannotBeDrawnInHousingUI") : nullptr;
    r.headOrder = headSets ? findField(headSets, "HeadListOrder") : nullptr;
    r.mainInstance = main ? findField(main, "instance") : nullptr;
    r.townManager = worldGen ? findField(worldGen, "TownManager") : nullptr;
    r.whoHoldsHead = main ? fieldOffset(main, "_npcIndexWhoHoldsHeadIndex") : -1;
    r.hasRoom = rooms ? fieldOffset(rooms, "_hasRoom") : -1;
    r.headIndex = npc ? a.class_get_method_from_name(npc, "TypeToDefaultHeadIndex", 1) : nullptr;
    r.headSets = headSets;
    r.ok = r.npcHead && r.cannotDraw && r.headOrder && r.mainInstance && r.townManager &&
           r.whoHoldsHead >= 0 && r.hasRoom >= 0 && r.headIndex;
    if (!r.ok) {
        BL_ERROR("mod town NPCs: references missing (NpcHead=%p HeadListOrder=%p TownManager=%p "
                 "_hasRoom=%d TypeToDefaultHeadIndex=%p)", (void*)r.npcHead, (void*)r.headOrder,
                 (void*)r.townManager, r.hasRoom, (void*)r.headIndex);
    }
    return r;
}

Il2CppArray* readStatic(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    if (f) il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

Il2CppObject* readStaticObject(FieldInfo* f) {
    Il2CppObject* obj = nullptr;
    if (f) il2cpp::api().field_static_get_value(f, &obj);
    return obj;
}

using HeadIndexFn = int32_t (*)(int32_t, const MethodInfo*);
HeadIndexFn g_origHeadIndex = nullptr;

int32_t hkHeadIndex(int32_t type, const MethodInfo* m) {
    if (type >= kVanillaNpcCount) {
        auto it = g_slotOf.find(type);
        return it != g_slotOf.end() ? it->second : 0;
    }
    return g_origHeadIndex(type, m);
}

bool applyHeads() {
    const Refs& r = refs();
    Il2CppArray* heads = readStatic(r.npcHead);
    if (!heads) return false;
    const int total = g_headTotal;
    if (heads->length < static_cast<uintptr_t>(total)) {
        Il2CppArray* bigger = TypeTables::resizedCopy(heads, static_cast<uintptr_t>(total));
        if (!bigger) return false;
        il2cpp::api().field_static_set_value(r.npcHead, bigger);
    }
    for (const Head& h : g_heads) {
        if (h.slot < 0 || !h.asset) continue;
        content::setTableElement(r.npcHead, h.slot, il2cpp::api().gchandle_get_target(h.asset));
    }
    return true;
}

void growHeadTables(int total) {
    const Refs& r = refs();
    auto& a = il2cpp::api();
    if (Il2CppArray* cannot = readStatic(r.cannotDraw);
        cannot && cannot->length < static_cast<uintptr_t>(total)) {
        if (Il2CppArray* bigger = TypeTables::resizedCopy(cannot, static_cast<uintptr_t>(total))) {
            a.field_static_set_value(r.cannotDraw, bigger);
        }
    }
    if (Il2CppArray* order = readStatic(r.headOrder)) {
        auto* ids = static_cast<int32_t*>(arrayData(order));
        std::vector<int> missing;
        for (const Head& h : g_heads) {
            bool found = false;
            for (uintptr_t i = 0; i < order->length && !found; ++i) found = ids[i] == h.slot;
            if (!found && h.slot >= 0) missing.push_back(h.slot);
        }
        if (!missing.empty()) {
            const uintptr_t n = order->length;
            if (Il2CppArray* bigger = TypeTables::resizedCopy(order, n + missing.size())) {
                auto* out = static_cast<int32_t*>(arrayData(bigger));
                for (size_t i = 0; i < missing.size(); ++i) out[n + i] = missing[i];
                a.field_static_set_value(r.headOrder, bigger);
            }
        }
    }
    if (Il2CppObject* main = readStaticObject(r.mainInstance)) {
        TypeTables::growInstanceTable(main, r.whoHoldsHead, g_vanillaHeads, total,
                                      "Main._npcIndexWhoHoldsHeadIndex");
    }
}

struct LoopPatch {
    const char* ns;
    const char* cls;
    const char* method;
    bool loopEnd;
};

constexpr LoopPatch kLoopPatches[] = {
    {"Terraria", "WorldGen", "IsThereASpawnablePrioritizedTownNPC", true},
    {"Terraria", "WorldGen", "IsThereASpawnablePrioritizedTownNPC_Old", true},
    {"Terraria", "Main", "UpdateTime_SpawnTownNPCs", true},
    {"Terraria.GameContent", "ShopHelper", "ProcessMood", false},
    {"Terraria.GameContent", "TownRoomManager", ".ctor", false},
};

}

int setModNpcHead(int type, int variant, const std::string& texturePath, const std::string& assetName) {
    std::lock_guard<std::mutex> l(g_mx);
    for (Head& h : g_heads) {
        if (h.type == type && h.variant == variant) {
            h.texture = texturePath;
            h.assetName = assetName;
            return h.slot;
        }
    }
    Head head{type, variant, texturePath, assetName};
    head.slot = kVanillaHeads + static_cast<int>(g_heads.size());
    g_heads.push_back(head);
    return head.slot;
}

int modNpcHeadSlot(int type, int variant) {
    std::lock_guard<std::mutex> l(g_mx);
    for (const Head& h : g_heads) {
        if (h.type == type && h.variant == variant) return h.slot;
    }
    return -1;
}

void prepareTownNpcs(int totalTypes) {
    static bool done = false;
    if (done || totalTypes <= kVanillaNpcCount) return;
    done = true;
    auto& a = il2cpp::api();
    for (const LoopPatch& p : kLoopPatches) {
        Il2CppClass* cls = il2cpp::findClass({p.ns, p.cls, {}});
        int patched = 0;
        void* it = nullptr;
        while (const MethodInfo* m = cls ? a.class_get_methods(cls, &it) : nullptr) {
            if (std::strcmp(a.method_get_name(m), p.method) != 0) continue;
            patched += p.loopEnd
                ? patchLoopEnd(m, kVanillaNpcCount + kArrayHeader, static_cast<uint32_t>(totalTypes) + kArrayHeader)
                : patchMovImmediate(m, kVanillaNpcCount, static_cast<uint32_t>(totalTypes));
        }
        if (patched > 0) {
            BL_DEBUG("mod town NPCs: %s.%s: %d swap(s) from %d to %d", p.cls, p.method, patched,
                     kVanillaNpcCount, totalTypes);
        } else {
            BL_ERROR("mod town NPCs: %s.%s: %d not found in the code; the mod town NPC goes without it",
                     p.cls, p.method, kVanillaNpcCount);
        }
    }
}

void installTownNpcs(int totalTypes) {
    if (g_installed) return;
    const Refs& r = refs();
    if (!r.ok) return;
    g_installed = true;

    if (Il2CppObject* rooms = readStaticObject(r.townManager)) {
        TypeTables::growInstanceTable(rooms, r.hasRoom, kVanillaNpcCount, totalTypes, "TownRoomManager._hasRoom");
    }

    if (il2cpp::api().runtime_class_init && r.headSets) il2cpp::api().runtime_class_init(r.headSets);
    Il2CppArray* heads = readStatic(r.npcHead);
    if (!heads) {
        BL_ERROR("mod town NPCs: TextureAssets.NpcHead is null; no mod heads");
        return;
    }
    std::lock_guard<std::mutex> l(g_mx);
    g_vanillaHeads = static_cast<int>(heads->length);
    if (g_vanillaHeads != kVanillaHeads) {
        BL_ERROR("town NPC heads: the game has %d heads, expected %d", g_vanillaHeads, kVanillaHeads);
    }
    for (Head& h : g_heads) {
        int w = 0, hgt = 0;
        Il2CppObject* asset = content::loadTextureAsset(h.texture, nullptr, 0, h.assetName, &w, &hgt);
        if (!asset) continue;
        h.asset = il2cpp::api().gchandle_new(asset, false);
        if (h.variant == 0) g_slotOf[h.type] = h.slot;
    }
    g_headTotal = kVanillaHeads + static_cast<int>(g_heads.size());
    if (!hook::install(r.headIndex, hkHeadIndex, &g_origHeadIndex)) {
        BL_ERROR("mod town NPCs: no hook on NPC.TypeToDefaultHeadIndex; mod town NPCs have no head");
        return;
    }
    if (g_heads.empty()) return;
    applyHeads();
    growHeadTables(g_headTotal);
    BL_DEBUG("mod town NPCs: %zu head(s) in TextureAssets.NpcHead (%d..%d)",
             g_heads.size(), g_vanillaHeads, g_headTotal - 1);
}

void watchTownNpcs() {
    if (!g_installed || g_heads.empty()) return;
    const Refs& r = refs();
    Il2CppArray* heads = readStatic(r.npcHead);
    const int total = g_headTotal;
    {
        std::lock_guard<std::mutex> l(g_mx);
        if (heads && heads->length < static_cast<uintptr_t>(total)) {
            applyHeads();
            BL_DEBUG("mod town NPCs: the game rebuilt TextureAssets.NpcHead; mod heads put back");
        }
        growHeadTables(total);
    }
    if (Il2CppObject* rooms = readStaticObject(r.townManager)) {
        TypeTables::growInstanceTable(rooms, r.hasRoom, kVanillaNpcCount, npcTypeCount(), "TownRoomManager._hasRoom");
    }
}

}
