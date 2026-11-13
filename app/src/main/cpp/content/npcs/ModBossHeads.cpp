#include "content/npcs/ModBossHeads.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "content/common/ContentAssets.h"
#include "content/common/TypeTables.h"

#include <map>
#include <mutex>
#include <vector>

namespace bl::runtime {

namespace {

struct BossHead {
    std::string texture, assetName;
    int slot = -1;
    uint32_t asset = 0;
    bool tried = false;
};

std::mutex g_mx;
std::vector<BossHead> g_heads;
std::map<int, int> g_typeSlot;
int g_base = -1;
uint32_t g_fallback = 0;
bool g_installed = false;
bool g_active = false;

struct Refs {
    bool tried = false, ok = false;
    Il2CppClass* textures = nullptr;
    FieldInfo* npcHeadBoss = nullptr;
    FieldInfo* bossHeads = nullptr;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    r.textures = findClass({"Terraria.GameContent", "TextureAssets", {}});
    Il2CppClass* sets = findClass({"Terraria.ID", "NPCID", "Sets"});
    r.npcHeadBoss = r.textures ? findField(r.textures, "NpcHeadBoss") : nullptr;
    r.bossHeads = sets ? findField(sets, "BossHeadTextures") : nullptr;
    r.ok = r.npcHeadBoss && r.bossHeads;
    if (!r.ok) {
        BL_ERROR("mod boss icons: references missing (TextureAssets.NpcHeadBoss=%p NPCID.Sets.BossHeadTextures=%p)",
                 (void*)r.npcHeadBoss, (void*)r.bossHeads);
    }
    return r;
}

Il2CppArray* readStatic(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    if (f) il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

int vanillaCount() {
    const Refs& r = refs();
    if (!r.ok) return -1;
    Il2CppArray* heads = readStatic(r.npcHeadBoss);
    if (!heads && il2cpp::api().runtime_class_init) {
        il2cpp::api().runtime_class_init(r.textures);
        heads = readStatic(r.npcHeadBoss);
    }
    if (!heads) {
        BL_ERROR("mod boss icons: TextureAssets.NpcHeadBoss is null; mod bosses get no bar and no icon");
        return -1;
    }
    return static_cast<int>(heads->length);
}

int total() { return g_base + static_cast<int>(g_heads.size()); }

bool applyTextures() {
    const Refs& r = refs();
    Il2CppArray* heads = readStatic(r.npcHeadBoss);
    if (!heads) return false;
    if (heads->length >= static_cast<uintptr_t>(total())) return false;

    Il2CppArray* bigger = TypeTables::resizedCopy(heads, static_cast<uintptr_t>(total()));
    if (!bigger) return false;
    il2cpp::api().field_static_set_value(r.npcHeadBoss, bigger);
    for (const BossHead& h : g_heads) {
        const uint32_t asset = h.asset ? h.asset : g_fallback;
        if (!asset) continue;
        content::setTableElement(r.npcHeadBoss, h.slot, il2cpp::api().gchandle_get_target(asset));
    }
    return true;
}

bool applyIndices() {
    Il2CppArray* table = readStatic(refs().bossHeads);
    if (!table) return false;
    auto* ids = static_cast<int32_t*>(arrayData(table));
    bool changed = false;
    for (const auto& [type, slot] : g_typeSlot) {
        if (type < 0 || static_cast<uintptr_t>(type) >= table->length) continue;
        if (!g_heads[static_cast<size_t>(slot - g_base)].asset) continue;
        if (ids[type] == slot) continue;
        ids[type] = slot;
        changed = true;
    }
    return changed;
}

int loadPending() {
    int loaded = 0;
    for (BossHead& h : g_heads) {
        if (h.tried) continue;
        h.tried = true;
        int w = 0, hgt = 0;
        Il2CppObject* asset = content::loadTextureAsset(h.texture, nullptr, 0, h.assetName, &w, &hgt);
        if (!asset) {
            BL_ERROR("mod boss icons: could not load %s (slot %d)", h.texture.c_str(), h.slot);
            continue;
        }
        h.asset = il2cpp::api().gchandle_new(asset, false);
        ++loaded;
    }
    return loaded;
}

}

int reserveBossHead(const std::string& texturePath, const std::string& assetName, int npcType) {
    std::lock_guard<std::mutex> l(g_mx);
    if (g_base < 0) g_base = vanillaCount();
    if (g_base < 0) return -1;

    int slot = -1;
    for (const BossHead& h : g_heads) {
        if (h.texture == texturePath) slot = h.slot;
    }
    if (slot < 0) {
        slot = total();
        g_heads.push_back({texturePath, assetName, slot});
    }
    if (npcType >= 0) g_typeSlot[npcType] = slot;
    return slot;
}

int bossHeadSlotOf(const std::string& texturePath) {
    std::lock_guard<std::mutex> l(g_mx);
    for (const BossHead& h : g_heads) {
        if (h.texture == texturePath) return h.slot;
    }
    return -1;
}

void installBossHeads() {
    std::lock_guard<std::mutex> l(g_mx);
    if (g_installed) return;
    g_installed = true;
    if (!refs().ok) return;

    Il2CppArray* heads = readStatic(refs().npcHeadBoss);
    if (!heads) {
        BL_ERROR("mod boss icons: TextureAssets.NpcHeadBoss is null; mod bosses get no bar and no icon");
        return;
    }
    if (g_base < 0) g_base = static_cast<int>(heads->length);
    if (heads->length != static_cast<uintptr_t>(g_base)) {
        BL_ERROR("mod boss icons: the game has %d icons, the slots were reserved from %d",
                 static_cast<int>(heads->length), g_base);
        if (heads->length > static_cast<uintptr_t>(g_base)) return;
    }
    if (heads->length > 0) {
        Il2CppObject* first = static_cast<Il2CppObject**>(arrayData(heads))[0];
        if (first) g_fallback = il2cpp::api().gchandle_new(first, false);
    }

    const int loaded = loadPending();
    g_active = true;
    applyTextures();
    applyIndices();
    if (!g_heads.empty()) {
        BL_DEBUG("mod boss icons: %d of %zu in TextureAssets.NpcHeadBoss (%d..%d)",
                 loaded, g_heads.size(), g_base, total() - 1);
    }
}

void watchBossHeads() {
    std::lock_guard<std::mutex> l(g_mx);
    if (!g_active) return;
    if (loadPending() > 0) BL_DEBUG("mod boss icons: icons requested after installation loaded");
    if (applyTextures()) BL_DEBUG("mod boss icons: the game rebuilt TextureAssets.NpcHeadBoss; icons put back");
    if (applyIndices()) BL_DEBUG("mod boss icons: NPCID.Sets.BossHeadTextures rebuilt; indices put back");
}

}
