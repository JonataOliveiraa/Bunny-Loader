#include "content/npcs/ModBossHeads.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "content/common/ContentAssets.h"
#include "content/common/TypeTables.h"

#include <mutex>
#include <vector>

namespace bl::runtime {

namespace {

struct BossHead {
    int type;
    std::string texture, assetName;
    int slot = -1;
    uint32_t asset = 0;   // gchandle do Asset<Texture2D>
};

std::mutex g_mx;
std::vector<BossHead> g_heads;
int g_total = 0;          // NpcHeadBoss com os de mod (os que carregaram)
bool g_installed = false;

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* npcHeadBoss = nullptr;   // TextureAssets.NpcHeadBoss
    FieldInfo* bossHeads = nullptr;     // NPCID.Sets.BossHeadTextures (int[NPCID.Count])
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    Il2CppClass* textures = findClass({"Terraria.GameContent", "TextureAssets", {}});
    Il2CppClass* sets = findClass({"Terraria.ID", "NPCID", "Sets"});
    r.npcHeadBoss = textures ? findField(textures, "NpcHeadBoss") : nullptr;
    r.bossHeads = sets ? findField(sets, "BossHeadTextures") : nullptr;
    r.ok = r.npcHeadBoss && r.bossHeads;
    if (!r.ok) {
        BL_ERROR("icone de chefe de mod: refs faltando (TextureAssets.NpcHeadBoss=%p NPCID.Sets.BossHeadTextures=%p)",
                 (void*)r.npcHeadBoss, (void*)r.bossHeads);
    }
    return r;
}

Il2CppArray* readStatic(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    if (f) il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

/** NpcHeadBoss com os icones de mod no fim; true se mexeu. */
bool applyTextures() {
    const Refs& r = refs();
    Il2CppArray* heads = readStatic(r.npcHeadBoss);
    if (!heads) return false;
    if (heads->length >= static_cast<uintptr_t>(g_total)) return false;

    Il2CppArray* bigger = TypeTables::resizedCopy(heads, static_cast<uintptr_t>(g_total));
    if (!bigger) return false;
    il2cpp::api().field_static_set_value(r.npcHeadBoss, bigger);
    for (const BossHead& h : g_heads) {
        if (h.slot < 0 || !h.asset) continue;
        content::setTableElement(r.npcHeadBoss, h.slot, il2cpp::api().gchandle_get_target(h.asset));
    }
    return true;
}

/** BossHeadTextures[type] = slot; true se algum estava diferente. */
bool applyIndices() {
    Il2CppArray* table = readStatic(refs().bossHeads);
    if (!table) return false;
    auto* ids = static_cast<int32_t*>(arrayData(table));
    bool changed = false;
    for (const BossHead& h : g_heads) {
        if (h.slot < 0 || h.type < 0 || static_cast<uintptr_t>(h.type) >= table->length) continue;
        if (ids[h.type] == h.slot) continue;
        ids[h.type] = h.slot;
        changed = true;
    }
    return changed;
}

} // namespace

void setModBossHead(int type, const std::string& texturePath, const std::string& assetName) {
    std::lock_guard<std::mutex> l(g_mx);
    for (BossHead& h : g_heads) {
        if (h.type != type) continue;
        h.texture = texturePath;
        h.assetName = assetName;
        return;
    }
    g_heads.push_back({type, texturePath, assetName});
}

void installBossHeads() {
    if (g_installed) return;
    const Refs& r = refs();
    if (!r.ok) return;
    g_installed = true;

    std::lock_guard<std::mutex> l(g_mx);
    if (g_heads.empty()) return;
    Il2CppArray* heads = readStatic(r.npcHeadBoss);
    if (!heads) {
        BL_ERROR("icone de chefe de mod: TextureAssets.NpcHeadBoss nulo; chefe de mod sem barra e sem icone");
        return;
    }

    const int vanilla = static_cast<int>(heads->length);
    int slot = vanilla;
    for (BossHead& h : g_heads) {
        int w = 0, hgt = 0;
        Il2CppObject* asset = content::loadTextureAsset(h.texture, nullptr, 0, h.assetName, &w, &hgt);
        if (!asset) continue;
        h.asset = il2cpp::api().gchandle_new(asset, false);
        h.slot = slot++;
    }
    g_total = slot;
    applyTextures();
    applyIndices();
    BL_DEBUG("icone de chefe de mod: %d em TextureAssets.NpcHeadBoss (%d..%d)", slot - vanilla, vanilla, slot - 1);
}

void watchBossHeads() {
    if (!g_installed) return;
    std::lock_guard<std::mutex> l(g_mx);
    if (g_total == 0) return;
    if (applyTextures()) BL_DEBUG("icone de chefe de mod: o jogo refez TextureAssets.NpcHeadBoss; icones devolvidos");
    if (applyIndices()) BL_DEBUG("icone de chefe de mod: NPCID.Sets.BossHeadTextures refeita; indices devolvidos");
}

} // namespace bl::runtime
