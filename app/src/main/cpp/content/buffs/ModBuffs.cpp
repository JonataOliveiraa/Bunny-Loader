#include "content/buffs/ModBuffs.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "hook/CodePatch.h"
#include "content/common/ContentAssets.h"
#include "content/common/ModLanguage.h"
#include "content/common/GameRefs.h"
#include "content/common/TypeTables.h"

#include <atomic>
#include <cstring>
#include <mutex>

namespace bl::runtime {

namespace {

struct Entry {
    ModBuffDef def;
    uint32_t asset = 0;
};

std::mutex g_mx;
std::vector<Entry> g_regs;
std::mutex g_hooksMx;
std::vector<BuffsInstalledHook> g_installedHooks;
std::atomic<int> g_total{0};
std::atomic<int> g_installed{0};
bool g_failed = false;

TypeTables g_tables("mod buffs", kVanillaBuffCount, {
    {"Terraria.ID", "BuffID", "Sets"},
    {"Terraria", "Main", "", "", {"item", "timeItemSlotCannotBeReusedFor", "itemFrame", "itemFrameCounter"}},
    {"Terraria", "Lang", ""},
    {"Terraria.GameContent", "TextureAssets", ""},
});

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* textures = nullptr;
    FieldInfo* names = nullptr;
    FieldInfo* descriptions = nullptr;
    FieldInfo* noSave = nullptr;
    FieldInfo* players = nullptr;
    FieldInfo* npcs = nullptr;
    int32_t playerImmune = -1, npcImmune = -1;
    int32_t buffType = -1, buffTime = -1;
    const MethodInfo* playerCtor = nullptr;
    const MethodInfo* npcCtor = nullptr;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* textures = findClass({"Terraria.GameContent", "TextureAssets", {}});
    Il2CppClass* lang = findClass({"Terraria", "Lang", {}});
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* player = findClass({"Terraria", "Player", {}});
    Il2CppClass* npc = findClass({"Terraria", "NPC", {}});
    r.textures = textures ? findField(textures, "Buff") : nullptr;
    r.names = lang ? findField(lang, "_buffNameCache") : nullptr;
    r.descriptions = lang ? findField(lang, "_buffDescriptionCache") : nullptr;
    r.noSave = main ? findField(main, "buffNoSave") : nullptr;
    r.players = main ? findField(main, "player") : nullptr;
    r.npcs = main ? findField(main, "npc") : nullptr;
    r.playerImmune = player ? fieldOffset(player, "buffImmune") : -1;
    r.npcImmune = npc ? fieldOffset(npc, "buffImmune") : -1;
    r.buffType = player ? fieldOffset(player, "buffType") : -1;
    r.buffTime = player ? fieldOffset(player, "buffTime") : -1;
    r.playerCtor = player ? a.class_get_method_from_name(player, ".ctor", 0) : nullptr;
    r.npcCtor = npc ? a.class_get_method_from_name(npc, ".ctor", 0) : nullptr;
    r.ok = r.textures && r.names && r.descriptions && r.noSave && r.players && r.npcs &&
           r.playerImmune >= 0 && r.npcImmune >= 0 && r.buffType >= 0 && r.buffTime >= 0 &&
           r.playerCtor && r.npcCtor;
    if (!r.ok) {
        BL_ERROR("mod buffs: references missing (TextureAssets.Buff=%p Lang._buffNameCache=%p "
                 "Player.buffImmune=%d NPC.buffImmune=%d)",
                 (void*)r.textures, (void*)r.names, r.playerImmune, r.npcImmune);
    }
    return r;
}

Il2CppArray* readStatic(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

void applyTexts(int type, const Entry& e) {
    if (Il2CppObject* t = trackedLanguageText("BuffName." + e.def.name, e.def.names, e.def.name)) {
        content::setTableElement(refs().names, type, t);
    }
    if (Il2CppObject* t = trackedLanguageText("BuffDescription." + e.def.name, e.def.descriptions, "")) {
        content::setTableElement(refs().descriptions, type, t);
    }
}

void applyTexture(int type, const Entry& e) {
    Il2CppObject* asset = e.asset ? il2cpp::api().gchandle_get_target(e.asset) : nullptr;
    if (asset) content::setTableElement(refs().textures, type, asset);
}

void onTableRegrown(FieldInfo* f, int size) {
    const Refs& r = refs();
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size() && static_cast<int>(i) < size - kVanillaBuffCount; ++i) {
        const int type = kVanillaBuffCount + static_cast<int>(i);
        if (f == r.names || f == r.descriptions) applyTexts(type, g_regs[i]);
        if (f == r.textures) applyTexture(type, g_regs[i]);
    }
}

bool gameReady(int size) {
    const Refs& r = refs();
    if (!r.ok) return false;
    Il2CppArray* textures = readStatic(r.textures);
    Il2CppArray* names = readStatic(r.names);
    return textures && names && readStatic(r.descriptions) &&
           textures->length == static_cast<uintptr_t>(size) && names->length >= static_cast<uintptr_t>(size);
}

struct LimitPatch {
    const char* ns;
    const char* cls;
    const char* method;
    uint32_t vanilla;
    int offset;
    bool belowToo;
};

constexpr LimitPatch kLimitPatches[] = {
    {"", "GUIBuffs", "Draw", kVanillaBuffCount - 2, -2, true},
    {"", "GUIBuffs", "Draw", kVanillaBuffCount, 0, true},
    {"Terraria", "Main", "GetNurseHealCost", kVanillaBuffCount - 1, -1, false},
    {"Terraria", "Main", "NPCChatText_DoNurseHeal", kVanillaBuffCount - 1, -1, false},
    {"", "GUINPCDialogue", "Option1Clicked", kVanillaBuffCount - 1, -1, false},
    {"", "GUINPCDialogue", "SetupButtonText", kVanillaBuffCount - 1, -1, false},
    {"Terraria", "NPC", "RequestBuffRemoval", kVanillaBuffCount - 1, -1, false},
};

void patchLimits(int total) {
    auto& a = il2cpp::api();
    int methodsOk = 0;
    for (const LimitPatch& p : kLimitPatches) {
        Il2CppClass* cls = il2cpp::findClass({p.ns, p.cls, {}});
        const uint32_t to = static_cast<uint32_t>(total + p.offset);
        int patched = 0;
        const MethodInfo* last = nullptr;
        void* it = nullptr;
        while (const MethodInfo* m = cls ? a.class_get_methods(cls, &it) : nullptr) {
            if (std::strcmp(a.method_get_name(m), p.method) != 0) continue;
            last = m;
            patched += patchCompareLimit(m, p.vanilla, to, p.belowToo);
        }
        if (patched > 0) {
            ++methodsOk;
            BL_DEBUG("mod buffs: %s.%s: %d limit(s) from %u to %u", p.cls, p.method, patched, p.vanilla, to);
        } else {
            BL_ERROR("mod buffs: %s.%s: limit %u not swapped: %s", p.cls, p.method, p.vanilla,
                     describeCompareMiss(last, p.vanilla, to, p.belowToo).c_str());
        }
    }
    const int methods = static_cast<int>(sizeof(kLimitPatches) / sizeof(kLimitPatches[0]));
    BL_INFO("mod buffs: compiled limits swapped in %d of %d method(s)", methodsOk, methods);
}

using CtorFn = void (*)(Il2CppObject*, const MethodInfo*);
CtorFn g_origPlayerCtor = nullptr;
CtorFn g_origNpcCtor = nullptr;

void hkPlayerCtor(Il2CppObject* self, const MethodInfo* m) {
    g_origPlayerCtor(self, m);
    if (buffTypeCount() > kVanillaBuffCount) {
        TypeTables::growInstanceTable(self, refs().playerImmune, kVanillaBuffCount, buffTypeCount(), nullptr);
    }
}

void hkNpcCtor(Il2CppObject* self, const MethodInfo* m) {
    g_origNpcCtor(self, m);
    if (buffTypeCount() > kVanillaBuffCount) {
        TypeTables::growInstanceTable(self, refs().npcImmune, kVanillaBuffCount, buffTypeCount(), nullptr);
    }
}

int growAll(FieldInfo* list, int32_t offset, int size) {
    Il2CppArray* arr = readStatic(list);
    int grown = 0;
    for (uintptr_t i = 0; arr && i < arr->length; ++i) {
        Il2CppObject* o = static_cast<Il2CppObject**>(arrayData(arr))[i];
        if (!o) continue;
        TypeTables::growInstanceTable(o, offset, kVanillaBuffCount, size, nullptr);
        ++grown;
    }
    return grown;
}

void growInstances(int size) {
    static bool hooked = false;
    const Refs& r = refs();
    if (!hooked) {
        hooked = true;
        if (!hook::install(r.playerCtor, hkPlayerCtor, &g_origPlayerCtor)) {
            BL_ERROR("mod buffs: no hook on the Player constructor; a new player reads past buffImmune");
        }
        if (!hook::install(r.npcCtor, hkNpcCtor, &g_origNpcCtor)) {
            BL_ERROR("mod buffs: no hook on the NPC constructor; a new NPC reads past buffImmune");
        }
    }
    const int players = growAll(r.players, r.playerImmune, size);
    const int npcs = growAll(r.npcs, r.npcImmune, size);
    BL_DEBUG("mod buffs: buffImmune grown in %d player(s) and %d NPC(s)", players, npcs);
}

}

int registerModBuff(ModBuffDef def) {
    std::lock_guard<std::mutex> l(g_mx);
    for (const Entry& e : g_regs) {
        if (e.def.mod == def.mod && e.def.name == def.name) return -1;
    }
    Entry e;
    e.def = std::move(def);
    g_regs.push_back(std::move(e));
    g_total.store(static_cast<int>(g_regs.size()), std::memory_order_release);
    return kVanillaBuffCount + static_cast<int>(g_regs.size()) - 1;
}

bool isModBuff(int type) {
    return type >= kVanillaBuffCount && type < kVanillaBuffCount + g_total.load(std::memory_order_acquire);
}

void setModBuffTexts(int type,
                     std::vector<std::pair<std::string, std::string>> names,
                     std::vector<std::pair<std::string, std::string>> descriptions) {
    std::lock_guard<std::mutex> l(g_mx);
    const int index = type - kVanillaBuffCount;
    if (index < 0 || index >= static_cast<int>(g_regs.size())) return;
    Entry& e = g_regs[static_cast<size_t>(index)];
    e.def.names = std::move(names);
    e.def.descriptions = std::move(descriptions);
    if (index < g_installed.load(std::memory_order_acquire)) applyTexts(type, e);
}

int buffTypeCount() {
    return kVanillaBuffCount + g_installed.load(std::memory_order_acquire);
}

void tickModBuffs() {
    const int total = g_total.load(std::memory_order_acquire);
    if (total == 0 || g_failed) return;
    const int installed = g_installed.load(std::memory_order_relaxed);
    const int from = kVanillaBuffCount + installed;

    if (installed == total) {
        g_tables.checkPending(from);
        static int frame = 0;
        if (++frame % 120 == 0) g_tables.watch(from, onTableRegrown);
        else {
            Il2CppArray* names = readStatic(refs().names);
            if (names && names->length < static_cast<uintptr_t>(from)) g_tables.watch(from, onTableRegrown);
        }
        return;
    }
    if (!gameReady(from)) return;
    if (installed > 0) {
        BL_ERROR("mod buffs: %d registered after installation; they get no table", total - installed);
        g_failed = true;
        return;
    }

    const int to = kVanillaBuffCount + total;
    const int grown = g_tables.grow(from, to);
    if (grown < 0) {
        BL_ERROR("mod buffs: no buff table found; mod buffs disabled");
        g_failed = true;
        return;
    }
    g_installed.store(total, std::memory_order_release);
    growInstances(to);
    patchLimits(to);

    {
        std::lock_guard<std::mutex> l(g_mx);
        for (int i = installed; i < total; ++i) {
            Entry& e = g_regs[static_cast<size_t>(i)];
            const int type = kVanillaBuffCount + i;
            int w = 0, h = 0;
            Il2CppObject* asset = content::loadTextureAsset(e.def.texture, nullptr, 0,
                                                            e.def.mod + "/" + e.def.name, &w, &h);
            if (asset) e.asset = il2cpp::api().gchandle_new(asset, false);
            applyTexture(type, e);
            applyTexts(type, e);
        }
    }
    BL_INFO("mod buffs: %d installed (ids %d..%d), %d table(s) grown from %d to %d",
            total - installed, from, to - 1, grown, from, to);
    std::vector<BuffsInstalledHook> hooks;
    {
        std::lock_guard<std::mutex> l(g_hooksMx);
        hooks = g_installedHooks;
    }
    for (BuffsInstalledHook hook : hooks) hook(from, to - 1);
}

bool modBuffsSettled() {
    return g_failed || g_installed.load(std::memory_order_relaxed) == g_total.load(std::memory_order_acquire);
}

void setBuffsInstalledHook(BuffsInstalledHook hook) {
    if (!hook) return;
    std::lock_guard<std::mutex> l(g_hooksMx);
    for (BuffsInstalledHook h : g_installedHooks) {
        if (h == hook) return;
    }
    g_installedHooks.push_back(hook);
}

int modBuffTypeByName(const std::string& mod, const std::string& name) {
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size(); ++i) {
        if (g_regs[i].def.mod == mod && g_regs[i].def.name == name) {
            return kVanillaBuffCount + static_cast<int>(i);
        }
    }
    return -1;
}

std::string modBuffKey(int type) {
    std::lock_guard<std::mutex> l(g_mx);
    const size_t i = static_cast<size_t>(type - kVanillaBuffCount);
    if (type < kVanillaBuffCount || i >= g_regs.size()) return {};
    return g_regs[i].def.mod + "/" + g_regs[i].def.name;
}

std::vector<ModBuffInfo> modBuffs() {
    std::lock_guard<std::mutex> l(g_mx);
    std::vector<ModBuffInfo> out;
    for (size_t i = 0; i < g_regs.size(); ++i) {
        const ModBuffDef& d = g_regs[i].def;
        out.push_back({kVanillaBuffCount + static_cast<int>(i), d.mod, d.name, d.texture});
    }
    return out;
}

int modBuffTypeByKey(const std::string& key) {
    const size_t slash = key.find('/');
    if (slash == std::string::npos) return -1;
    return modBuffTypeByName(key.substr(0, slash), key.substr(slash + 1));
}

std::vector<SavedBuff> collectModBuffs(Il2CppObject* player) {
    std::vector<SavedBuff> out;
    const Refs& r = refs();
    if (!r.ok || !player) return out;
    auto* types = field<Il2CppArray*>(player, r.buffType);
    auto* times = field<Il2CppArray*>(player, r.buffTime);
    Il2CppArray* noSave = readStatic(r.noSave);
    if (!types || !times) return out;
    const uintptr_t n = types->length < times->length ? types->length : times->length;
    for (uintptr_t i = 0; i < n; ++i) {
        const int type = static_cast<int32_t*>(arrayData(types))[i];
        const int time = static_cast<int32_t*>(arrayData(times))[i];
        if (!isModBuff(type) || time <= 0) continue;
        if (noSave && static_cast<uintptr_t>(type) < noSave->length &&
            static_cast<uint8_t*>(arrayData(noSave))[type]) {
            continue;
        }
        out.push_back({static_cast<int>(i), time, modBuffKey(type)});
    }
    return out;
}

std::vector<SavedBuff> restoreModBuffs(Il2CppObject* player, const std::vector<SavedBuff>& saved) {
    std::vector<SavedBuff> kept;
    const Refs& r = refs();
    auto* types = r.ok && player ? field<Il2CppArray*>(player, r.buffType) : nullptr;
    auto* times = r.ok && player ? field<Il2CppArray*>(player, r.buffTime) : nullptr;
    if (!types || !times) return saved;
    auto* t = static_cast<int32_t*>(arrayData(types));
    auto* d = static_cast<int32_t*>(arrayData(times));
    const int n = static_cast<int>(types->length < times->length ? types->length : times->length);
    for (const SavedBuff& s : saved) {
        const int type = modBuffTypeByKey(s.key);
        if (type < 0) { kept.push_back(s); continue; }
        int slot = -1;
        for (int i = 0; i < n && slot < 0; ++i) {
            if (t[i] == 0) slot = i;
        }
        if (slot < 0) { kept.push_back(s); continue; }
        t[slot] = type;
        d[slot] = s.time;
    }
    return kept;
}

}
