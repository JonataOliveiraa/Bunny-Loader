#include "content/items/ModItemSorting.h"
#include "content/items/ModItems.h"
#include "content/common/GameRefs.h"
#include "content/common/TypeTables.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <cstring>

namespace bl::runtime {

namespace {

constexpr uint32_t kFieldStatic = 0x0010;  // FIELD_ATTRIBUTE_STATIC

struct Refs {
    bool tried = false, ok = false;
    Il2CppClass* layerCls = nullptr;     // ItemSorting.ItemSortingLayer
    Il2CppClass* layersCls = nullptr;    // ItemSorting.ItemSortingLayers (as camadas, estaticas)
    Il2CppClass* itemCls = nullptr;
    Il2CppClass* stringCls = nullptr;
    FieldInfo* whiteLists = nullptr;     // ItemSorting._layerWhiteLists (Dictionary<string, List<int>>)
    FieldInfo* layerIndex = nullptr;     // ItemSorting._layerIndexForItemType (int[])
    const MethodInfo* setupWhiteLists = nullptr;
    const MethodInfo* itemCtor = nullptr;
    const MethodInfo* setDefaults = nullptr;
    int32_t offName = -1, offMethod = -1;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    Il2CppClass* sorting = findClass({"Terraria.UI", "ItemSorting", {}});
    r.layerCls = sorting ? findNested(sorting, "ItemSortingLayer") : nullptr;
    r.layersCls = sorting ? findNested(sorting, "ItemSortingLayers") : nullptr;
    r.itemCls = findClass({"Terraria", "Item", {}});
    r.stringCls = findClass({"System", "String", {}});
    r.whiteLists = sorting ? findField(sorting, "_layerWhiteLists") : nullptr;
    r.layerIndex = sorting ? findField(sorting, "_layerIndexForItemType") : nullptr;
    r.setupWhiteLists = sorting ? api().class_get_method_from_name(sorting, "SetupWhiteLists", 0) : nullptr;
    r.itemCtor = r.itemCls ? findMethodBySignature(r.itemCls, parseSignature("void .ctor()")) : nullptr;
    r.setDefaults = r.itemCls ? findMethodBySignature(
        r.itemCls, parseSignature("void SetDefaults(int Type, ItemVariant variant)")) : nullptr;
    r.offName = r.layerCls ? fieldOffset(r.layerCls, "Name") : -1;
    r.offMethod = r.layerCls ? fieldOffset(r.layerCls, "SortingMethod") : -1;
    r.ok = r.layerCls && r.layersCls && r.itemCls && r.stringCls && r.whiteLists && r.layerIndex &&
           r.setupWhiteLists && r.itemCtor && r.setDefaults && r.offName >= 0 && r.offMethod >= 0;
    if (!r.ok) {
        BL_ERROR("ordenacao de itens de mod: refs faltando (ItemSortingLayer=%p ItemSortingLayers=%p "
                 "_layerWhiteLists=%p _layerIndexForItemType=%p SetupWhiteLists=%p); ordenar o "
                 "inventario apaga item de mod", (void*)r.layerCls, (void*)r.layersCls,
                 (void*)r.whiteLists, (void*)r.layerIndex, (void*)r.setupWhiteLists);
    }
    return r;
}

/** Metodo pelo objeto (as classes genericas sao as dele). Nulo se lancou. */
Il2CppObject* call(Il2CppObject* self, const char* name, int argc, void** args, const char* what,
                   bool* failed) {
    auto& a = il2cpp::api();
    const MethodInfo* m = a.class_get_method_from_name(a.object_get_class(self), name, argc);
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = m ? a.runtime_invoke(m, self, args, &exc) : nullptr;
    if (!m || exc) {
        BL_ERROR("ordenacao de itens de mod: %s.%s %s", what, name, m ? "lancou excecao" : "nao achado");
        *failed = true;
        return nullptr;
    }
    return r;
}

int unboxInt(Il2CppObject* o) {
    return o ? *reinterpret_cast<int*>(reinterpret_cast<char*>(o) + sizeof(Il2CppObject)) : 0;
}

bool sameString(Il2CppString* x, Il2CppString* y) {
    if (x == y) return true;
    return x && y && x->length == y->length &&
           std::memcmp(x->chars, y->chars, sizeof(char16_t) * static_cast<size_t>(x->length)) == 0;
}

/** A camada (estatica de ItemSortingLayers) com esse Name. As chaves do dicionario sao o proprio Name. */
Il2CppObject* layerNamed(Il2CppString* name) {
    auto& a = il2cpp::api();
    const Refs& r = refs();
    void* it = nullptr;
    while (FieldInfo* f = a.class_get_fields(r.layersCls, &it)) {
        if (!(a.field_get_flags(f) & kFieldStatic)) continue;
        if (a.class_from_il2cpp_type(a.field_get_type(f)) != r.layerCls) continue;
        Il2CppObject* layer = nullptr;
        a.field_static_get_value(f, &layer);
        if (layer && sameString(field<Il2CppString*>(layer, r.offName), name)) return layer;
    }
    return nullptr;
}

/** Garante `_layerIndexForItemType` com `size` posicoes (o SetupWhiteLists o refaz com ItemID.Count). */
Il2CppArray* layerIndexTable(int size) {
    auto& a = il2cpp::api();
    Il2CppArray* table = nullptr;
    a.field_static_get_value(refs().layerIndex, &table);
    if (!table || table->length >= static_cast<uintptr_t>(size)) return table;
    Il2CppArray* bigger = TypeTables::growArray(table, static_cast<uintptr_t>(size));
    if (bigger) a.field_static_set_value(refs().layerIndex, bigger);
    return bigger;
}

/**
 * O miolo do SetupWhiteLists para os tipos [from, to): um item de cada, as
 * camadas NA ORDEM do dicionario (cada uma tira da lista o que pegou, e a
 * ultima, "Last - Trash", pega o resto), e o tipo entra na lista branca da
 * camada que o pegou. O Validate das camadas filtra pela propria lista
 * branca; com o dicionario trocado por um vazio durante a conta, ele nao
 * filtra — como no SetupWhiteLists, que limpa o dicionario antes.
 */
void classify(int from, int to) {
    const Refs& r = refs();
    if (!r.ok || to <= from) return;
    auto& a = il2cpp::api();
    Il2CppObject* dict = nullptr;
    a.field_static_get_value(r.whiteLists, &dict);
    bool failed = false;
    const int layers = dict ? unboxInt(call(dict, "get_Count", 0, nullptr, "_layerWhiteLists", &failed)) : 0;
    if (failed || layers <= 0) return;   // SetupWhiteLists ainda nao rodou: o hook faz quando rodar

    Il2CppArray* keys = a.array_new(r.stringCls, static_cast<uintptr_t>(layers));
    Il2CppObject* keyCollection = call(dict, "get_Keys", 0, nullptr, "_layerWhiteLists", &failed);
    int zero = 0;
    void* copyArgs[2] = {keys, &zero};
    if (keyCollection) call(keyCollection, "CopyTo", 2, copyArgs, "_layerWhiteLists.Keys", &failed);
    if (failed || !keys) return;
    Il2CppString** names = static_cast<Il2CppString**>(arrayData(keys));

    // Os itens, e a lista de indices que as camadas vao consumindo.
    const int n = to - from;
    Il2CppArray* items = a.array_new(r.itemCls, static_cast<uintptr_t>(n));
    if (!items) return;
    for (int i = 0; i < n; ++i) {
        Il2CppObject* item = a.object_new(r.itemCls);
        Il2CppObject* exc = nullptr;
        a.runtime_invoke(r.itemCtor, item, nullptr, &exc);
        int type = from + i;
        void* sd[2] = {&type, nullptr};
        if (!exc) a.runtime_invoke(r.setDefaults, item, sd, &exc);
        if (exc) BL_ERROR("ordenacao de itens de mod: SetDefaults(%d) lancou excecao", type);
        a.gc_wbarrier_set_field(reinterpret_cast<Il2CppObject*>(items),
                                reinterpret_cast<void**>(static_cast<Il2CppObject**>(arrayData(items)) + i), item);
    }
    void* firstKey[1] = {names[0]};
    Il2CppObject* sample = call(dict, "get_Item", 1, firstKey, "_layerWhiteLists", &failed);
    if (failed || !sample) return;
    Il2CppObject* indices = a.object_new(a.object_get_class(sample));   // List<int>
    call(indices, ".ctor", 0, nullptr, "List<int>", &failed);
    for (int i = 0; i < n && !failed; ++i) {
        void* add[1] = {&i};
        call(indices, "Add", 1, add, "List<int>", &failed);
    }
    if (failed) return;

    Il2CppArray* layerIndex = layerIndexTable(to);
    if (!layerIndex) {
        BL_ERROR("ordenacao de itens de mod: sem _layerIndexForItemType");
        return;
    }
    int32_t* layerOf = static_cast<int32_t*>(arrayData(layerIndex));

    Il2CppObject* empty = a.object_new(a.object_get_class(dict));
    call(empty, ".ctor", 0, nullptr, "Dictionary", &failed);
    if (failed) return;
    a.field_static_set_value(r.whiteLists, empty);

    int placed = 0;
    for (int j = 0; j < layers; ++j) {
        Il2CppObject* layer = layerNamed(names[j]);
        void* key[1] = {names[j]};
        bool listFailed = false;
        Il2CppObject* whiteList = call(dict, "get_Item", 1, key, "_layerWhiteLists", &listFailed);
        Il2CppObject* method = layer ? field<Il2CppObject*>(layer, r.offMethod) : nullptr;
        if (!method || !whiteList) {
            BL_ERROR("ordenacao de itens de mod: camada %d sem objeto ou sem lista", j);
            continue;
        }
        void* args[3] = {layer, items, indices};
        Il2CppObject* picked = call(method, "Invoke", 3, args, "ItemSortingLayer.SortingMethod", &listFailed);
        if (!picked) continue;
        Il2CppClass* pickedCls = a.object_get_class(picked);
        const int32_t offItems = il2cpp::fieldOffset(pickedCls, "_items");
        const int32_t offSize = il2cpp::fieldOffset(pickedCls, "_size");
        Il2CppArray* arr = offItems >= 0 ? field<Il2CppArray*>(picked, offItems) : nullptr;
        const int size = offSize >= 0 ? field<int32_t>(picked, offSize) : 0;
        for (int k = 0; arr && k < size; ++k) {
            int type = from + static_cast<int32_t*>(arrayData(arr))[k];
            void* add[1] = {&type};
            call(whiteList, "Add", 1, add, "List<int>", &listFailed);
            layerOf[type] = j;
            ++placed;
        }
    }
    a.field_static_set_value(r.whiteLists, dict);

    if (placed < n) {
        BL_ERROR("ordenacao de itens de mod: %d de %d tipo(s) sem camada; ordenar o inventario os apaga",
                 n - placed, n);
    } else {
        BL_DEBUG("ordenacao de itens de mod: %d tipo(s) (%d..%d) nas camadas de ordenacao", n, from, to - 1);
    }
}

using SetupWhiteListsFn = void (*)(const MethodInfo*);
SetupWhiteListsFn g_origSetupWhiteLists = nullptr;

void hkSetupWhiteLists(const MethodInfo* m) {
    g_origSetupWhiteLists(m);
    classify(kVanillaItemCount, itemTypeCount());
}

} // namespace

void installModItemSorting(int from, int to) {
    const Refs& r = refs();
    if (!r.ok) return;
    if (!g_origSetupWhiteLists &&
        !hook::install(r.setupWhiteLists, hkSetupWhiteLists, &g_origSetupWhiteLists)) {
        BL_ERROR("ordenacao de itens de mod: sem hook em ItemSorting.SetupWhiteLists; se o jogo "
                 "refizer as listas, ordenar o inventario apaga item de mod");
    }
    classify(from, to);
}

} // namespace bl::runtime
