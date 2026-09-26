#include "content/items/ModItems.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "hook/HookManager.h"
#include "hook/CodePatch.h"
#include "content/common/ContentAssets.h"
#include "content/common/GameRefs.h"
#include "content/common/TypeTables.h"
#include "content/items/UnloadedIcon.h"

#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>

namespace bl::runtime {

namespace {

struct Entry {
    ModItemDef def;
    int width = 0, height = 0;
    uint32_t asset = 0;   // gchandle do Asset<Texture2D>: reaplicado se a tabela for refeita
    std::string unloadedKey;   // so da reserva "?": a chave do item ausente que ele representa
};

std::mutex g_mx;
std::vector<Entry> g_regs;          // indice = type - kVanillaItemCount
std::atomic<int> g_total{0};           // registrados
std::atomic<int> g_installed{0};      // ja nas tabelas do jogo
bool g_failed = false;
int g_poolFirst = -1;   // primeiro tipo da reserva "?", -1 sem reserva

// ------------------------------ refs ------------------------------

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* itemTextures = nullptr;        // TextureAssets.Item (Asset<Texture2D>[])
    FieldInfo* nameCache = nullptr;      // Lang._itemNameCache (LocalizedText[])
    FieldInfo* tooltipCache = nullptr;   // Lang._itemTooltipCache (ItemTooltip[])
    const MethodInfo* tooltipFromText = nullptr;   // ItemTooltip.FromHardcodedText(string[])
    Il2CppClass* stringCls = nullptr;
    FieldInfo* itemsByType = nullptr;       // ContentSamples.ItemsByType
    FieldInfo* persistentIdById = nullptr;   // ContentSamples.ItemPersistentIdsByNetIds
    FieldInfo* idByPersistentId = nullptr;   // ContentSamples.ItemNetIdsByPersistentIds
    Il2CppClass* itemCls = nullptr;
    Il2CppClass* localizedTextCls = nullptr;     // LocalizedText
    Il2CppClass* byteCls = nullptr;
    Il2CppClass* unityTex = nullptr;
    Il2CppClass* gameTex = nullptr;
    const MethodInfo* resetStats = nullptr;
    const MethodInfo* rebuildTooltip = nullptr;
    const MethodInfo* itemCtor = nullptr;
    const MethodInfo* setDefaults = nullptr;
    const MethodInfo* localizedTextCtor = nullptr;
    const MethodInfo* activeCulture = nullptr;
    const MethodInfo* cultureName = nullptr;
    const MethodInfo* unityCtor = nullptr;
    const MethodInfo* loadImage = nullptr;
    const MethodInfo* setFilterMode = nullptr;
    const MethodInfo* getWidth = nullptr;
    const MethodInfo* getHeight = nullptr;
    const MethodInfo* gameCtor = nullptr;
    const MethodInfo* arrayCopy = nullptr;
    const MethodInfo* arraySet = nullptr;
    int32_t offType = -1, offNetId = -1, offWidth = -1, offHeight = -1, offMaxStack = -1;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    auto& a = api();
    auto sig = [](Il2CppClass* c, const char* s) -> const MethodInfo* {
        return c ? findMethodBySignature(c, parseSignature(s)) : nullptr;
    };

    Il2CppClass* tex = findClass({"Terraria.GameContent", "TextureAssets", {}});
    Il2CppClass* lang = findClass({"Terraria", "Lang", {}});
    Il2CppClass* cs = findClass({"Terraria.ID", "ContentSamples", {}});
    Il2CppClass* language = findClass({"Terraria.Localization", "Language", {}});
    Il2CppClass* culture = findClass({"Terraria.Localization", "GameCulture", {}});
    Il2CppClass* conv = findClass({"UnityEngine", "ImageConversion", {}});
    Il2CppClass* unityTexBase = findClass({"UnityEngine", "Texture", {}});
    Il2CppClass* array = findClass({"System", "Array", {}});
    r.itemCls = findClass({"Terraria", "Item", {}});
    r.localizedTextCls = findClass({"Terraria.Localization", "LocalizedText", {}});
    r.byteCls = findClass({"System", "Byte", {}});
    r.unityTex = findClass({"UnityEngine", "Texture2D", {}});
    r.gameTex = findClass({"Microsoft.Xna.Framework.Graphics", "Texture2D", {}});

    r.itemTextures = tex ? findField(tex, "Item") : nullptr;
    r.nameCache = lang ? findField(lang, "_itemNameCache") : nullptr;
    r.tooltipCache = lang ? findField(lang, "_itemTooltipCache") : nullptr;
    r.tooltipFromText = sig(findClass({"Terraria.UI", "ItemTooltip", {}}),
                            "ItemTooltip FromHardcodedText(string[] text)");
    r.stringCls = findClass({"System", "String", {}});
    r.itemsByType = cs ? findField(cs, "ItemsByType") : nullptr;
    r.persistentIdById = cs ? findField(cs, "ItemPersistentIdsByNetIds") : nullptr;
    r.idByPersistentId = cs ? findField(cs, "ItemNetIdsByPersistentIds") : nullptr;

    r.resetStats = sig(r.itemCls, "void ResetStats(int Type)");
    r.rebuildTooltip = sig(r.itemCls, "void RebuildTooltip()");
    r.itemCtor = sig(r.itemCls, "void .ctor()");
    r.setDefaults = sig(r.itemCls, "void SetDefaults(int Type, ItemVariant variant)");
    r.localizedTextCtor = sig(r.localizedTextCls, "void .ctor(string key, string text)");
    r.activeCulture = language ? a.class_get_method_from_name(language, "get_ActiveCulture", 0) : nullptr;
    r.cultureName = culture ? a.class_get_method_from_name(culture, "get_Name", 0) : nullptr;
    r.unityCtor = sig(r.unityTex, "void .ctor(int width, int height)");
    r.loadImage = sig(conv, "bool LoadImage(Texture2D tex, byte[] data, bool markNonReadable)");
    r.setFilterMode = unityTexBase ? a.class_get_method_from_name(unityTexBase, "set_filterMode", 1) : nullptr;
    r.getWidth = unityTexBase ? a.class_get_method_from_name(unityTexBase, "get_width", 0) : nullptr;
    r.getHeight = unityTexBase ? a.class_get_method_from_name(unityTexBase, "get_height", 0) : nullptr;
    r.gameCtor = sig(r.gameTex, "void .ctor(Texture2D texture)");
    r.arrayCopy = sig(array, "void Copy(Array sourceArray, Array destinationArray, int length)");
    r.arraySet = sig(array, "void SetValue(object value, int index)");

    if (r.itemCls) {
        r.offType = fieldOffset(r.itemCls, "type");
        // netID nao existe em toda versao; sem ele, fica so o tipo.
        FieldInfo* netId = findField(r.itemCls, "netID");
        r.offNetId = netId ? static_cast<int32_t>(a.field_get_offset(netId)) : -1;
        r.offWidth = fieldOffset(r.itemCls, "width");
        r.offHeight = fieldOffset(r.itemCls, "height");
        r.offMaxStack = fieldOffset(r.itemCls, "maxStack");
    }

    r.ok = r.itemTextures && r.nameCache && r.itemsByType && r.itemCls && r.localizedTextCls && r.byteCls &&
           r.unityTex && r.gameTex && r.resetStats && r.itemCtor && r.setDefaults &&
           r.localizedTextCtor && r.unityCtor && r.loadImage && r.gameCtor && r.arrayCopy &&
           r.arraySet && r.offType >= 0 && r.offWidth >= 0 && r.offHeight >= 0 && a.array_new;
    if (!r.ok) {
        BL_ERROR("itens de mod: refs faltando (TextureAssets.Item=%p Lang._itemNameCache=%p "
                 "ContentSamples=%p ResetStats=%p LocalizedText.ctor=%p LoadImage=%p "
                 "Array.Copy=%p SetValue=%p)", (void*)r.itemTextures, (void*)r.nameCache,
                 (void*)r.itemsByType, (void*)r.resetStats, (void*)r.localizedTextCtor,
                 (void*)r.loadImage, (void*)r.arrayCopy, (void*)r.arraySet);
    }
    return r;
}

/**
 * true se rodou sem excecao; o retorno vai em `ret`. As duas coisas separadas
 * de proposito: metodo `void` devolve nulo do runtime_invoke, e tratar nulo
 * como falha fazia todo construtor e todo Array.Copy "falharem" com sucesso.
 */
bool invoke(const MethodInfo* m, void* self, void** args, const char* what,
            Il2CppObject** ret = nullptr) {
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = il2cpp::api().runtime_invoke(m, self, args, &exc);
    if (exc) {
        BL_ERROR("itens de mod: %s lancou excecao", what);
        return false;
    }
    if (ret) *ret = r;
    return true;
}

/** Para quem quer o retorno: nulo tambem quando lancou. */
Il2CppObject* invokeValue(const MethodInfo* m, void* self, void** args, const char* what) {
    Il2CppObject* r = nullptr;
    return invoke(m, self, args, what, &r) ? r : nullptr;
}

int unboxInt(Il2CppObject* o) {
    return o ? *reinterpret_cast<int*>(reinterpret_cast<char*>(o) + sizeof(Il2CppObject)) : 0;
}

Il2CppArray* readStatic(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

// ------------------------------ tabelas ------------------------------
//
// Tudo que e indexado por tipo de item nasce com ItemID.Count posicoes; a
// maquinaria que acha e aumenta esta em TypeTables. Aqui, so ONDE procurar.
//
// Esquecer uma classe NAO da erro, le ou escreve alem do fim: foi assim que o
// tooltip de item de mod sumiu (ArmorSetBonuses.SetsContaining ficou de fora,
// a leitura pegou lixo nulo e o jogo lancou NullReference a cada quadro). A
// lista foi conferida contra TODA alocacao de 6147 posicoes na libil2cpp
// (`mov #0x1803` antes de um new[]); o que nao e campo estatico esta em
// "tabelas de instancia", mais abaixo.

TypeTables g_tables("itens de mod", kVanillaItemCount, {
    {"Terraria.ID", "ItemID", "Sets"},
    {"Terraria.ID", "ItemID", ""},
    {"Terraria.ID", "AmmoID", "Sets"},
    {"Terraria", "Item", ""},
    {"Terraria", "Lang", ""},
    {"Terraria", "Main", ""},
    {"Terraria", "Player", ""},
    {"Terraria", "Recipe", ""},
    {"Terraria.GameContent", "TextureAssets", ""},
    {"Terraria.GameContent", "EmergencyStacking", ""},
    {"Terraria.GameContent.Items", "ItemVariants", ""},
    {"Terraria.GameContent.Prefixes", "PrefixLegacy", "ItemSets"},
    {"Terraria.DataStructures", "ArmorSetBonuses", ""},
    {"Terraria.UI", "ItemSorting", ""},
    {"Terraria.UI", "ItemSlot", ""},
    {"", "VirtualControllerInputState", ""},   // controles de toque
});

// ------------------------------ conteudo ------------------------------


/** O tooltip na cultura atual, uma linha por '\n'. Sem tooltip, nao mexe. */
void applyTooltip(int type, const Entry& reg) {
    const Refs& r = refs();
    if (reg.def.tooltips.empty() || !r.tooltipCache || !r.tooltipFromText || !r.stringCls) return;
    const std::string text = content::textForCulture(reg.def.tooltips, "");
    std::vector<std::string> lines;
    size_t start = 0;
    while (start <= text.size()) {
        const size_t end = text.find('\n', start);
        lines.push_back(text.substr(start, end == std::string::npos ? std::string::npos : end - start));
        if (end == std::string::npos) break;
        start = end + 1;
    }
    auto& a = il2cpp::api();
    Il2CppArray* arr = a.array_new(r.stringCls, lines.size());
    if (!arr) return;
    for (size_t i = 0; i < lines.size(); ++i) {
        a.gc_wbarrier_set_field(reinterpret_cast<Il2CppObject*>(arr),
                                reinterpret_cast<void**>(static_cast<Il2CppString**>(arrayData(arr)) + i),
                                a.string_new(lines[i].c_str()));
    }
    void* args[1] = {arr};
    Il2CppObject* exc = nullptr;
    Il2CppObject* tip = a.runtime_invoke(r.tooltipFromText, nullptr, args, &exc);
    if (exc || !tip) { BL_ERROR("itens de mod: FromHardcodedText lancou (%s)", reg.def.name.c_str()); return; }
    content::setTableElement(r.tooltipCache, type, tip);
}

void applyName(int type, const Entry& reg) {
    const std::string name = content::textForCulture(reg.def.names, reg.def.name);
    if (Il2CppObject* text = content::makeLocalizedText("ItemName." + reg.def.name, name)) {
        content::setTableElement(refs().nameCache, type, text);
    }
}

void applyTexture(int type, const Entry& reg) {
    Il2CppObject* asset = reg.asset ? il2cpp::api().gchandle_get_target(reg.asset) : nullptr;
    if (asset) content::setTableElement(refs().itemTextures, type, asset);
}

/** Chama um metodo de um Dictionary pelo objeto (a classe genérica e a dele). */
void setDictionaryEntry(FieldInfo* fieldInfo, void* key, void* value, const char* what) {
    auto& a = il2cpp::api();
    if (!fieldInfo) return;
    Il2CppObject* d = nullptr;
    a.field_static_get_value(fieldInfo, &d);
    if (!d) return;
    const MethodInfo* set = a.class_get_method_from_name(a.object_get_class(d), "set_Item", 2);
    if (!set) return;
    void* args[2] = {key, value};
    invoke(set, d, args, what);
}

/**
 * A amostra do item em ContentSamples. O jogo consulta ItemsByType[tipo] para
 * tooltip, pesquisa da Jornada e ordenacao — e um dicionario, que estoura com
 * KeyNotFoundException em vez de index fora do array.
 */
void registerSample(int type, const std::string& mod, const std::string& name) {
    auto& a = il2cpp::api();
    Refs& r = refs();
    Il2CppObject* item = a.object_new(r.itemCls);
    invoke(r.itemCtor, item, nullptr, "Item.ctor");
    int t = type;
    void* sd[2] = {&t, nullptr};
    invoke(r.setDefaults, item, sd, "SetDefaults da amostra");   // passa pelo hook do mod
    setDictionaryEntry(r.itemsByType, &t, item, "ContentSamples.ItemsByType");
    const std::string persistentId = mod + "/" + name;
    Il2CppString* p = a.string_new(persistentId.c_str());
    setDictionaryEntry(r.persistentIdById, &t, p, "ContentSamples.ItemPersistentIdsByNetIds");
    setDictionaryEntry(r.idByPersistentId, p, &t, "ContentSamples.ItemNetIdsByPersistentIds");
}

// ---- Lang.GetItemName ----
//
// Converte o id para short e devolve LocalizedText.Empty para qualquer um acima
// de ItemID.Count — o limite e a constante, nao o tamanho do cache. Todo nome
// de item passa por aqui (Item.Name, tooltip, a lista do menu), entao item de
// mod saia sem nome em todo lugar. Para tipo de mod, devolve o que gravamos no
// cache; para o resto, o original.

using GetNameFn = Il2CppObject* (*)(int32_t, const MethodInfo*);
GetNameFn g_origGetItemName = nullptr;

Il2CppObject* hkGetItemName(int32_t id, const MethodInfo* m) {
    if (isModItem(id)) {
        Il2CppArray* cache = readStatic(refs().nameCache);
        if (cache && static_cast<uintptr_t>(id) < cache->length) {
            if (Il2CppObject* t = reinterpret_cast<Il2CppObject**>(arrayData(cache))[id]) return t;
        }
    }
    return g_origGetItemName(id, m);
}

void hookItemNames() {
    static bool done = false;
    if (done) return;
    done = true;
    Il2CppClass* lang = il2cpp::findClass({"Terraria", "Lang", {}});
    const MethodInfo* m = lang ? il2cpp::findMethodBySignature(
        lang, il2cpp::parseSignature("LocalizedText GetItemName(int id)")) : nullptr;
    if (!m || !hook::install(m, hkGetItemName, &g_origGetItemName)) {
        BL_ERROR("itens de mod: sem hook em Lang.GetItemName; itens de mod ficam sem nome");
    }
}

// O drop do jogo recusa item de mod: os CommonCode.DropItem* comecam com
// `if (itemId > 0 && itemId < ItemID.Count)`, compilado pela metade
// (`(itemId - 1) >> 1 <= 0xC00`; ver patchHalvedLimit). O tModLoader troca o
// ItemID.Count por ItemLoader.ItemCount nos mesmos metodos. Aceita de 1 a
// 2*imm+2, entao imm = (total-3)/2 nunca passa do ultimo tipo; com `total`
// par sobra o ultimo, que e da reserva "?" (registrada depois dos mods).
uint32_t g_dropLimit = 0xC00;

void patchDropLimits(int total) {
    const uint32_t imm = static_cast<uint32_t>((total - 3) / 2);
    if (imm == g_dropLimit) return;
    if (imm > 0xFFF) {
        BL_ERROR("itens de mod: %d tipos de item passam do limite do drop do jogo (8193)", total);
        return;
    }
    auto& a = il2cpp::api();
    Il2CppClass* cls = il2cpp::findClass({"Terraria.GameContent.ItemDropRules", "CommonCode", {}});
    int patched = 0;
    void* it = nullptr;
    while (const MethodInfo* m = cls ? a.class_get_methods(cls, &it) : nullptr) {
        patched += patchHalvedLimit(m, g_dropLimit, imm);
    }
    if (patched == 0) {
        BL_ERROR("itens de mod: limite do drop (CommonCode.DropItem*) nao achado no codigo; "
                 "NPC nenhum solta item de mod");
        return;
    }
    BL_INFO("itens de mod: drop do jogo aceita ate o id %u (%d metodo(s) do CommonCode)", 2 * imm + 2, patched);
    g_dropLimit = imm;
}

/** O jogo ja criou as tabelas de agora? (Sao feitas no carregamento, nao no boot.) */
bool gameReady(int size) {
    Refs& r = refs();
    if (!r.ok) return false;
    Il2CppArray* tex = readStatic(r.itemTextures);
    Il2CppArray* names = readStatic(r.nameCache);
    Il2CppObject* itemsByType = nullptr;
    il2cpp::api().field_static_get_value(r.itemsByType, &itemsByType);
    return tex && names && itemsByType && tex->length == static_cast<uintptr_t>(size) &&
           names->length >= static_cast<uintptr_t>(size);
}

/** Tabela que o jogo refez (a troca de idioma refaz os caches de nome): reaplica o nosso. */
void onTableRegrown(FieldInfo* f, int size) {
    Refs& r = refs();
    if (f != r.nameCache && f != r.itemTextures && f != r.tooltipCache) return;
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size() && static_cast<int>(i) < size - kVanillaItemCount; ++i) {
        const int type = kVanillaItemCount + static_cast<int>(i);
        if (f == r.nameCache) applyName(type, g_regs[i]);
        if (f == r.itemTextures) applyTexture(type, g_regs[i]);
        if (f == r.tooltipCache) applyTooltip(type, g_regs[i]);
    }
}

// ---- tabelas de instancia ----
//
// Duas tabelas por tipo moram num OBJETO, nao num campo estatico, e a busca
// acima nao as ve:
//  - QuickStacking.MatchingItemTypeDestinationList.firstEntryForType (int[]):
//    o empilhamento rapido ESCREVE nela pelo tipo. Um objeto so, criado no
//    construtor estatico de QuickStacking, que ainda nao rodou na tela de
//    titulo.
//  - ItemFilters.MiscFallback._fitsFilterByItemType (Nullable<bool>[]): cache
//    do filtro "Diversos" do inventario, tambem escrito pelo tipo. Criado sob
//    demanda, ja dentro do mundo.
// As duas: hook no construtor, que aumenta logo depois do original, e o objeto
// que por acaso ja exista e aumentado na instalacao.
// As duas sao de tipo de valor e o zero e o "ainda nao sei" de ambas, entao o
// que cresce nasce zerado, e nao copia do [0] como nas estaticas.


using DestinationListCtorFn = void (*)(Il2CppObject*, const MethodInfo*);
using MiscFallbackCtorFn = void (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
DestinationListCtorFn g_origDestinationListCtor = nullptr;
MiscFallbackCtorFn g_origMiscFallbackCtor = nullptr;
int32_t g_offFirstEntryForType = -1;
int32_t g_offMiscFallbackTable = -1;

void hkDestinationListCtor(Il2CppObject* self, const MethodInfo* m) {
    g_origDestinationListCtor(self, m);
    TypeTables::growInstanceTable(self, g_offFirstEntryForType, kVanillaItemCount, itemTypeCount(),
                                  "itens de mod: QuickStacking.firstEntryForType");
}

void hkMiscFallbackCtor(Il2CppObject* self, Il2CppObject* otherFilters, const MethodInfo* m) {
    g_origMiscFallbackCtor(self, otherFilters, m);
    TypeTables::growInstanceTable(self, g_offMiscFallbackTable, kVanillaItemCount, itemTypeCount(),
                                  "itens de mod: MiscFallback._fitsFilterByItemType");
}

void growInstanceTables(int size) {
    static bool done = false;
    if (done) return;
    done = true;
    auto& a = il2cpp::api();

    Il2CppClass* qs = il2cpp::findClassQuiet("Terraria.GameContent", "QuickStacking");
    Il2CppClass* list = qs ? il2cpp::findNested(qs, "MatchingItemTypeDestinationList") : nullptr;
    const MethodInfo* listCtor = list ? a.class_get_method_from_name(list, ".ctor", 0) : nullptr;
    g_offFirstEntryForType = list ? il2cpp::fieldOffset(list, "firstEntryForType") : -1;
    if (!listCtor || g_offFirstEntryForType < 0 ||
        !hook::install(listCtor, hkDestinationListCtor, &g_origDestinationListCtor)) {
        BL_ERROR("itens de mod: sem hook em QuickStacking.MatchingItemTypeDestinationList; "
                 "empilhar item de mod escreve fora da tabela");
    }
    if (FieldInfo* scratch = qs ? il2cpp::findField(qs, "matchingItemTypeScratch") : nullptr) {
        Il2CppObject* obj = nullptr;
        a.field_static_get_value(scratch, &obj);
        TypeTables::growInstanceTable(obj, g_offFirstEntryForType, kVanillaItemCount, size,
                                      "itens de mod: QuickStacking.firstEntryForType");
    }

    Il2CppClass* filters = il2cpp::findClassQuiet("Terraria.GameContent.Creative", "ItemFilters");
    Il2CppClass* misc = filters ? il2cpp::findNested(filters, "MiscFallback") : nullptr;
    const MethodInfo* miscCtor = misc ? a.class_get_method_from_name(misc, ".ctor", 1) : nullptr;
    g_offMiscFallbackTable = misc ? il2cpp::fieldOffset(misc, "_fitsFilterByItemType") : -1;
    if (!miscCtor || g_offMiscFallbackTable < 0 ||
        !hook::install(miscCtor, hkMiscFallbackCtor, &g_origMiscFallbackCtor)) {
        BL_ERROR("itens de mod: sem hook em ItemFilters.MiscFallback; o filtro Diversos "
                 "escreve fora da tabela com item de mod");
    }
}

} // namespace

int registerModItem(ModItemDef def) {
    std::lock_guard<std::mutex> l(g_mx);
    for (const Entry& r : g_regs) {
        if (r.def.mod == def.mod && r.def.name == def.name) return -1;
    }
    Entry reg;
    reg.def = std::move(def);
    g_regs.push_back(std::move(reg));
    g_total.store(static_cast<int>(g_regs.size()), std::memory_order_release);
    return kVanillaItemCount + static_cast<int>(g_regs.size()) - 1;
}

bool isModItem(int type) {
    return type >= kVanillaItemCount &&
           type < kVanillaItemCount + g_total.load(std::memory_order_acquire);
}

std::string modItemKey(int type) {
    if (!isModItem(type)) return {};
    std::lock_guard<std::mutex> l(g_mx);
    const Entry& e = g_regs[static_cast<size_t>(type - kVanillaItemCount)];
    // O "?" responde pelo item que representa; um "?" livre nao e nada.
    if (isUnloadedType(type)) return e.unloadedKey;
    return e.def.mod + "/" + e.def.name;
}

int modItemTypeByKey(const std::string& key) {
    const size_t slash = key.find('/');
    if (slash == std::string::npos) return -1;
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size(); ++i) {
        const ModItemDef& d = g_regs[i].def;
        if (isUnloadedType(kVanillaItemCount + static_cast<int>(i))) continue;
        if (key.compare(0, slash, d.mod) == 0 && slash == d.mod.size() &&
            key.compare(slash + 1, std::string::npos, d.name) == 0) {
            return kVanillaItemCount + static_cast<int>(i);
        }
    }
    return -1;
}

// ------------------------------ o "?" ------------------------------

constexpr const char* kLoaderMod = "bunnyloader";

void registerUnloadedPool() {
    if (g_poolFirst >= 0) return;
    for (int i = 0; i < kUnloadedPoolSize; ++i) {
        ModItemDef d;
        d.mod = kLoaderMod;
        d.name = "Unloaded" + std::to_string(i);
        d.textureData = bl_unloaded_icon_png;
        d.textureSize = bl_unloaded_icon_png_len;
        d.names = {{"pt-BR", "Item não carregado"}, {"en-US", "Unloaded item"}};
        const int type = registerModItem(std::move(d));
        if (i == 0) g_poolFirst = type;
    }
    BL_INFO("itens de mod: reserva de %d item(ns) \"?\" a partir do id %d", kUnloadedPoolSize, g_poolFirst);
}

bool isUnloadedType(int type) {
    return g_poolFirst >= 0 && type >= g_poolFirst && type < g_poolFirst + kUnloadedPoolSize;
}

int unloadedTypeFor(const std::string& key) {
    if (g_poolFirst < 0 || key.empty() || g_poolFirst + kUnloadedPoolSize > itemTypeCount()) return -1;
    Entry named;
    int type = -1;
    {
        std::lock_guard<std::mutex> l(g_mx);
        for (int i = 0; i < kUnloadedPoolSize; ++i) {
            Entry& e = g_regs[static_cast<size_t>(g_poolFirst - kVanillaItemCount + i)];
            if (e.unloadedKey == key) return g_poolFirst + i;
            if (type < 0 && e.unloadedKey.empty()) type = g_poolFirst + i;
        }
        if (type < 0) {
            BL_ERROR("itens de mod: reserva \"?\" esgotada (%d itens ausentes distintos); %s "
                     "fica so no arquivo", kUnloadedPoolSize, key.c_str());
            return -1;
        }
        Entry& e = g_regs[static_cast<size_t>(type - kVanillaItemCount)];
        e.unloadedKey = key;
        // O nome diz o que era, ja que o mod nao esta aqui para dizer.
        const std::string name = key.substr(key.find('/') + 1);
        e.def.names = {{"pt-BR", "Item não carregado (" + name + ")"},
                       {"en-US", "Unloaded item (" + name + ")"}};
        named = e;
    }
    applyName(type, named);
    return type;
}

void setupUnloadedItem(Il2CppObject* item, int type) {
    Refs& r = refs();
    if (!r.ok || !item) return;
    prepareModItem(item, type);
    // Nao se usa nem se coloca (o ResetStats ja deixou assim); so a pilha, que
    // tem de caber a do item original.
    if (r.offMaxStack >= 0) field<int32_t>(item, r.offMaxStack) = 9999;
    finishModItem(item, type);
}

int itemTypeCount() {
    return kVanillaItemCount + g_installed.load(std::memory_order_acquire);
}

namespace {
std::atomic<ItemsInstalledHook> g_installedHook{nullptr};
} // namespace

void tickModItems() {
    const int total = g_total.load(std::memory_order_acquire);
    if (total == 0 || g_failed) return;
    const int installed = g_installed.load(std::memory_order_relaxed);
    const int from = kVanillaItemCount + installed;

    if (installed == total) {
        // Um quadro a cada ~2 s basta para tabela refeita — menos o cache de
        // nomes, que a troca de idioma refaz na hora e o inventario le logo.
        g_tables.checkPending(from);
        static int frame = 0;
        if (++frame % 120 == 0) g_tables.watch(from, onTableRegrown);
        else {
            Il2CppArray* names = readStatic(refs().nameCache);
            if (names && names->length < static_cast<uintptr_t>(from)) g_tables.watch(from, onTableRegrown);
        }
        return;
    }
    if (!gameReady(from)) return;

    auto& a = il2cpp::api();
    const int to = kVanillaItemCount + total;
    const int grown = g_tables.grow(from, to);
    if (grown < 0) {
        BL_ERROR("itens de mod: nenhuma tabela de item achada; itens de mod desligados");
        g_failed = true;
        return;
    }
    g_installed.store(total, std::memory_order_release);
    hookItemNames();
    patchDropLimits(to);
    growInstanceTables(to);

    struct PendingSample { int type; std::string mod, name; };
    std::vector<PendingSample> pending;
    {
        std::lock_guard<std::mutex> l(g_mx);
        for (int i = installed; i < total; ++i) {
            Entry& reg = g_regs[static_cast<size_t>(i)];
            const int type = kVanillaItemCount + i;
            Il2CppObject* asset = content::loadTextureAsset(
                reg.def.texture, reg.def.textureData, reg.def.textureSize,
                reg.def.mod + "/" + reg.def.name, &reg.width, &reg.height);
            if (asset) reg.asset = a.gchandle_new(asset, false);
            applyTexture(type, reg);
            applyName(type, reg);
            applyTooltip(type, reg);
            pending.push_back({type, reg.def.mod, reg.def.name});
        }
    }
    // FORA da trava: o SetDefaults da amostra passa pelo hook, entra no JS do
    // mod e volta em finishModItem, que pede a mesma trava — segura-la aqui
    // travaria o jogo no primeiro item.
    for (const PendingSample& n : pending) registerSample(n.type, n.mod, n.name);
    BL_INFO("itens de mod: %d instalado(s) (ids %d..%d), %d tabela(s) aumentadas de %d para %d",
            total - installed, from, to - 1, grown, from, to);
    if (ItemsInstalledHook hook = g_installedHook.load(std::memory_order_acquire)) hook(from, to - 1);
}

void setModItemTooltip(int type, std::vector<std::pair<std::string, std::string>> tooltips) {
    std::lock_guard<std::mutex> l(g_mx);
    const size_t i = static_cast<size_t>(type - kVanillaItemCount);
    if (type < kVanillaItemCount || i >= g_regs.size()) return;
    g_regs[i].def.tooltips = std::move(tooltips);
    if (static_cast<int>(i) < g_installed.load(std::memory_order_relaxed)) applyTooltip(type, g_regs[i]);
}

bool modItemsSettled() {
    return g_failed || g_installed.load(std::memory_order_relaxed) == g_total.load(std::memory_order_acquire);
}

void setItemsInstalledHook(ItemsInstalledHook hook) {
    g_installedHook.store(hook, std::memory_order_release);
}

void prepareModItem(Il2CppObject* item, int type) {
    Refs& r = refs();
    if (!r.ok || !item) return;
    int t = type;
    void* args[1] = {&t};
    invoke(r.resetStats, item, args, "Item.ResetStats");
    field<int32_t>(item, r.offType) = type;
    if (r.offNetId >= 0) field<int32_t>(item, r.offNetId) = type;
}

void finishModItem(Il2CppObject* item, int type) {
    Refs& r = refs();
    if (!r.ok || !item) return;
    int w = 0, h = 0;
    {
        std::lock_guard<std::mutex> l(g_mx);
        const size_t i = static_cast<size_t>(type - kVanillaItemCount);
        if (i < g_regs.size()) { w = g_regs[i].width; h = g_regs[i].height; }
    }
    if (field<int32_t>(item, r.offWidth) <= 0 && w > 0) field<int32_t>(item, r.offWidth) = w;
    if (field<int32_t>(item, r.offHeight) <= 0 && h > 0) field<int32_t>(item, r.offHeight) = h;
    if (r.rebuildTooltip) invoke(r.rebuildTooltip, item, nullptr, "Item.RebuildTooltip");
}


std::vector<ModItemInfo> modItems() {
    std::lock_guard<std::mutex> l(g_mx);
    std::vector<ModItemInfo> v;
    v.reserve(g_regs.size());
    for (size_t i = 0; i < g_regs.size(); ++i) {
        if (isUnloadedType(kVanillaItemCount + static_cast<int>(i))) continue;   // nao vai para o menu
        v.push_back({kVanillaItemCount + static_cast<int>(i), g_regs[i].def.mod,
                     g_regs[i].def.name, g_regs[i].def.texture});
    }
    return v;
}

} // namespace bl::runtime
