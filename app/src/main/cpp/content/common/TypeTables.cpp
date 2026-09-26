#include "content/common/TypeTables.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <cstring>
#include <unordered_map>

namespace bl::runtime {

namespace {

Il2CppArray* readStatic(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

const char* fieldName(FieldInfo* f) {
    return il2cpp::api().field_get_name ? il2cpp::api().field_get_name(f) : "?";
}

/** true se rodou sem excecao (metodo `void` devolve nulo, e isso nao e falha). */
bool invoke(const MethodInfo* m, void* self, void** args) {
    if (!m) return false;
    Il2CppObject* exc = nullptr;
    il2cpp::api().runtime_invoke(m, self, args, &exc);
    return exc == nullptr;
}

} // namespace

TypeTables::TypeTables(const char* what, int vanillaCount, std::vector<TableClass> classes, Fill fill)
    : what_(what), vanillaCount_(vanillaCount), fill_(fill), classes_(std::move(classes)) {}

namespace {

/** O indice de um elemento com o valor mais comum (bytes iguais) do array. */
uintptr_t modeIndex(const char* data, uintptr_t len, size_t elemSize) {
    if (len == 0 || elemSize > 8) return 0;
    std::unordered_map<uint64_t, std::pair<uintptr_t, uintptr_t>> count;   // valor -> (vezes, primeiro)
    uintptr_t best = 0, bestCount = 0;
    for (uintptr_t i = 0; i < len; ++i) {
        uint64_t v = 0;
        std::memcpy(&v, data + i * elemSize, elemSize);
        auto& c = count.try_emplace(v, 0, i).first->second;
        if (++c.first > bestCount) { bestCount = c.first; best = c.second; }
    }
    return best;
}

} // namespace

Il2CppArray* TypeTables::growArray(Il2CppArray* old, uintptr_t newLength, Fill fill) {
    auto& a = il2cpp::api();
    Il2CppClass* elem = a.class_get_element_class(a.object_get_class(reinterpret_cast<Il2CppObject*>(old)));
    Il2CppArray* n = a.array_new(elem, newLength);
    if (!n) return nullptr;
    const uintptr_t len = old->length;

    if (a.class_is_valuetype(elem)) {
        // Tipo de valor: bytes. O [0] (o "nada") e o valor de quem ainda nao
        // disse nada — o mesmo que o tipo novo teria se o jogo o conhecesse.
        uint32_t align = 0;
        const size_t elemSize = static_cast<size_t>(a.class_value_size(elem, &align));
        char* d = static_cast<char*>(arrayData(n));
        const char* s = static_cast<const char*>(arrayData(old));
        std::memcpy(d, s, elemSize * len);
        const char* model = s + (fill == Fill::Mode ? modeIndex(s, len, elemSize) : 0) * elemSize;
        for (uintptr_t i = len; i < newLength; ++i) std::memcpy(d + i * elemSize, model, elemSize);
        return n;
    }
    // Referencia: pelo proprio runtime (Array.Copy / SetValue), que passa pela
    // barreira de escrita do coletor. Copiar ponteiro com memcpy num heap com
    // GC incremental pode deixar o objeto sem ninguem que o marque.
    static const MethodInfo* copy = nullptr;
    static const MethodInfo* set = nullptr;
    if (!copy || !set) {
        Il2CppClass* array = il2cpp::findClass({"System", "Array", {}});
        copy = array ? il2cpp::findMethodBySignature(
            array, il2cpp::parseSignature("void Copy(Array sourceArray, Array destinationArray, int length)")) : nullptr;
        set = array ? il2cpp::findMethodBySignature(
            array, il2cpp::parseSignature("void SetValue(object value, int index)")) : nullptr;
    }
    int copyLength = static_cast<int>(len);
    void* args[3] = {old, n, &copyLength};
    if (!invoke(copy, nullptr, args)) return nullptr;
    auto** refsOld = reinterpret_cast<Il2CppObject**>(arrayData(old));
    const uintptr_t pick = fill == Fill::Mode ? modeIndex(reinterpret_cast<const char*>(refsOld), len, sizeof(void*)) : 0;
    Il2CppObject* zero = len > 0 ? refsOld[pick] : nullptr;
    for (uintptr_t i = len; i < newLength; ++i) {
        int idx = static_cast<int>(i);
        void* sv[2] = {zero, &idx};
        if (!invoke(set, n, sv)) return nullptr;
    }
    return n;
}

Il2CppArray* TypeTables::resizedCopy(Il2CppArray* old, uintptr_t newLength) {
    auto& a = il2cpp::api();
    Il2CppClass* elem = a.class_get_element_class(a.object_get_class(reinterpret_cast<Il2CppObject*>(old)));
    Il2CppArray* n = a.array_new(elem, newLength);   // ja zerado
    if (!n) return nullptr;
    int copyLength = static_cast<int>(old->length < newLength ? old->length : newLength);
    if (copyLength == 0) return n;
    // Pelo Array.Copy do jogo: referencia (ou struct com referencia dentro)
    // passa pela barreira de escrita do coletor.
    static const MethodInfo* copy = nullptr;
    if (!copy) {
        Il2CppClass* array = il2cpp::findClass({"System", "Array", {}});
        copy = array ? il2cpp::findMethodBySignature(
            array, il2cpp::parseSignature("void Copy(Array sourceArray, Array destinationArray, int length)")) : nullptr;
    }
    void* args[3] = {old, n, &copyLength};
    return invoke(copy, nullptr, args) ? n : nullptr;
}

void TypeTables::growInstanceTable(Il2CppObject* obj, int32_t offset, int vanillaCount, int size,
                                   const char* what) {
    if (!obj || offset < 0) return;
    auto& a = il2cpp::api();
    auto** slot = reinterpret_cast<Il2CppArray**>(reinterpret_cast<char*>(obj) + offset);
    Il2CppArray* old = *slot;
    if (!old || old->length != static_cast<uintptr_t>(vanillaCount) ||
        old->length >= static_cast<uintptr_t>(size)) {
        return;
    }
    Il2CppClass* elem = a.class_get_element_class(a.object_get_class(reinterpret_cast<Il2CppObject*>(old)));
    if (!a.class_is_valuetype(elem)) return;
    Il2CppArray* n = a.array_new(elem, static_cast<uintptr_t>(size));   // ja zerado
    if (!n) return;
    uint32_t align = 0;
    const size_t elemSize = static_cast<size_t>(a.class_value_size(elem, &align));
    std::memcpy(arrayData(n), arrayData(old), elemSize * old->length);
    if (a.gc_wbarrier_set_field) a.gc_wbarrier_set_field(obj, reinterpret_cast<void**>(slot), n);
    else *slot = n;
    if (what) BL_INFO("%s aumentada de %d para %d", what, vanillaCount, size);
}

void TypeTables::find(uintptr_t size) {
    auto& a = il2cpp::api();
    if (!a.class_get_fields || !a.field_get_flags) {
        BL_ERROR("%s: il2cpp sem enumeracao de campos; nao da para achar as tabelas", what_);
        return;
    }
    for (const TableClass& c : classes_) {
        // Quieto: classe que nao existe nesta versao so fica de fora.
        Il2CppClass* cls = il2cpp::findClassQuiet(c.ns, c.name);
        if (cls && c.nested[0]) cls = il2cpp::findNested(cls, c.nested);
        if (cls && c.nested2[0]) cls = il2cpp::findNested(cls, c.nested2);
        if (!cls) continue;
        // O construtor estatico primeiro: sem ele as tabelas da classe ainda
        // sao nulas (ProjectileID.Sets inteira, na primeira vez), ficam como
        // "pendentes" e so crescem no quadro seguinte — depois do
        // SetStaticDefaults do mod, que a esta altura ja leu e escreveu alem
        // do fim. Com a classe iniciada aqui, elas crescem junto com o resto.
        if (a.runtime_class_init) a.runtime_class_init(cls);
        int found = 0, pending = 0;
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
            if (!arr) {
                pending_.push_back(f);
                ++pending;
                continue;
            }
            if (arr->length != size) continue;
            tables_.push_back(f);
            ++found;
        }
        BL_INFO("%s: %s.%s%s%s%s%s: %d tabela(s), %d ainda nula(s)", what_, c.ns, c.name,
                c.nested[0] ? "." : "", c.nested, c.nested2[0] ? "." : "", c.nested2, found, pending);
    }
}

int TypeTables::grow(int from, int to) {
    if (!searched_) {
        searched_ = true;
        find(static_cast<uintptr_t>(from));
    }
    if (tables_.empty()) return -1;
    int grown = 0;
    for (FieldInfo* f : tables_) {
        Il2CppArray* arr = readStatic(f);
        if (!arr || arr->length != static_cast<uintptr_t>(from)) continue;
        Il2CppArray* bigger = growArray(arr, static_cast<uintptr_t>(to), fill_);
        if (!bigger) continue;
        il2cpp::api().field_static_set_value(f, bigger);
        ++grown;
    }
    return grown;
}

void TypeTables::checkPending(int size) {
    // A cada quadro, e nao a cada 2 s como o watch: entre a criacao e o
    // aumento, uma consulta pelo tipo novo le fora do array. Custa uma leitura
    // de campo estatico por pendente.
    for (size_t i = 0; i < pending_.size();) {
        FieldInfo* f = pending_[i];
        Il2CppArray* arr = readStatic(f);
        if (!arr) { ++i; continue; }
        pending_[i] = pending_.back();
        pending_.pop_back();
        if (arr->length != static_cast<uintptr_t>(vanillaCount_)) continue;   // nao e deste tipo
        Il2CppArray* bigger = growArray(arr, static_cast<uintptr_t>(size), fill_);
        if (!bigger) continue;
        il2cpp::api().field_static_set_value(f, bigger);
        tables_.push_back(f);
        BL_INFO("%s: tabela %s criada pelo jogo; aumentada", what_, fieldName(f));
    }
}

void TypeTables::watch(int size, Regrown onRegrown) {
    for (FieldInfo* f : tables_) {
        Il2CppArray* arr = readStatic(f);
        if (!arr || arr->length >= static_cast<uintptr_t>(size)) continue;
        if (arr->length != static_cast<uintptr_t>(vanillaCount_)) continue;   // nao e nossa
        Il2CppArray* bigger = growArray(arr, static_cast<uintptr_t>(size), fill_);
        if (!bigger) continue;
        il2cpp::api().field_static_set_value(f, bigger);
        BL_INFO("%s: tabela %s refeita pelo jogo; aumentada de novo", what_, fieldName(f));
        if (onRegrown) onRegrown(f, size);
    }
}

} // namespace bl::runtime
