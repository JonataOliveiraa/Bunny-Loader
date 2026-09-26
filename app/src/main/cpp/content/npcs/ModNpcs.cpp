#include "content/npcs/ModNpcs.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "hook/CodePatch.h"
#include "content/common/ContentAssets.h"
#include "content/common/GameRefs.h"
#include "content/npcs/ModTownNpcs.h"
#include "content/common/TypeTables.h"

#include <atomic>
#include <cstring>
#include <mutex>

namespace bl::runtime {

namespace {

struct Entry {
    ModNpcDef def;
    int width = 0, height = 0;   // de UM quadro
    int textureHeight = 0;       // a tira inteira
    uint32_t asset = 0;
    // A amostra em ContentSamples.NpcsByNetId (gchandle). E ela que o
    // retrato do Bestiario desenha; nasce antes do SetStaticDefaults do mod,
    // entao os quadros definidos la precisam chegar nela depois.
    uint32_t sample = 0;          // gchandle do Asset<Texture2D>: reaplicado se a tabela for refeita
};

std::mutex g_mx;
std::vector<Entry> g_regs;               // indice = type - kVanillaNpcCount
std::atomic<int> g_total{0};
std::atomic<int> g_installed{0};
bool g_failed = false;
NpcsInstalledHook g_installedHook = nullptr;
bool g_staticDefaultsPending = false;   // instalado, esperando a tabela de drop do jogo

// animationType por tipo de mod, lido a cada quadro por NPC: copia sem trava,
// montada na instalacao e so lida depois.
std::vector<int> g_animation;

// Conferida contra toda alocacao de 697 posicoes na libil2cpp (`mov #0x2b9`
// antes de um new[]); a de Player e por instancia (abaixo).
TypeTables g_tables("NPCs de mod", kVanillaNpcCount, {
    {"Terraria.ID", "NPCID", "Sets"},
    {"Terraria", "Main", ""},
    {"Terraria", "Lang", ""},
    {"Terraria", "NPC", ""},
    {"Terraria.GameContent", "TextureAssets", ""},
    {"Terraria.GameContent.UI", "EmoteBubble", ""},
    {"Terraria.GameContent", "ConditionalDialogue", ""},
});

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* textures = nullptr;       // TextureAssets.Npc
    FieldInfo* names = nullptr;          // Lang._npcNameCache
    FieldInfo* frames = nullptr;         // Main.npcFrameCount
    FieldInfo* players = nullptr;        // Main.player
    FieldInfo* itemDrops = nullptr;      // Main.ItemDropsDB
    FieldInfo* samples = nullptr;        // ContentSamples.NpcsByNetId
    FieldInfo* creditIds = nullptr;      // ContentSamples.NpcBestiaryCreditIdsByNpcNetIds
    FieldInfo* persistentById = nullptr; // ContentSamples.NpcPersistentIdsByNetIds
    FieldInfo* idByPersistent = nullptr; // ContentSamples.NpcNetIdsByPersistentIds
    FieldInfo* rarity = nullptr;         // ContentSamples.NpcBestiaryRarityStars
    FieldInfo* sorting = nullptr;        // ContentSamples.NpcBestiarySortingId
    Il2CppClass* npcCls = nullptr;
    int32_t noAggro = -1;                // Player.npcTypeNoAggro
    int32_t type = -1, netId = -1, active = -1, width = -1, height = -1, frame = -1;
    int32_t life = -1, lifeMax = -1, damage = -1, defense = -1, defDamage = -1, defDefense = -1;
    const MethodInfo* npcCtor = nullptr;
    const MethodInfo* setDefaults = nullptr;
    const MethodInfo* scaleStats = nullptr;
    const MethodInfo* findFrame = nullptr;
    const MethodInfo* playerCtor = nullptr;
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
    Il2CppClass* cs = findClass({"Terraria.ID", "ContentSamples", {}});
    r.npcCls = findClass({"Terraria", "NPC", {}});
    Il2CppClass* npc = r.npcCls;
    r.textures = textures ? findField(textures, "Npc") : nullptr;
    r.names = lang ? findField(lang, "_npcNameCache") : nullptr;
    r.frames = main ? findField(main, "npcFrameCount") : nullptr;
    r.players = main ? findField(main, "player") : nullptr;
    r.itemDrops = main ? findField(main, "ItemDropsDB") : nullptr;
    if (cs) {
        r.samples = findField(cs, "NpcsByNetId");
        r.creditIds = findField(cs, "NpcBestiaryCreditIdsByNpcNetIds");
        r.persistentById = findField(cs, "NpcPersistentIdsByNetIds");
        r.idByPersistent = findField(cs, "NpcNetIdsByPersistentIds");
        r.rarity = findField(cs, "NpcBestiaryRarityStars");
        r.sorting = findField(cs, "NpcBestiarySortingId");
    }
    r.noAggro = player ? fieldOffset(player, "npcTypeNoAggro") : -1;
    if (npc) {
        r.type = fieldOffset(npc, "type");
        r.netId = fieldOffset(npc, "netID");
        r.active = fieldOffset(npc, "active");
        r.width = fieldOffset(npc, "width");
        r.height = fieldOffset(npc, "height");
        r.frame = fieldOffset(npc, "frame");   // Rectangle: X, Y, Width, Height
        r.life = fieldOffset(npc, "life");
        r.lifeMax = fieldOffset(npc, "lifeMax");
        r.damage = fieldOffset(npc, "damage");
        r.defense = fieldOffset(npc, "defense");
        r.defDamage = fieldOffset(npc, "defDamage");
        r.defDefense = fieldOffset(npc, "defDefense");
        r.npcCtor = a.class_get_method_from_name(npc, ".ctor", 0);
        r.setDefaults = findMethodBySignature(npc, parseSignature("void SetDefaults(int Type, NPCSpawnParams spawnparams)"));
        r.scaleStats = findMethodBySignature(npc, parseSignature("void ScaleStats(Nullable`1 activePlayersCount, Nullable`1 strengthOverride)"));
        r.findFrame = a.class_get_method_from_name(npc, "FindFrame", 0);
    }
    r.playerCtor = player ? a.class_get_method_from_name(player, ".ctor", 0) : nullptr;
    r.ok = r.textures && r.names && r.frames && r.players && r.samples && r.creditIds &&
           r.noAggro >= 0 && r.type >= 0 && r.netId >= 0 && r.active >= 0 && r.width >= 0 &&
           r.height >= 0 && r.life >= 0 && r.lifeMax >= 0 && r.damage >= 0 && r.defense >= 0 &&
           r.defDamage >= 0 && r.defDefense >= 0 && r.npcCtor && r.setDefaults && r.findFrame &&
           r.playerCtor;
    if (!r.ok) {
        BL_ERROR("NPCs de mod: refs faltando (TextureAssets.Npc=%p Lang._npcNameCache=%p "
                 "Main.npcFrameCount=%p ContentSamples=%p SetDefaults=%p FindFrame=%p)",
                 (void*)r.textures, (void*)r.names, (void*)r.frames, (void*)r.samples,
                 (void*)r.setDefaults, (void*)r.findFrame);
    }
    return r;
}

Il2CppArray* readStatic(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

bool invoke(const MethodInfo* m, void* self, void** args, const char* what) {
    Il2CppObject* exc = nullptr;
    il2cpp::api().runtime_invoke(m, self, args, &exc);
    if (exc) BL_ERROR("NPCs de mod: %s lancou excecao", what);
    return exc == nullptr;
}

void applyName(int type, const Entry& e) {
    const std::string text = content::textForCulture(e.def.names, e.def.name);
    if (Il2CppObject* t = content::makeLocalizedText("NPCName." + e.def.name, text)) {
        content::setTableElement(refs().names, type, t);
    }
}

void applyTexture(int type, const Entry& e) {
    Il2CppObject* asset = e.asset ? il2cpp::api().gchandle_get_target(e.asset) : nullptr;
    if (asset) content::setTableElement(refs().textures, type, asset);
}

void applyFrames(int type, const Entry& e) {
    Il2CppArray* frames = readStatic(refs().frames);
    if (frames && static_cast<uintptr_t>(type) < frames->length) {
        static_cast<int32_t*>(arrayData(frames))[type] = e.def.frames > 0 ? e.def.frames : 1;
    }
}

void onTableRegrown(FieldInfo* f, int size) {
    const Refs& r = refs();
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size() && static_cast<int>(i) < size - kVanillaNpcCount; ++i) {
        const int type = kVanillaNpcCount + static_cast<int>(i);
        if (f == r.names) applyName(type, g_regs[i]);
        if (f == r.textures) applyTexture(type, g_regs[i]);
        if (f == r.frames) applyFrames(type, g_regs[i]);
    }
}

bool gameReady(int size) {
    const Refs& r = refs();
    if (!r.ok) return false;
    Il2CppArray* textures = readStatic(r.textures);
    Il2CppArray* names = readStatic(r.names);
    Il2CppObject* samples = nullptr;
    il2cpp::api().field_static_get_value(r.samples, &samples);
    return textures && names && samples && readStatic(r.frames) &&
           textures->length == static_cast<uintptr_t>(size) && names->length >= static_cast<uintptr_t>(size);
}

// ---- Bestiario e amostras (ContentSamples) ----

void setDictionaryEntry(FieldInfo* f, void* key, void* value, const char* what) {
    auto& a = il2cpp::api();
    if (!f) return;
    Il2CppObject* d = nullptr;
    a.field_static_get_value(f, &d);
    if (!d) return;
    const MethodInfo* set = a.class_get_method_from_name(a.object_get_class(d), "set_Item", 2);
    if (!set) return;
    void* args[2] = {key, value};
    invoke(set, d, args, what);
}

/**
 * A amostra do NPC e as chaves do Bestiario. Matar um NPC credita a morte por
 * NpcBestiaryCreditIdsByNpcNetIds[tipo] (indexador: sem entrada, excecao), e
 * o jogo consulta NpcsByNetId em varios lugares (bestiario, invocacao).
 */
void registerSample(int type, const std::string& key) {
    auto& a = il2cpp::api();
    const Refs& r = refs();
    Il2CppObject* npc = a.object_new(r.npcCls);
    invoke(r.npcCtor, npc, nullptr, "NPC.ctor");
    int t = type;
    alignas(8) uint8_t spawnParams[32] = {};   // NPCSpawnParams sem nada: 3 Nullable vazios
    void* sd[2] = {&t, spawnParams};
    invoke(r.setDefaults, npc, sd, "SetDefaults da amostra");   // passa pelo hook do mod
    setDictionaryEntry(r.samples, &t, npc, "ContentSamples.NpcsByNetId");
    {
        std::lock_guard<std::mutex> l(g_mx);
        const size_t i = static_cast<size_t>(type - kVanillaNpcCount);
        if (i < g_regs.size()) g_regs[i].sample = a.gchandle_new(npc, false);
    }
    Il2CppString* id = a.string_new(key.c_str());
    setDictionaryEntry(r.creditIds, &t, id, "ContentSamples.NpcBestiaryCreditIdsByNpcNetIds");
    setDictionaryEntry(r.persistentById, &t, id, "ContentSamples.NpcPersistentIdsByNetIds");
    setDictionaryEntry(r.idByPersistent, id, &t, "ContentSamples.NpcNetIdsByPersistentIds");
    int stars = 1, sorting = type;
    setDictionaryEntry(r.rarity, &t, &stars, "ContentSamples.NpcBestiaryRarityStars");
    setDictionaryEntry(r.sorting, &t, &sorting, "ContentSamples.NpcBestiarySortingId");
}

// ---- limites compilados no codigo ----

struct LimitPatch {
    const char* cls;
    const char* method;
    int argCount;       // -1: todo overload com esse nome
    uint32_t vanilla;   // o limite como esta no codigo
    int offset;         // novo limite = total + offset
};

// NPCID.Count - 1 (`type > 696` sai) em quase todos; GetNPCName compara
// `netID - 1 > 695`. Os que ficam de fora (mapa, rede, tiles) so deixam o NPC
// de mod sem aquele detalhe.
constexpr LimitPatch kLimitPatches[] = {
    {"Main", "DrawNPCs", 1, kVanillaNpcCount - 1, -1},
    {"NPC", "NPCLoot", 0, kVanillaNpcCount - 1, -1},
    {"NPC", "SetDefaults", -1, kVanillaNpcCount - 1, -1},
    {"NPC", "UpdateFoundActiveNPCs", 0, kVanillaNpcCount - 1, -1},
    {"Lang", "GetNPCName", 1, kVanillaNpcCount - 2, -2},
};

bool g_limitsPatched = false;

void patchLimits(int total) {
    if (g_limitsPatched) return;
    g_limitsPatched = true;
    auto& a = il2cpp::api();
    int methodsOk = 0;
    for (const LimitPatch& p : kLimitPatches) {
        Il2CppClass* cls = il2cpp::findClass({"Terraria", p.cls, {}});
        const uint32_t to = static_cast<uint32_t>(total + p.offset);
        int patched = 0;
        const MethodInfo* last = nullptr;
        void* it = nullptr;
        while (const MethodInfo* m = cls ? a.class_get_methods(cls, &it) : nullptr) {
            if (std::strcmp(a.method_get_name(m), p.method) != 0) continue;
            if (p.argCount >= 0 && static_cast<int>(a.method_get_param_count(m)) != p.argCount) continue;
            last = m;
            patched += patchCompareLimit(m, p.vanilla, to);
        }
        if (patched > 0) {
            ++methodsOk;
            BL_DEBUG("NPCs de mod: %s.%s: %d limite(s) de %u para %u", p.cls, p.method, patched, p.vanilla, to);
        } else {
            BL_ERROR("NPCs de mod: %s.%s: limite %u nao trocado (o NPC de mod fica sem isso): %s", p.cls,
                     p.method, p.vanilla, describeCompareMiss(last, p.vanilla, to).c_str());
        }
    }
    const int methods = static_cast<int>(sizeof(kLimitPatches) / sizeof(kLimitPatches[0]));
    BL_INFO("NPCs de mod: limites do codigo trocados em %d de %d metodo(s)", methodsOk, methods);
}

// ---- NPC.FindFrame: animacao emprestada ----

using FindFrameFn = void (*)(Il2CppObject*, const MethodInfo*);
FindFrameFn g_origFindFrame = nullptr;

/**
 * A animacao do jogo e um switch pelo tipo. Com `animationType`, o NPC de mod
 * roda o ramo de um NPC do jogo: o tipo e trocado so durante a chamada (o que
 * o tModLoader faz com AnimationType). Nativo, e nao JS: roda por NPC, por
 * quadro, e para os do jogo custa uma comparacao.
 *
 * So o tipo nao basta: o FindFrame mede a altura do quadro pela textura e
 * pela contagem de quadros do tipo ATUAL (TextureAssets.Npc[tipo].Height() /
 * Main.npcFrameCount[tipo]), e com o tipo trocado mediria a do NPC do jogo —
 * que nem carregada estava (altura 0, o slime parado no quadro 0). Entao a
 * textura e a contagem do NPC de mod vao para o lugar do outro, tambem so
 * durante a chamada. Tudo na thread principal, como o desenho.
 */
void hkFindFrame(Il2CppObject* self, const MethodInfo* m) {
    const Refs& r = refs();
    int32_t& type = field<int32_t>(self, r.type);
    const int i = type - kVanillaNpcCount;
    if (i < 0 || static_cast<size_t>(i) >= g_animation.size() || g_animation[static_cast<size_t>(i)] <= 0) {
        g_origFindFrame(self, m);
        return;
    }
    const int32_t own = type;
    const int32_t borrowed = g_animation[static_cast<size_t>(i)];
    Il2CppArray* textures = readStatic(r.textures);
    Il2CppArray* frames = readStatic(r.frames);
    const bool swap = textures && frames && static_cast<uintptr_t>(own) < textures->length &&
                      static_cast<uintptr_t>(own) < frames->length;
    Il2CppObject** tex = swap ? static_cast<Il2CppObject**>(arrayData(textures)) : nullptr;
    int32_t* count = swap ? static_cast<int32_t*>(arrayData(frames)) : nullptr;
    Il2CppObject* savedTex = swap ? tex[borrowed] : nullptr;
    const int32_t savedCount = swap ? count[borrowed] : 0;
    if (swap) {
        // Ponteiro trocado e devolvido na mesma chamada: o objeto continua
        // seguro pela nossa tabela (e o coletor nao roda no meio, nesta thread).
        tex[borrowed] = tex[own];
        count[borrowed] = count[own];
    }
    type = borrowed;
    g_origFindFrame(self, m);
    type = own;
    if (swap) {
        tex[borrowed] = savedTex;
        count[borrowed] = savedCount;
    }
}

// ---- Player.npcTypeNoAggro ----
//
// bool[NPCID.Count] em CADA jogador, lido quando um NPC escolhe alvo. Os 256
// de Main.player ja existem na instalacao; os novos passam pelo construtor.

using PlayerCtorFn = void (*)(Il2CppObject*, const MethodInfo*);
PlayerCtorFn g_origPlayerCtor = nullptr;

void hkPlayerCtor(Il2CppObject* self, const MethodInfo* m) {
    g_origPlayerCtor(self, m);
    if (npcTypeCount() > kVanillaNpcCount) {
        TypeTables::growInstanceTable(self, refs().noAggro, kVanillaNpcCount, npcTypeCount(), nullptr);
    }
}

void growPlayers(int size) {
    const Refs& r = refs();
    if (!hook::install(r.playerCtor, hkPlayerCtor, &g_origPlayerCtor)) {
        BL_ERROR("NPCs de mod: sem hook no construtor de Player; jogador novo le fora de npcTypeNoAggro");
    }
    Il2CppArray* players = readStatic(r.players);
    for (uintptr_t i = 0; players && i < players->length; ++i) {
        Il2CppObject* p = static_cast<Il2CppObject**>(arrayData(players))[i];
        if (p) TypeTables::growInstanceTable(p, r.noAggro, kVanillaNpcCount, size, nullptr);
    }
}

} // namespace

int registerModNpc(ModNpcDef def) {
    std::lock_guard<std::mutex> l(g_mx);
    for (const Entry& e : g_regs) {
        if (e.def.mod == def.mod && e.def.name == def.name) return -1;
    }
    Entry e;
    e.def = std::move(def);
    g_regs.push_back(std::move(e));
    g_total.store(static_cast<int>(g_regs.size()), std::memory_order_release);
    return kVanillaNpcCount + static_cast<int>(g_regs.size()) - 1;
}

bool isModNpc(int type) {
    return type >= kVanillaNpcCount && type < kVanillaNpcCount + g_total.load(std::memory_order_acquire);
}

int npcTypeCount() {
    return kVanillaNpcCount + g_installed.load(std::memory_order_acquire);
}

void prepareModNpcs() {
    const int total = g_total.load(std::memory_order_acquire);
    if (total > 0) {
        patchLimits(kVanillaNpcCount + total);
        prepareTownNpcs(kVanillaNpcCount + total);
    }
}

bool modNpcsSettled() {
    return g_failed || (g_installed.load(std::memory_order_relaxed) == g_total.load(std::memory_order_acquire) &&
                        !g_staticDefaultsPending);
}

void setNpcsInstalledHook(NpcsInstalledHook hook) {
    g_installedHook = hook;
}

void tickModNpcs() {
    const int total = g_total.load(std::memory_order_acquire);
    if (total == 0 || g_failed) return;
    const int installed = g_installed.load(std::memory_order_relaxed);
    const int from = kVanillaNpcCount + installed;

    if (installed == total) {
        // O setStaticDefaults do mod mexe na tabela de drop, que o jogo cria
        // (e ja preenche) depois de as tabelas por tipo existirem. Nao nula =
        // pronta: o Initialize_AlmostEverything so grava o campo depois do
        // Populate.
        if (g_staticDefaultsPending) {
            Il2CppObject* drops = nullptr;
            if (refs().itemDrops) il2cpp::api().field_static_get_value(refs().itemDrops, &drops);
            if (drops || !refs().itemDrops) {
                g_staticDefaultsPending = false;
                if (g_installedHook) g_installedHook(kVanillaNpcCount, from - 1);
            }
        }
        g_tables.checkPending(from);
        static int frame = 0;
        if (++frame % 120 == 0) {
            g_tables.watch(from, onTableRegrown);
            watchTownNpcs();
        }
        else {
            Il2CppArray* names = readStatic(refs().names);
            if (names && names->length < static_cast<uintptr_t>(from)) g_tables.watch(from, onTableRegrown);
        }
        return;
    }
    if (installed > 0) {
        // Os limites no codigo foram trocados para o total da primeira
        // instalacao; registro depois dela nao e suportado.
        BL_ERROR("NPCs de mod: registro depois da instalacao ignorado");
        g_failed = true;
        return;
    }
    if (!gameReady(from)) return;

    const int to = kVanillaNpcCount + total;
    const int grown = g_tables.grow(from, to);
    if (grown < 0) {
        BL_ERROR("NPCs de mod: nenhuma tabela de NPC achada; NPCs de mod desligados");
        g_failed = true;
        return;
    }
    g_installed.store(total, std::memory_order_release);
    growPlayers(to);
    patchLimits(to);

    std::vector<std::pair<int, std::string>> samples;
    {
        std::lock_guard<std::mutex> l(g_mx);
        g_animation.clear();
        for (int i = 0; i < total; ++i) {
            Entry& e = g_regs[static_cast<size_t>(i)];
            const int type = kVanillaNpcCount + i;
            int w = 0, h = 0;
            Il2CppObject* asset = content::loadTextureAsset(e.def.texture, nullptr, 0,
                                                            e.def.mod + "/" + e.def.name, &w, &h);
            if (asset) e.asset = il2cpp::api().gchandle_new(asset, false);
            e.width = w;
            e.textureHeight = h;
            e.height = e.def.frames > 1 ? h / e.def.frames : h;
            applyTexture(type, e);
            applyName(type, e);
            applyFrames(type, e);
            g_animation.push_back(e.def.animationType);
            samples.push_back({type, e.def.mod + "/" + e.def.name});
        }
    }
    if (!hook::install(refs().findFrame, hkFindFrame, &g_origFindFrame)) {
        BL_ERROR("NPCs de mod: sem hook em NPC.FindFrame; NPC de mod nao anima");
    }
    installTownNpcs(to);
    // FORA da trava: o SetDefaults da amostra entra no JS do mod.
    for (const auto& s : samples) registerSample(s.first, s.second);
    BL_INFO("NPCs de mod: %d instalado(s) (ids %d..%d), %d tabela(s) aumentadas de %d para %d",
            total, from, to - 1, grown, from, to);
    g_staticDefaultsPending = true;
}

std::vector<ModNpcInfo> modNpcs() {
    std::lock_guard<std::mutex> l(g_mx);
    std::vector<ModNpcInfo> v;
    v.reserve(g_regs.size());
    for (size_t i = 0; i < g_regs.size(); ++i) {
        const ModNpcDef& d = g_regs[i].def;
        v.push_back({kVanillaNpcCount + static_cast<int>(i), d.mod, d.name, d.texture, d.frames});
    }
    return v;
}

void setModNpcFrames(int type, int frames) {
    if (frames < 1) return;
    std::lock_guard<std::mutex> l(g_mx);
    const size_t i = static_cast<size_t>(type - kVanillaNpcCount);
    if (type < kVanillaNpcCount || i >= g_regs.size()) return;
    Entry& e = g_regs[i];
    const int oldHeight = e.height;
    e.def.frames = frames;
    if (e.textureHeight > 0) e.height = e.textureHeight / frames;
    if (static_cast<int>(i) < g_installed.load(std::memory_order_relaxed)) applyFrames(type, e);

    // A amostra: o quadro que o Bestiario desenha, e a altura que veio da
    // textura (so se o mod nao tinha dito a dele).
    const Refs& r = refs();
    Il2CppObject* sample = e.sample ? il2cpp::api().gchandle_get_target(e.sample) : nullptr;
    if (sample && r.frame >= 0 && e.height > 0) {
        field<int32_t>(sample, r.frame + 12) = e.height;
        if (field<int32_t>(sample, r.height) == oldHeight) field<int32_t>(sample, r.height) = e.height;
    }
}

void setModNpcAnimation(int type, int animationType) {
    if (animationType < 0 || animationType >= kVanillaNpcCount) return;
    std::lock_guard<std::mutex> l(g_mx);
    const size_t i = static_cast<size_t>(type - kVanillaNpcCount);
    if (type < kVanillaNpcCount || i >= g_regs.size()) return;
    g_regs[i].def.animationType = animationType;
    // O FindFrame le sem trava: um int trocado por inteiro, na mesma thread.
    if (i < g_animation.size()) g_animation[i] = animationType;
}

int modNpcTypeByName(const std::string& mod, const std::string& name) {
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size(); ++i) {
        if (g_regs[i].def.mod == mod && g_regs[i].def.name == name) {
            return kVanillaNpcCount + static_cast<int>(i);
        }
    }
    return -1;
}

void finishModNpc(Il2CppObject* npc, int type) {
    const Refs& r = refs();
    if (!r.ok || !npc) return;
    int w = 0, h = 0;
    {
        std::lock_guard<std::mutex> l(g_mx);
        const size_t i = static_cast<size_t>(type - kVanillaNpcCount);
        if (i < g_regs.size()) { w = g_regs[i].width; h = g_regs[i].height; }
    }
    if (field<int32_t>(npc, r.width) <= 0 && w > 0) field<int32_t>(npc, r.width) = w;
    if (field<int32_t>(npc, r.height) <= 0 && h > 0) field<int32_t>(npc, r.height) = h;
    // O final do SetDefaults do jogo rodou ANTES do mod preencher os valores:
    // vida e bases saíram zeradas. De novo, agora com os do mod.
    field<uint8_t>(npc, r.active) = 1;
    field<int32_t>(npc, r.netId) = type;
    field<int32_t>(npc, r.defDamage) = field<int32_t>(npc, r.damage);
    field<int32_t>(npc, r.defDefense) = field<int32_t>(npc, r.defense);
    field<int32_t>(npc, r.life) = field<int32_t>(npc, r.lifeMax);
    if (r.scaleStats) {
        // Nullable vazio nos dois: jogadores ativos e forca de agora (o que o
        // SetDefaults faz sem NPCSpawnParams).
        alignas(8) uint8_t players[8] = {}, strength[8] = {};
        void* args[2] = {players, strength};
        invoke(r.scaleStats, npc, args, "NPC.ScaleStats");
        field<int32_t>(npc, r.life) = field<int32_t>(npc, r.lifeMax);   // a escala mexe no lifeMax
    }
}

} // namespace bl::runtime
