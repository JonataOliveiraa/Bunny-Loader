#include "runtime/ModBuffs.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "runtime/CodePatch.h"
#include "runtime/ContentAssets.h"
#include "runtime/GameRefs.h"
#include "runtime/TypeTables.h"

#include <atomic>
#include <cstring>
#include <mutex>

namespace bl::runtime {

namespace {

struct Entry {
    ModBuffDef def;
    uint32_t asset = 0;   // gchandle do Asset<Texture2D>: reaplicado se a tabela for refeita
};

std::mutex g_mx;
std::vector<Entry> g_regs;   // indice = type - kVanillaBuffCount
std::atomic<BuffsInstalledHook> g_installedHook{nullptr};
std::atomic<int> g_total{0};
std::atomic<int> g_installed{0};
bool g_failed = false;

// Conferida contra toda alocacao de 389 posicoes na libil2cpp (`mov #0x185`):
// as estaticas estao em Main (10), BuffID.Sets, Lang (nome e descricao) e
// TextureAssets; as por instancia, em Player e NPC (abaixo).
TypeTables g_tables("buffs de mod", kVanillaBuffCount, {
    {"Terraria.ID", "BuffID", "Sets"},
    {"Terraria", "Main", ""},
    {"Terraria", "Lang", ""},
    {"Terraria.GameContent", "TextureAssets", ""},
});

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* textures = nullptr;       // TextureAssets.Buff
    FieldInfo* names = nullptr;          // Lang._buffNameCache
    FieldInfo* descriptions = nullptr;   // Lang._buffDescriptionCache
    FieldInfo* noSave = nullptr;         // Main.buffNoSave
    FieldInfo* players = nullptr;        // Main.player
    FieldInfo* npcs = nullptr;           // Main.npc
    int32_t playerImmune = -1, npcImmune = -1;   // buffImmune
    int32_t buffType = -1, buffTime = -1;        // Player
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
        BL_ERROR("buffs de mod: refs faltando (TextureAssets.Buff=%p Lang._buffNameCache=%p "
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
    const std::string name = content::textForCulture(e.def.names, e.def.name);
    if (Il2CppObject* t = content::makeLocalizedText("BuffName." + e.def.name, name)) {
        content::setTableElement(refs().names, type, t);
    }
    const std::string desc = content::textForCulture(e.def.descriptions, "");
    if (Il2CppObject* t = content::makeLocalizedText("BuffDescription." + e.def.name, desc)) {
        content::setTableElement(refs().descriptions, type, t);
    }
}

void applyTexture(int type, const Entry& e) {
    Il2CppObject* asset = e.asset ? il2cpp::api().gchandle_get_target(e.asset) : nullptr;
    if (asset) content::setTableElement(refs().textures, type, asset);
}

/** Tabela que o jogo refez (a troca de idioma refaz os nomes): reaplica o nosso. */
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

// ---- limites compilados no codigo ----

struct LimitPatch {
    const char* ns;
    const char* cls;
    const char* method;
    uint32_t vanilla;   // o limite como esta no codigo
    int offset;         // novo limite = total + offset
    bool belowToo;      // tambem `tipo < limite` (b.lo)
};

// Achados varrendo a libil2cpp por `cmp #387..#389` + desvio de ordem, e
// conferidos um a um (os outros sao tiles e projeteis com esses numeros).
constexpr LimitPatch kLimitPatches[] = {
    // A barra: `1 <= tipo <= 388` entra; `tipo >= 389` e ZERADO no jogador.
    {"", "GUIBuffs", "Draw", kVanillaBuffCount - 2, -2, true},
    {"", "GUIBuffs", "Draw", kVanillaBuffCount, 0, true},
    // A enfermeira so cura debuff com tipo <= 388.
    {"Terraria", "Main", "GetNurseHealCost", kVanillaBuffCount - 1, -1, false},
    {"Terraria", "Main", "NPCChatText_DoNurseHeal", kVanillaBuffCount - 1, -1, false},
    {"", "GUINPCDialogue", "Option1Clicked", kVanillaBuffCount - 1, -1, false},
    {"", "GUINPCDialogue", "SetupButtonText", kVanillaBuffCount - 1, -1, false},
    // Remover buff de NPC pedido pela rede (chicote etc. no multijogador).
    {"Terraria", "NPC", "RequestBuffRemoval", kVanillaBuffCount - 1, -1, false},
};

void patchLimits(int total) {
    auto& a = il2cpp::api();
    for (const LimitPatch& p : kLimitPatches) {
        Il2CppClass* cls = il2cpp::findClass({p.ns, p.cls, {}});
        int patched = 0;
        void* it = nullptr;
        while (const MethodInfo* m = cls ? a.class_get_methods(cls, &it) : nullptr) {
            if (std::strcmp(a.method_get_name(m), p.method) != 0) continue;
            patched += patchCompareLimit(m, p.vanilla, static_cast<uint32_t>(total + p.offset), p.belowToo);
        }
        if (patched > 0) {
            BL_INFO("buffs de mod: %s.%s: %d limite(s) de %u para %d", p.cls, p.method, patched,
                    p.vanilla, total + p.offset);
        } else {
            BL_ERROR("buffs de mod: %s.%s: limite %u nao achado no codigo", p.cls, p.method, p.vanilla);
        }
    }
}

// ---- buffImmune: bool[389] em cada Player e NPC ----
//
// O construtor cria; Player.Update e NPC.SetDefaults so zeram as 389
// primeiras (memset). Os de Main.player/Main.npc ja existem na instalacao; os
// que nascerem depois passam pelo construtor.

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
            BL_ERROR("buffs de mod: sem hook no construtor de Player; jogador novo le fora de buffImmune");
        }
        if (!hook::install(r.npcCtor, hkNpcCtor, &g_origNpcCtor)) {
            BL_ERROR("buffs de mod: sem hook no construtor de NPC; NPC novo le fora de buffImmune");
        }
    }
    const int players = growAll(r.players, r.playerImmune, size);
    const int npcs = growAll(r.npcs, r.npcImmune, size);
    BL_INFO("buffs de mod: buffImmune aumentada em %d jogador(es) e %d NPC(s)", players, npcs);
}

} // namespace

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
    // Os limites compilados valem para UM total: um lote depois do primeiro
    // (mod carregado mais tarde) ficaria de fora da barra. Todos os mods
    // registram antes da tela de titulo, entao na pratica ha um lote so.
    if (installed > 0) {
        BL_ERROR("buffs de mod: %d registrado(s) depois da instalacao; ficam sem tabela", total - installed);
        g_failed = true;
        return;
    }

    const int to = kVanillaBuffCount + total;
    const int grown = g_tables.grow(from, to);
    if (grown < 0) {
        BL_ERROR("buffs de mod: nenhuma tabela de buff achada; buffs de mod desligados");
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
    BL_INFO("buffs de mod: %d instalado(s) (ids %d..%d), %d tabela(s) aumentadas de %d para %d",
            total - installed, from, to - 1, grown, from, to);
    if (BuffsInstalledHook hook = g_installedHook.load(std::memory_order_acquire)) {
        hook(from, to - 1);
    }
}

bool modBuffsSettled() {
    return g_failed || g_installed.load(std::memory_order_relaxed) == g_total.load(std::memory_order_acquire);
}

void setBuffsInstalledHook(BuffsInstalledHook hook) {
    g_installedHook.store(hook, std::memory_order_release);
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
        // O jogo compacta a lista ao carregar (o buff de mod saiu dela) e a
        // quer continua: primeiro slot livre, nao o de antes.
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

} // namespace bl::runtime
