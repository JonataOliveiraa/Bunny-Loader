#include "runtime/ModItems.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "hook/HookManager.h"
#include "runtime/GameRefs.h"

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
};

std::mutex g_mx;
std::vector<Entry> g_regs;          // indice = type - kVanillaItemCount
std::atomic<int> g_total{0};           // registrados
std::atomic<int> g_installed{0};      // ja nas tabelas do jogo
bool g_failed = false;

// ------------------------------ refs ------------------------------

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* itemTextures = nullptr;        // TextureAssets.Item (Asset<Texture2D>[])
    FieldInfo* nameCache = nullptr;      // Lang._itemNameCache (LocalizedText[])
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
    int32_t offType = -1, offNetId = -1, offWidth = -1, offHeight = -1;
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
// Toda tabela do jogo indexada por tipo de item nasce com ItemID.Count
// posicoes, e o jogo le `tabela[tipo]` sem conferir: um id novo estoura o
// array na primeira consulta. O ExMod do TL Pro aumenta umas trinta a mao;
// aqui elas sao ACHADAS: em cada classe que guarda tabelas de item, todo array
// estatico com exatamente ItemID.Count posicoes e uma delas.
//
// So nestas classes, e nao no jogo inteiro: ler um campo estatico roda o
// construtor estatico da classe, e rodar o de uma classe que o jogo ainda nao
// tocou e arriscar efeito colateral. Estas o jogo ja inicializou no menu.

struct TableClass { const char* ns; const char* name; const char* nested; };
constexpr TableClass kTableClasses[] = {
    {"Terraria.ID", "ItemID", "Sets"},
    {"Terraria.ID", "ItemID", ""},
    {"Terraria", "Item", ""},
    {"Terraria", "Lang", ""},
    {"Terraria", "Main", ""},
    {"Terraria", "Player", ""},
    {"Terraria", "Recipe", ""},
    {"Terraria.GameContent", "TextureAssets", ""},
    {"Terraria.GameContent.Prefixes", "PrefixLegacy", "ItemSets"},
    {"Terraria.UI", "ItemSorting", ""},
    {"Terraria.UI", "ItemSlot", ""},
};

std::vector<FieldInfo*> g_tables;

/** Novo array com `newLength` posicoes: copia o velho e enche o resto com o [0]. */
Il2CppArray* grow(Il2CppArray* old, uintptr_t newLength) {
    auto& a = il2cpp::api();
    Refs& r = refs();
    Il2CppClass* arrCls = a.object_get_class(reinterpret_cast<Il2CppObject*>(old));
    Il2CppClass* elem = a.class_get_element_class(arrCls);
    Il2CppArray* n = a.array_new(elem, newLength);
    if (!n) return nullptr;
    const uintptr_t len = old->length;

    if (a.class_is_valuetype(elem)) {
        // Tipo de valor: bytes. O [0] (o "nada") e o valor de quem ainda nao
        // disse nada — o mesmo que o item de mod teria se o jogo o conhecesse.
        uint32_t align = 0;
        const size_t elemSize = static_cast<size_t>(a.class_value_size(elem, &align));
        char* d = static_cast<char*>(arrayData(n));
        const char* s = static_cast<const char*>(arrayData(old));
        std::memcpy(d, s, elemSize * len);
        for (uintptr_t i = len; i < newLength; ++i) std::memcpy(d + i * elemSize, s, elemSize);
        return n;
    }
    // Referencia: pelo proprio runtime (Array.Copy / SetValue), que passa pela
    // barreira de escrita do coletor. Copiar ponteiro com memcpy num heap com
    // GC incremental pode deixar o objeto sem ninguem que o marque.
    int copyLength = static_cast<int>(len);
    void* args[3] = {old, n, &copyLength};
    if (!invoke(r.arrayCopy, nullptr, args, "Array.Copy")) return nullptr;
    Il2CppObject* zero = len > 0 ? reinterpret_cast<Il2CppObject**>(arrayData(old))[0] : nullptr;
    for (uintptr_t i = len; i < newLength; ++i) {
        int idx = static_cast<int>(i);
        void* sv[2] = {zero, &idx};
        if (!invoke(r.arraySet, n, sv, "Array.SetValue")) return nullptr;
    }
    return n;
}

/** As tabelas: todo array estatico com `tamanho` posicoes nas classes acima. */
void findTables(uintptr_t size) {
    auto& a = il2cpp::api();
    if (!a.class_get_fields || !a.field_get_flags) {
        BL_ERROR("itens de mod: il2cpp sem enumeracao de campos; nao da para achar as tabelas");
        return;
    }
    for (const TableClass& c : kTableClasses) {
        // Quieto: classe que nao existe nesta versao so fica de fora.
        Il2CppClass* cls = il2cpp::findClassQuiet(c.ns, c.name);
        if (cls && c.nested[0]) cls = il2cpp::findNested(cls, c.nested);
        if (!cls) continue;
        int found = 0;
        void* it = nullptr;
        while (FieldInfo* f = a.class_get_fields(cls, &it)) {
            const uint32_t flags = a.field_get_flags(f);
            if (!(flags & 0x10) || (flags & 0x40)) continue;          // so estatico, sem const
            if (static_cast<int64_t>(a.field_get_offset(f)) == -1) continue;  // thread-static
            char* typeName = a.type_get_name(a.field_get_type(f));
            const bool isArray = typeName && std::strlen(typeName) > 2 &&
                                 std::strcmp(typeName + std::strlen(typeName) - 2, "[]") == 0;
            if (typeName) a.il2cpp_free(typeName);
            if (!isArray) continue;
            Il2CppArray* arr = readStatic(f);
            if (!arr || arr->length != size) continue;
            g_tables.push_back(f);
            ++found;
        }
        BL_INFO("itens de mod: %s.%s%s%s: %d tabela(s) de item", c.ns, c.name,
                c.nested[0] ? "." : "", c.nested, found);
    }
}

// ------------------------------ conteudo ------------------------------

/** O PNG do mod virou Texture2D do jogo (ver Texture.cpp: e o mesmo caminho). */
Il2CppObject* loadPngTexture(const std::string& path, int* w, int* h) {
    auto& a = il2cpp::api();
    Refs& r = refs();
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) { BL_ERROR("itens de mod: textura nao abre: %s", path.c_str()); return nullptr; }
    std::fseek(f, 0, SEEK_END);
    long n = std::ftell(f);
    std::fseek(f, 0, SEEK_SET);
    Il2CppArray* bytes = n > 0 ? a.array_new(r.byteCls, static_cast<uintptr_t>(n)) : nullptr;
    size_t bytesRead = bytes ? std::fread(arrayData(bytes), 1, static_cast<size_t>(n), f) : 0;
    std::fclose(f);
    if (!bytes || bytesRead != static_cast<size_t>(n)) {
        BL_ERROR("itens de mod: textura ilegivel: %s", path.c_str());
        return nullptr;
    }

    Il2CppObject* ut = a.object_new(r.unityTex);
    int two = 2;
    void* c1[2] = {&two, &two};
    if (!invoke(r.unityCtor, ut, c1, "Texture2D(2,2)")) return nullptr;
    uint8_t nonReadable = 0;
    void* c2[3] = {ut, bytes, &nonReadable};
    Il2CppObject* ok = invokeValue(r.loadImage, nullptr, c2, "ImageConversion.LoadImage");
    if (!ok || !*(reinterpret_cast<uint8_t*>(ok) + sizeof(Il2CppObject))) {
        BL_ERROR("itens de mod: a Unity nao decodificou %s", path.c_str());
        return nullptr;
    }
    // Pixel art: vizinho-mais-proximo. A Unity nasce bilinear e o item sairia
    // borrado ao lado dos do jogo.
    if (r.setFilterMode) {
        int point = 0;   // FilterMode.Point
        void* c3[1] = {&point};
        invoke(r.setFilterMode, ut, c3, "Texture.filterMode");
    }
    if (r.getWidth) *w = unboxInt(invokeValue(r.getWidth, ut, nullptr, "Texture.width"));
    if (r.getHeight) *h = unboxInt(invokeValue(r.getHeight, ut, nullptr, "Texture.height"));

    Il2CppObject* gt = a.object_new(r.gameTex);
    void* c4[1] = {ut};
    if (!invoke(r.gameCtor, gt, c4, "Texture2D do jogo")) return nullptr;
    return gt;
}

/**
 * Asset<Texture2D> ja CARREGADO. O jogo so desenha item pelo asset, e antes
 * de desenhar pergunta o estado: NotLoaded o faria pedir "Images/Item_6147" ao
 * disco, que nao existe.
 */
Il2CppObject* createAsset(Il2CppObject* tex, const std::string& name) {
    auto& a = il2cpp::api();
    Il2CppArray* table = readStatic(refs().itemTextures);
    Il2CppClass* assetCls = a.class_get_element_class(
        a.object_get_class(reinterpret_cast<Il2CppObject*>(table)));
    const MethodInfo* ctor = a.class_get_method_from_name(assetCls, ".ctor", 1);
    const MethodInfo* submit = a.class_get_method_from_name(assetCls, "SubmitLoadedContent", 2);
    if (!ctor || !submit) {
        BL_ERROR("itens de mod: Asset<Texture2D> sem .ctor/SubmitLoadedContent");
        return nullptr;
    }
    Il2CppObject* asset = a.object_new(assetCls);
    void* c1[1] = {a.string_new(name.c_str())};
    if (!invoke(ctor, asset, c1, "Asset.ctor")) return nullptr;
    void* c2[2] = {tex, nullptr};
    if (!invoke(submit, asset, c2, "Asset.SubmitLoadedContent")) return nullptr;
    return asset;
}

/** O nome na cultura do jogo agora: exata, depois pela lingua, depois qualquer. */
std::string nameForCulture(const ModItemDef& d) {
    Refs& r = refs();
    std::string culture;
    if (r.activeCulture && r.cultureName) {
        Il2CppObject* c = invokeValue(r.activeCulture, nullptr, nullptr, "Language.ActiveCulture");
        auto* s = c ? reinterpret_cast<Il2CppString*>(invokeValue(r.cultureName, c, nullptr, "GameCulture.Name"))
                    : nullptr;
        if (s) for (int i = 0; i < s->length; ++i) culture += static_cast<char>(s->chars[i]);
    }
    const std::string languagePrefix = culture.substr(0, culture.find('-'));
    const std::string* match = nullptr;
    for (const auto& n : d.names) if (n.first == culture) match = &n.second;
    if (!match) for (const auto& n : d.names) if (n.first.substr(0, n.first.find('-')) == languagePrefix) match = &n.second;
    if (!match) for (const auto& n : d.names) if (n.first.empty() || n.first == "en-US") match = &n.second;
    if (!match && !d.names.empty()) match = &d.names.front().second;
    return match ? *match : d.name;
}

bool writeToTable(FieldInfo* fieldInfo, int index, Il2CppObject* value, const char* what) {
    Il2CppArray* arr = readStatic(fieldInfo);
    if (!arr || static_cast<uintptr_t>(index) >= arr->length) return false;
    void* sv[2] = {value, &index};
    return invoke(refs().arraySet, arr, sv, what);
}

void applyName(int type, const Entry& reg) {
    Refs& r = refs();
    Il2CppObject* text = il2cpp::api().object_new(r.localizedTextCls);
    const std::string key = "ItemName." + reg.def.name;
    const std::string name = nameForCulture(reg.def);
    void* c[2] = {il2cpp::api().string_new(key.c_str()), il2cpp::api().string_new(name.c_str())};
    if (invoke(r.localizedTextCtor, text, c, "LocalizedText.ctor")) {
        writeToTable(r.nameCache, type, text, "nome do item");
    }
}

void applyTexture(int type, const Entry& reg) {
    Il2CppObject* asset = reg.asset ? il2cpp::api().gchandle_get_target(reg.asset) : nullptr;
    if (asset) writeToTable(refs().itemTextures, type, asset, "textura do item");
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

/**
 * Tabela refeita pelo jogo com o tamanho de fabrica (a troca de idioma refaz
 * os caches de nome): aumenta de novo e reaplica o que era nosso nela.
 */
void watchTables(int size) {
    Refs& r = refs();
    for (FieldInfo* f : g_tables) {
        Il2CppArray* arr = readStatic(f);
        if (!arr || arr->length >= static_cast<uintptr_t>(size)) continue;
        if (arr->length != static_cast<uintptr_t>(kVanillaItemCount)) continue;   // nao e nossa
        Il2CppArray* grownArr = grow(arr, static_cast<uintptr_t>(size));
        if (!grownArr) continue;
        il2cpp::api().field_static_set_value(f, grownArr);
        BL_INFO("itens de mod: tabela %s refeita pelo jogo; aumentada de novo",
                il2cpp::api().field_get_name ? il2cpp::api().field_get_name(f) : "?");
        std::lock_guard<std::mutex> l(g_mx);
        for (size_t i = 0; i < g_regs.size() && static_cast<int>(i) < size - kVanillaItemCount; ++i) {
            const int type = kVanillaItemCount + static_cast<int>(i);
            if (f == r.nameCache) applyName(type, g_regs[i]);
            if (f == r.itemTextures) applyTexture(type, g_regs[i]);
        }
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

int itemTypeCount() {
    return kVanillaItemCount + g_installed.load(std::memory_order_acquire);
}

void tickModItems() {
    const int total = g_total.load(std::memory_order_acquire);
    if (total == 0 || g_failed) return;
    const int installed = g_installed.load(std::memory_order_relaxed);
    const int from = kVanillaItemCount + installed;

    if (installed == total) {
        // Um quadro a cada ~2 s basta para tabela refeita — menos o cache de
        // nomes, que a troca de idioma refaz na hora e o inventario le logo.
        static int frame = 0;
        if (++frame % 120 == 0) watchTables(from);
        else {
            Il2CppArray* names = readStatic(refs().nameCache);
            if (names && names->length < static_cast<uintptr_t>(from)) watchTables(from);
        }
        return;
    }
    if (!gameReady(from)) return;

    auto& a = il2cpp::api();
    const int to = kVanillaItemCount + total;
    if (g_tables.empty()) {
        findTables(static_cast<uintptr_t>(from));
        if (g_tables.empty()) {
            BL_ERROR("itens de mod: nenhuma tabela de item achada; itens de mod desligados");
            g_failed = true;
            return;
        }
    }
    int grown = 0;
    for (FieldInfo* f : g_tables) {
        Il2CppArray* arr = readStatic(f);
        if (!arr || arr->length != static_cast<uintptr_t>(from)) continue;
        Il2CppArray* grownArr = grow(arr, static_cast<uintptr_t>(to));
        if (!grownArr) continue;
        a.field_static_set_value(f, grownArr);
        ++grown;
    }
    g_installed.store(total, std::memory_order_release);
    hookItemNames();

    struct PendingSample { int type; std::string mod, name; };
    std::vector<PendingSample> pending;
    {
        std::lock_guard<std::mutex> l(g_mx);
        for (int i = installed; i < total; ++i) {
            Entry& reg = g_regs[static_cast<size_t>(i)];
            const int type = kVanillaItemCount + i;
            Il2CppObject* tex = loadPngTexture(reg.def.texture, &reg.width, &reg.height);
            Il2CppObject* asset = tex ? createAsset(tex, reg.def.mod + "/" + reg.def.name) : nullptr;
            if (asset) reg.asset = a.gchandle_new(asset, false);
            applyTexture(type, reg);
            applyName(type, reg);
            pending.push_back({type, reg.def.mod, reg.def.name});
        }
    }
    // FORA da trava: o SetDefaults da amostra passa pelo hook, entra no JS do
    // mod e volta em finishModItem, que pede a mesma trava — segura-la aqui
    // travaria o jogo no primeiro item.
    for (const PendingSample& n : pending) registerSample(n.type, n.mod, n.name);
    BL_INFO("itens de mod: %d instalado(s) (ids %d..%d), %d tabela(s) aumentadas de %d para %d",
            total - installed, from, to - 1, grown, from, to);
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

static std::vector<ModCategory> g_categories;   // protegido por g_mx

int modCategory(const std::string& mod, const std::string& name, const std::string& icon) {
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_categories.size(); ++i) {
        ModCategory& c = g_categories[i];
        if (c.mod == mod && c.name == name) {
            if (c.icon.empty()) c.icon = icon;
            return static_cast<int>(i);
        }
    }
    g_categories.push_back({mod, name, icon, {}});
    return static_cast<int>(g_categories.size()) - 1;
}

bool addToModCategory(int category, int type) {
    std::lock_guard<std::mutex> l(g_mx);
    if (category < 0 || static_cast<size_t>(category) >= g_categories.size()) return false;
    std::vector<int>& t = g_categories[static_cast<size_t>(category)].types;
    for (int x : t) if (x == type) return true;
    t.push_back(type);
    return true;
}

std::vector<ModCategory> modCategories() {
    std::lock_guard<std::mutex> l(g_mx);
    return g_categories;
}

std::vector<ModItemInfo> modItems() {
    std::lock_guard<std::mutex> l(g_mx);
    std::vector<ModItemInfo> v;
    v.reserve(g_regs.size());
    for (size_t i = 0; i < g_regs.size(); ++i) {
        v.push_back({kVanillaItemCount + static_cast<int>(i), g_regs[i].def.mod,
                     g_regs[i].def.name, g_regs[i].def.texture});
    }
    return v;
}

} // namespace bl::runtime
