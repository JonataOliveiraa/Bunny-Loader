#include "script/bridge/Value.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "script/bridge/Bridge.h"
#include "script/bridge/Invoke.h"
#include "script/bridge/Marshal.h"
#include "script/bridge/Members.h"

#include <cmath>
#include <cstdlib>
#include <cstring>
#include <unordered_map>
#include <vector>

namespace bl::script {

namespace {

JSClassID g_structId;

/**
 * Um struct do jogo visto pelo JS.
 *
 * Tres origens, com semanticas diferentes de proposito:
 *   - VISTA: `data` aponta para dentro de `owner` (campo de objeto, elemento
 *     de array). Escrever altera o jogo de verdade.
 *   - COPIA: `owned` e nosso buffer. Escrever nao vai a lugar nenhum — que e a
 *     semantica de valor do C# para retorno de metodo e argumento de hook.
 *   - ESTATICO: copia mais `staticField`, devolvida ao jogo a cada escrita,
 *     porque o IL2CPP nao entrega o endereco do bloco de estaticos.
 */
struct StructRef {
    Il2CppClass* cls = nullptr;
    void* data = nullptr;
    size_t size = 0;
    JSValue owner = JS_UNDEFINED;
    void* owned = nullptr;
    FieldInfo* staticField = nullptr;
    StructRef* next = nullptr;   // so enquanto esta na lista de reuso
};

StructRef* refOf(JSValueConst v) {
    return static_cast<StructRef*>(JS_GetOpaque(v, g_structId));
}

// `npc.position.X` cria uma vista, le e joga fora — e o padrao mais comum de
// mod. Reciclar o StructRef tira um malloc/free de cada leitura dessas. Nao
// precisa de trava: so se chega aqui com o motor JS travado, e o finalizador
// roda dentro da coleta, que tambem e.
StructRef* g_livres = nullptr;
int g_livresN = 0;
constexpr int kPoolMax = 64;

StructRef* takeRef() {
    if (!g_livres) return new StructRef();
    StructRef* r = g_livres;
    g_livres = r->next;
    --g_livresN;
    *r = StructRef{};
    return r;
}

void giveRef(StructRef* r) {
    if (g_livresN >= kPoolMax) { delete r; return; }
    r->next = g_livres;
    g_livres = r;
    ++g_livresN;
}

StructRef* newRef(JSContext* ctx, JSValue* out, Il2CppClass* cls, size_t size) {
    JSValue v = JS_NewObjectClass(ctx, g_structId);
    if (JS_IsException(v)) { *out = v; return nullptr; }
    auto* r = takeRef();
    r->cls = cls;
    r->size = size;
    JS_SetOpaque(v, r);
    *out = v;
    return r;
}

void gs_finalizer(JSRuntime* rt, JSValue val) {
    auto* r = refOf(val);
    if (!r) return;
    JS_FreeValueRT(rt, r->owner);
    std::free(r->owned);
    giveRef(r);
}

/** Depois de escrever: um estatico so existe no jogo se for devolvido. */
void flush(StructRef* r) {
    if (r->staticField) il2cpp::api().field_static_set_value(r->staticField, r->data);
}

JSValue gs_toString(JSContext* ctx, JSValueConst self, int, JSValueConst*) {
    auto* r = refOf(self);
    auto& a = il2cpp::api();
    if (!r) return JS_NewString(ctx, "[GameStruct]");
    const char* cn = a.class_get_name(r->cls);
    std::string s = cn ? cn : "struct";
    s += "(";
    if (a.class_get_fields) {
        void* iter = nullptr;
        bool first = true;
        while (FieldInfo* f = a.class_get_fields(r->cls, &iter)) {
            if (a.field_get_flags && (a.field_get_flags(f) & 0x0010)) continue;  // estatico
            const TypeDesc& d = describe(a.field_get_type(f));
            if (d.prim == Prim::Struct) continue;
            JSValue v = readAt(ctx, static_cast<char*>(r->data) +
                                        structFieldOffset(r->cls, a.field_get_offset(f)),
                               d, JS_UNDEFINED);
            const char* txt = JS_ToCString(ctx, v);
            if (!first) s += ", ";
            first = false;
            if (a.field_get_name) { s += a.field_get_name(f); s += "="; }
            s += txt ? txt : "?";
            if (txt) JS_FreeCString(ctx, txt);
            JS_FreeValue(ctx, v);
        }
    }
    s += ")";
    return JS_NewString(ctx, s.c_str());
}

// ------------------------- struct com indexador -------------------------
//
// `proj.ai[0]`, `proj.oldPos[3].X`, `player.hideMisc[1]` — ver Indexer
// (Members.h). Fora do limite e RangeError, como no array do jogo: o jogo
// nao confere nada, entao `ai[5]` leria o campo vizinho sem erro.

const char* className(StructRef* r) {
    const char* n = il2cpp::api().class_get_name(r->cls);
    return n ? n : "struct";
}

/** Limite do modo METODOS: o get_Length do struct, se houver; -1 = sem limite. */
int64_t methodBound(JSContext* ctx, StructRef* r, const Indexer& ix) {
    if (!ix.length) return -1;
    JSValue got = invokeMethod(ctx, ix.length, r->data, 0, nullptr);
    int64_t n = -1;
    if (JS_IsException(got)) JS_FreeValue(ctx, JS_GetException(ctx));
    else if (JS_ToInt64(ctx, &n, got) < 0) n = -1;
    JS_FreeValue(ctx, got);
    return n;
}

JSValue indexGet(JSContext* ctx, JSValueConst obj, StructRef* r, int64_t idx) {
    const Indexer* ix = indexerOf(r->cls);
    if (!ix) return JS_UNDEFINED;
    const int64_t bound = ix->fields() ? ix->count : methodBound(ctx, r, *ix);
    if (bound >= 0 && idx >= bound) {
        return JS_ThrowRangeError(ctx, "%s: indice %lld fora de 0..%lld", className(r),
                                  static_cast<long long>(idx), static_cast<long long>(bound - 1));
    }
    if (ix->fields()) {
        // Dono = esta vista: um elemento struct sai como vista para dentro dela.
        return readAt(ctx, static_cast<char*>(r->data) + ix->offset + static_cast<size_t>(idx) * ix->stride,
                      *ix->elem, obj);
    }
    JSValue arg = JS_NewInt64(ctx, idx);
    JSValue got = invokeMethod(ctx, ix->get, r->data, 1, &arg);
    JS_FreeValue(ctx, arg);
    return got;
}

int indexSet(JSContext* ctx, StructRef* r, int64_t idx, JSValueConst value) {
    const Indexer* ix = indexerOf(r->cls);
    if (!ix) {
        JS_ThrowTypeError(ctx, "%s nao tem indexador ([i])", className(r));
        return -1;
    }
    const int64_t bound = ix->fields() ? ix->count : methodBound(ctx, r, *ix);
    if (bound >= 0 && idx >= bound) {
        JS_ThrowRangeError(ctx, "%s: indice %lld fora de 0..%lld", className(r),
                           static_cast<long long>(idx), static_cast<long long>(bound - 1));
        return -1;
    }
    if (ix->fields()) {
        const int ok = writeAt(ctx, static_cast<char*>(r->data) + ix->offset + static_cast<size_t>(idx) * ix->stride,
                               *ix->elem, value);
        if (ok > 0) flush(r);
        return ok;
    }
    if (!ix->set) {
        JS_ThrowTypeError(ctx, "%s: o indexador e so de leitura", className(r));
        return -1;
    }
    JSValue index = JS_NewInt64(ctx, idx);
    JSValueConst args[2] = {index, value};
    JSValue got = invokeMethod(ctx, ix->set, r->data, 2, args);
    JS_FreeValue(ctx, index);
    if (JS_IsException(got)) return -1;
    JS_FreeValue(ctx, got);
    flush(r);
    return true;
}

JSValue gs_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    auto* r = refOf(obj);
    if (!r) return JS_UNDEFINED;
    const int64_t idx = indexOf(ctx, atom);
    if (idx >= 0) return indexGet(ctx, obj, r, idx);
    const Member& m = member(ctx, r->cls, atom, Space::Struct, g_structId);

    if (m.proto) return protoGet(ctx, g_structId, atom);
    if (m.field) return readAt(ctx, static_cast<char*>(r->data) + m.offset, *m.type, obj);
    if (m.method) return makeGameMethod(ctx, m.method);
    // Propriedade C# do struct: o `this` de um value type sao os dados.
    if (m.getter) return invokeMethod(ctx, m.getter, r->data, 0, nullptr);
    if (m.signature) {
        return JS_ThrowTypeError(ctx, "metodo nao encontrado em %s: %s",
                                 il2cpp::api().class_get_name(r->cls), atomName(ctx, atom).c_str());
    }
    return JS_UNDEFINED;
}

int gs_exotic_set(JSContext* ctx, JSValueConst obj, JSAtom atom,
                  JSValueConst value, JSValueConst, int) {
    auto* r = refOf(obj);
    if (!r) return -1;
    auto& a = il2cpp::api();
    const int64_t idx = indexOf(ctx, atom);
    if (idx >= 0) return indexSet(ctx, r, idx, value);
    const Member& m = member(ctx, r->cls, atom, Space::Struct, g_structId);
    if (m.field) {
        int ok = writeAt(ctx, static_cast<char*>(r->data) + m.offset, *m.type, value);
        if (ok > 0) flush(r);
        return ok;
    }
    if (m.setter) {
        JSValue got = invokeMethod(ctx, m.setter, r->data, 1, &value);
        if (JS_IsException(got)) return -1;
        JS_FreeValue(ctx, got);
        flush(r);
        return true;
    }
    JS_ThrowTypeError(ctx, "%s nao tem o campo %s", a.class_get_name(r->cls),
                      atomName(ctx, atom).c_str());
    return -1;
}

const JSClassExoticMethods gs_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr, gs_exotic_get, gs_exotic_set,
};

const JSCFunctionListEntry gs_proto[] = {
    JS_CFUNC_DEF("toString", 0, gs_toString),
};

} // namespace

// ============================== TypeDesc ==============================

/**
 * Quanto descontar do offset de campo para chegar aos DADOS de um struct.
 *
 * O IL2CPP conta offset a partir do inicio do OBJETO. Para um value type isso
 * poderia ou nao incluir o cabecalho de 16 bytes da versao encaixotada — e a
 * resposta muda entre versoes do runtime. Chutar errado le 16 bytes fora do
 * lugar, sem erro nenhum: o campo vizinho aparece no lugar do certo.
 *
 * Entao perguntamos a propria classe. Se algum campo de instancia TERMINA
 * depois do tamanho do valor, o offset so pode estar contando o cabecalho.
 */
size_t headerAdjustOf(Il2CppClass* cls) {
    static std::unordered_map<Il2CppClass*, size_t> cache;
    auto it = cache.find(cls);
    if (it != cache.end()) return it->second;

    auto& a = il2cpp::api();
    size_t adjust = 0;
    uint32_t align = 0;
    size_t valueSize = a.class_value_size ? static_cast<size_t>(a.class_value_size(cls, &align)) : 0;
    if (!a.class_get_fields || !valueSize) {
        // Sem a enumeracao de campos sobra a heuristica antiga.
        adjust = sizeof(Il2CppObject);
    } else {
        void* iter = nullptr;
        while (FieldInfo* f = a.class_get_fields(cls, &iter)) {
            if (a.field_get_flags && (a.field_get_flags(f) & 0x0010)) continue;  // estatico
            const TypeDesc& d = describe(a.field_get_type(f));
            if (a.field_get_offset(f) + d.size > valueSize) {
                adjust = sizeof(Il2CppObject);
                break;
            }
        }
    }
    const char* n = a.class_get_name(cls);
    BL_DEBUG("struct %s: valor=%zu bytes, offsets %s cabecalho",
             n ? n : "?", valueSize, adjust ? "COM" : "sem");
    cache[cls] = adjust;
    return adjust;
}

size_t structFieldOffset(Il2CppClass* cls, size_t fieldOffset) {
    size_t adjust = headerAdjustOf(cls);
    return fieldOffset >= adjust ? fieldOffset - adjust : fieldOffset;
}

namespace {

/**
 * `System.Nullable<T>`: acha o T e onde moram `hasValue` e `value`.
 *
 * Pelos campos, e nao por um layout suposto: o valor de um `double?` fica em
 * 8, o de um `char?` em 2 — o alinhamento e do T.
 */
void describeNullable(TypeDesc& d) {
    auto& a = il2cpp::api();
    const char* name = a.class_get_name(d.cls);
    const char* ns = a.class_get_namespace(d.cls);
    if (!name || !ns || std::strcmp(name, "Nullable`1") != 0 || std::strcmp(ns, "System") != 0) {
        return;
    }
    if (!a.class_get_fields) return;
    FieldInfo* hasValue = nullptr;
    FieldInfo* value = nullptr;
    void* iter = nullptr;
    while (FieldInfo* f = a.class_get_fields(d.cls, &iter)) {
        if (a.field_get_flags && (a.field_get_flags(f) & 0x0010)) continue;  // estatico
        const char* fn = a.field_get_name(f);
        if (!fn) continue;
        if (std::strcmp(fn, "hasValue") == 0) hasValue = f;
        else if (std::strcmp(fn, "value") == 0) value = f;
    }
    if (!hasValue || !value) {
        BL_WARN("tipos: %s sem hasValue/value; tratado como struct comum", d.name.c_str());
        return;
    }
    d.hasValueOffset = structFieldOffset(d.cls, a.field_get_offset(hasValue));
    d.valueOffset = structFieldOffset(d.cls, a.field_get_offset(value));
    d.inner = &describe(a.field_get_type(value));
}

TypeDesc describeUncached(const Il2CppType* t) {
    auto& a = il2cpp::api();
    TypeDesc d;
    if (!t) return d;
    char* raw = a.type_get_name(t);
    d.name = raw ? raw : "";
    if (raw) a.il2cpp_free(raw);

    std::string n = d.name;
    if (!n.empty() && (n.back() == '&' || n.back() == '*')) {
        d.byRef = true;
        n.pop_back();
        d.name = n;
    }

    struct P { const char* clr; Prim prim; size_t size; };
    static const P kPrims[] = {
        {"System.Void", Prim::Void, 0},     {"System.Boolean", Prim::Bool, 1},
        {"System.SByte", Prim::I8, 1},      {"System.Byte", Prim::U8, 1},
        {"System.Int16", Prim::I16, 2},     {"System.UInt16", Prim::U16, 2},
        {"System.Char", Prim::Char, 2},     {"System.Int32", Prim::I32, 4},
        {"System.UInt32", Prim::U32, 4},    {"System.Int64", Prim::I64, 8},
        {"System.UInt64", Prim::U64, 8},    {"System.IntPtr", Prim::I64, 8},
        {"System.UIntPtr", Prim::U64, 8},   {"System.Single", Prim::F32, 4},
        {"System.Double", Prim::F64, 8},
    };
    for (const auto& p : kPrims) {
        if (n == p.clr) {
            d.prim = p.prim;
            d.size = p.size;
            d.byValue = true;
            return d;
        }
    }
    if (n == "System.String") { d.prim = Prim::String; d.size = sizeof(void*); return d; }
    if (n.size() > 2 && n.compare(n.size() - 2, 2, "[]") == 0) {
        d.prim = Prim::Array;
        d.size = sizeof(void*);
        // A classe do array (int[]): e dela que sai o elemento quando um
        // array JS chega onde o jogo quer um array (listFromJS).
        d.cls = a.class_from_il2cpp_type ? a.class_from_il2cpp_type(t) : nullptr;
        return d;
    }

    d.cls = a.class_from_il2cpp_type ? a.class_from_il2cpp_type(t) : nullptr;
    if (!d.cls) { d.prim = Prim::Object; return d; }

    if (a.class_is_enum && a.class_is_enum(d.cls)) {
        // O nome do enum nao diz o tamanho: Terraria tem enum de byte e de int.
        TypeDesc u;
        if (a.class_enum_basetype) u = describe(a.class_enum_basetype(d.cls));
        if (!u.byValue) {
            u.prim = Prim::I32;
            u.size = 4;
            u.byValue = true;
        }
        u.cls = d.cls;
        u.isEnum = true;
        u.byRef = d.byRef;
        u.name = d.name;
        return u;
    }
    if (a.class_is_valuetype && a.class_is_valuetype(d.cls)) {
        d.prim = Prim::Struct;
        d.byValue = true;
        uint32_t align = 0;
        d.size = a.class_value_size ? static_cast<size_t>(a.class_value_size(d.cls, &align))
                                    : sizeof(void*);
        describeNullable(d);
        return d;
    }
    d.prim = Prim::Object;
    d.size = sizeof(void*);
    return d;
}

} // namespace

const TypeDesc& describe(const Il2CppType* t) {
    static const TypeDesc kNone{};
    if (!t) return kNone;
    // unordered_map guarda cada par num no proprio: a referencia sobrevive a
    // rehash, e a recursao (enum -> tipo subjacente) pode inserir a vontade.
    static std::unordered_map<const Il2CppType*, TypeDesc> cache;
    auto it = cache.find(t);
    if (it != cache.end()) return it->second;
    TypeDesc d = describeUncached(t);
    return cache.emplace(t, std::move(d)).first->second;
}

int hfaOf(Il2CppClass* cls, bool* isDouble) {
    auto& a = il2cpp::api();
    if (!cls || !a.class_get_fields) return 0;
    int count = 0;
    bool sawDouble = false, sawFloat = false;
    void* iter = nullptr;
    while (FieldInfo* f = a.class_get_fields(cls, &iter)) {
        if (a.field_get_flags && (a.field_get_flags(f) & 0x0010)) continue;  // estatico
        const TypeDesc& d = describe(a.field_get_type(f));
        if (d.prim == Prim::F32) { sawFloat = true; ++count; }
        else if (d.prim == Prim::F64) { sawDouble = true; ++count; }
        else if (d.prim == Prim::Struct) {
            bool inner = false;
            int k = hfaOf(d.cls, &inner);
            if (!k) return 0;
            if (inner) sawDouble = true; else sawFloat = true;
            count += k;
        } else {
            return 0;
        }
        if (count > 4) return 0;
    }
    if (count == 0 || (sawFloat && sawDouble)) return 0;
    if (isDouble) *isDouble = sawDouble;
    return count;
}

// ============================ ler / escrever ============================

namespace {

/** Como chamar o que veio do JS, numa mensagem de erro. */
const char* kindOf(JSContext* ctx, JSValueConst v) {
    if (JS_IsNull(v)) return "null";
    if (JS_IsUndefined(v)) return "undefined";
    if (JS_IsFunction(ctx, v)) return "uma funcao";
    if (JS_IsString(v)) return "um texto que nao e numero";
    if (structDataOf(v, nullptr, nullptr)) return "um struct do jogo";
    if (objectFromJS(v)) return "um objeto do jogo";
    if (JS_IsObject(v)) return "um objeto";
    return "isso";
}

/**
 * JS -> numero, RECUSANDO o que nao e numero.
 *
 * Sem isto, `JS_ToInt64` engolia qualquer coisa: objeto virava 0, "abc" virava
 * 0, NaN virava 0 e 1e20 virava 1661992960 — tudo escrito no campo do jogo sem
 * um pio. Um `item.damage = algumNPC` que devolve zero e pior que um erro,
 * porque so aparece como comportamento estranho horas depois.
 */
int toNumber(JSContext* ctx, JSValueConst v, const TypeDesc& d, double* out) {
    double x = 0;
    if (JS_IsNumber(v)) {
        JS_ToFloat64(ctx, &x, v);
    } else if (JS_IsBool(v)) {
        x = JS_ToBool(ctx, v) ? 1.0 : 0.0;
    } else if (JS_IsString(v)) {
        // So aceita se o texto INTEIRO for um numero: "42" passa, "42abc" nao.
        const char* cs = JS_ToCString(ctx, v);
        if (!cs) return -1;
        char* fim = nullptr;
        x = std::strtod(cs, &fim);
        bool inteiro = fim && fim != cs;
        while (inteiro && *fim == ' ') ++fim;
        if (!inteiro || *fim != '\0') {
            JS_ThrowTypeError(ctx, "%s espera um numero, recebeu o texto \"%s\"",
                              d.name.c_str(), cs);
            JS_FreeCString(ctx, cs);
            return -1;
        }
        JS_FreeCString(ctx, cs);
    } else {
        JS_ThrowTypeError(ctx, "%s espera um numero, recebeu %s", d.name.c_str(),
                          kindOf(ctx, v));
        return -1;
    }

    // NaN e infinito dentro de um campo do jogo envenenam a fisica em silencio:
    // a posicao vira NaN e a entidade some do mundo sem erro nenhum.
    if (std::isnan(x) || std::isinf(x)) {
        JS_ThrowRangeError(ctx, "%s nao aceita %s", d.name.c_str(),
                           std::isnan(x) ? "NaN" : "infinito");
        return -1;
    }
    *out = x;
    return true;
}

/** Limites do inteiro de destino, para recusar o que nao cabe. */
bool fits(Prim p, double x, double* lo, double* hi) {
    switch (p) {
        case Prim::I8:  *lo = -128.0; *hi = 127.0; break;
        case Prim::U8:  *lo = 0.0; *hi = 255.0; break;
        case Prim::I16: *lo = -32768.0; *hi = 32767.0; break;
        case Prim::U16:
        case Prim::Char: *lo = 0.0; *hi = 65535.0; break;
        case Prim::I32: *lo = -2147483648.0; *hi = 2147483647.0; break;
        case Prim::U32: *lo = 0.0; *hi = 4294967295.0; break;
        case Prim::I64: *lo = -9223372036854775808.0; *hi = 9223372036854775807.0; break;
        case Prim::U64: *lo = 0.0; *hi = 18446744073709551615.0; break;
        default: *lo = 0; *hi = 0; return true;
    }
    // Trunca como o C# faria; o que se recusa e o que nao CABE.
    double t = x < 0 ? std::ceil(x) : std::floor(x);
    return t >= *lo && t <= *hi;
}

/**
 * `null`/`undefined` -> sem valor; um Nullable do mesmo tipo -> copiado; o
 * resto e o T, com hasValue ligado.
 *
 * Monta num temporario: um T recusado (texto para um `float?`) nao pode
 * deixar hasValue ligado com o valor antigo.
 */
int writeNullable(JSContext* ctx, char* b, const TypeDesc& d, JSValueConst v) {
    if (JS_IsNull(v) || JS_IsUndefined(v)) {
        std::memset(b, 0, d.size);
        return true;
    }
    Il2CppClass* src = nullptr;
    size_t srcSize = 0;
    if (void* data = structDataOf(v, &src, &srcSize)) {
        if (src == d.cls) {
            std::memcpy(b, data, d.size < srcSize ? d.size : srcSize);
            return true;
        }
    }
    std::vector<char> tmp(d.size, 0);
    if (writeAt(ctx, tmp.data() + d.valueOffset, *d.inner, v) < 0) return -1;
    tmp[d.hasValueOffset] = 1;
    std::memcpy(b, tmp.data(), d.size);
    return true;
}

/**
 * Array JS onde o jogo quer um array: `new RecipeGroup(nome, [1, 2])` sem
 * montar o int[] na mao. Um array novo do tipo que o parametro declara, cada
 * elemento escrito como um campo desse tipo (numero, texto, objeto, struct).
 */
Il2CppArray* listFromJS(JSContext* ctx, JSValueConst v, const TypeDesc& d) {
    auto& a = il2cpp::api();
    Il2CppClass* elem = d.cls && a.class_get_element_class ? a.class_get_element_class(d.cls) : nullptr;
    if (!elem) {
        JS_ThrowTypeError(ctx, "%s: tipo do elemento desconhecido", d.name.c_str());
        return nullptr;
    }
    int64_t len = 0;
    JSValue lv = JS_GetPropertyStr(ctx, v, "length");
    const int bad = JS_ToInt64(ctx, &len, lv);
    JS_FreeValue(ctx, lv);
    if (bad < 0 || len < 0) return nullptr;
    Il2CppArray* arr = a.array_new(elem, static_cast<uintptr_t>(len));
    if (!arr) {
        JS_ThrowInternalError(ctx, "%s: array_new falhou", d.name.c_str());
        return nullptr;
    }
    const TypeDesc& ed = describe(a.class_get_type(elem));
    const size_t stride = ed.byValue ? ed.size : sizeof(void*);
    auto* data = static_cast<char*>(arrayData(arr));
    for (int64_t i = 0; i < len; ++i) {
        JSValue x = JS_GetPropertyUint32(ctx, v, static_cast<uint32_t>(i));
        const int r = writeAt(ctx, data + static_cast<size_t>(i) * stride, ed, x);
        JS_FreeValue(ctx, x);
        if (r < 0) return nullptr;
    }
    return arr;
}

} // namespace

JSValue readAt(JSContext* ctx, void* p, const TypeDesc& d, JSValueConst owner) {
    auto* b = static_cast<char*>(p);
    switch (d.prim) {
        case Prim::Void:   return JS_UNDEFINED;
        case Prim::Bool:   return JS_NewBool(ctx, *reinterpret_cast<uint8_t*>(b) != 0);
        case Prim::I8:     return JS_NewInt32(ctx, *reinterpret_cast<int8_t*>(b));
        case Prim::U8:     return JS_NewInt32(ctx, *reinterpret_cast<uint8_t*>(b));
        case Prim::I16:    return JS_NewInt32(ctx, *reinterpret_cast<int16_t*>(b));
        case Prim::U16:
        case Prim::Char:   return JS_NewInt32(ctx, *reinterpret_cast<uint16_t*>(b));
        case Prim::I32:    return JS_NewInt32(ctx, *reinterpret_cast<int32_t*>(b));
        case Prim::U32:    return JS_NewInt64(ctx, *reinterpret_cast<uint32_t*>(b));
        case Prim::I64:    return JS_NewInt64(ctx, *reinterpret_cast<int64_t*>(b));
        case Prim::U64:
            return JS_NewInt64(ctx, static_cast<int64_t>(*reinterpret_cast<uint64_t*>(b)));
        case Prim::F32:    return JS_NewFloat64(ctx, *reinterpret_cast<float*>(b));
        case Prim::F64:    return JS_NewFloat64(ctx, *reinterpret_cast<double*>(b));
        case Prim::String: {
            auto* s = *reinterpret_cast<Il2CppString**>(b);
            return s ? JS_NewString(ctx, stringToUtf8(s).c_str()) : JS_NULL;
        }
        case Prim::Array: {
            auto* arr = *reinterpret_cast<Il2CppArray**>(b);
            return arr ? makeGameArray(ctx, arr) : JS_NULL;
        }
        case Prim::Struct:
            if (d.nullable()) {
                // Copia, nao vista: `Nullable<T>.Value` do C# tambem e copia,
                // e escrever no T sem mexer em hasValue deixaria os dois
                // desencontrados.
                if (!*reinterpret_cast<uint8_t*>(b + d.hasValueOffset)) return JS_NULL;
                return readAt(ctx, b + d.valueOffset, *d.inner, JS_UNDEFINED);
            }
            // Sem dono conhecido a vista ficaria pendurada quando o buffer
            // sumisse; copia e o unico desfecho seguro.
            if (JS_IsUndefined(owner)) return makeStructCopy(ctx, d.cls, b, d.size);
            return makeStructView(ctx, d.cls, b, owner, d.size);
        case Prim::Object: {
            auto* o = *reinterpret_cast<Il2CppObject**>(b);
            if (!o) return JS_NULL;
            // Declarado object/System.Array, mas e um T[]: indexavel no JS.
            if (isArrayObject(o)) return makeGameArray(ctx, reinterpret_cast<Il2CppArray*>(o));
            return makeNativeObject(ctx, o);
        }
    }
    return JS_UNDEFINED;
}

int writeAt(JSContext* ctx, void* p, const TypeDesc& d, JSValueConst v) {
    auto& a = il2cpp::api();
    auto* b = static_cast<char*>(p);
    switch (d.prim) {
        case Prim::Void: return true;
        case Prim::Bool: *reinterpret_cast<uint8_t*>(b) = JS_ToBool(ctx, v) ? 1 : 0; return true;
        case Prim::F32:
        case Prim::F64: {
            double x = 0;
            if (toNumber(ctx, v, d, &x) < 0) return -1;
            if (d.prim == Prim::F32) *reinterpret_cast<float*>(b) = static_cast<float>(x);
            else *reinterpret_cast<double*>(b) = x;
            return true;
        }
        case Prim::String: {
            if (JS_IsNull(v) || JS_IsUndefined(v)) {
                *reinterpret_cast<void**>(b) = nullptr;
                return true;
            }
            const char* cs = JS_ToCString(ctx, v);
            if (!cs) return -1;
            *reinterpret_cast<Il2CppString**>(b) = a.string_new(cs);
            JS_FreeCString(ctx, cs);
            return true;
        }
        case Prim::Array: {
            Il2CppArray* arr = arrayFromJS(v);
            if (!arr && JS_IsArray(v)) {
                arr = listFromJS(ctx, v, d);
                if (!arr) return -1;
            }
            // Antes qualquer coisa que nao fosse array virava null, calada, e o
            // metodo do jogo recebia null no lugar do que o mod mandou.
            if (!arr && !JS_IsNull(v) && !JS_IsUndefined(v)) {
                JS_ThrowTypeError(ctx, "esperava um %s, recebeu %s", d.name.c_str(), JS_IsString(v) ? "um texto" : kindOf(ctx, v));
                return -1;
            }
            *reinterpret_cast<Il2CppArray**>(b) = arr;
            return true;
        }
        case Prim::Object: {
            Il2CppObject* o = objectFromJS(v);
            // Array tambem e objeto (parametro `object` ou `System.Array`).
            if (!o) o = reinterpret_cast<Il2CppObject*>(arrayFromJS(v));
            if (!o && !JS_IsNull(v) && !JS_IsUndefined(v)) {
                JS_ThrowTypeError(ctx, "esperava %s (objeto do jogo), recebeu %s",
                                  d.name.c_str(), JS_IsString(v) ? "um texto" : kindOf(ctx, v));
                return -1;
            }
            // Sem esta conferencia, `player.someItem = algumNPC` gravava o
            // ponteiro e so quebrava depois, no jogo, longe da linha culpada.
            // O IL2CPP sabe responder; era so perguntar.
            if (o && d.cls && a.class_is_assignable_from && a.object_get_class) {
                Il2CppClass* src = a.object_get_class(o);
                if (src && !a.class_is_assignable_from(d.cls, src)) {
                    JS_ThrowTypeError(ctx, "esperava %s, recebeu %s", d.name.c_str(),
                                      a.class_get_name(src));
                    return -1;
                }
            }
            *reinterpret_cast<Il2CppObject**>(b) = o;
            return true;
        }
        case Prim::Struct: {
            if (d.nullable()) return writeNullable(ctx, b, d, v);
            Il2CppClass* src = nullptr;
            size_t srcSize = 0;
            void* data = structDataOf(v, &src, &srcSize);
            if (!data) {
                JS_ThrowTypeError(ctx, "esperava um %s (struct do jogo)", d.name.c_str());
                return -1;
            }
            if (src && d.cls && src != d.cls) {
                const char* sn = a.class_get_name(src);
                JS_ThrowTypeError(ctx, "esperava %s, recebeu %s", d.name.c_str(), sn ? sn : "?");
                return -1;
            }
            std::memcpy(b, data, d.size < srcSize ? d.size : srcSize);
            return true;
        }
        default: break;
    }
    double n = 0;
    if (toNumber(ctx, v, d, &n) < 0) return -1;
    double lo = 0, hi = 0;
    if (!fits(d.prim, n, &lo, &hi)) {
        JS_ThrowRangeError(ctx, "%g nao cabe em %s (de %g a %g)", n, d.name.c_str(), lo, hi);
        return -1;
    }
    int64_t x = static_cast<int64_t>(n);
    std::memcpy(b, &x, d.size);  // little-endian: os bytes baixos sao os certos
    return true;
}

// ============================== GameStruct ==============================

JSValue makeStructView(JSContext* ctx, Il2CppClass* cls, void* data, JSValueConst owner,
                       size_t size) {
    // O TypeDesc de quem chama ja tem o tamanho; so perguntamos ao IL2CPP
    // quando ninguem sabe. Era uma chamada ao runtime por leitura de campo.
    size_t n = size;
    if (!n) {
        uint32_t align = 0;
        auto& a = il2cpp::api();
        n = a.class_value_size ? static_cast<size_t>(a.class_value_size(cls, &align)) : 0;
    }
    JSValue v;
    StructRef* r = newRef(ctx, &v, cls, n);
    if (!r) return v;
    r->data = data;
    r->owner = JS_DupValue(ctx, owner);
    return v;
}

JSValue makeStructCopy(JSContext* ctx, Il2CppClass* cls, const void* src, size_t n) {
    JSValue v;
    StructRef* r = newRef(ctx, &v, cls, n);
    if (!r) return v;
    r->owned = std::calloc(1, n ? n : 1);
    if (!r->owned) { JS_FreeValue(ctx, v); return JS_ThrowOutOfMemory(ctx); }
    if (src) std::memcpy(r->owned, src, n);
    r->data = r->owned;
    return v;
}

JSValue makeStaticStruct(JSContext* ctx, Il2CppClass* cls, FieldInfo* f, size_t n) {
    JSValue v = makeStructCopy(ctx, cls, nullptr, n);
    if (JS_IsException(v)) return v;
    StructRef* r = refOf(v);
    r->staticField = f;
    il2cpp::api().field_static_get_value(f, r->data);
    return v;
}

void* structDataOf(JSValueConst v, Il2CppClass** outCls, size_t* outSize) {
    if (auto* r = refOf(v)) {
        if (outCls) *outCls = r->cls;
        if (outSize) *outSize = r->size;
        return r->data;
    }
    // Objeto encaixotado (o que Vector2.new() devolve): os dados vem logo
    // depois do cabecalho.
    if (Il2CppObject* o = objectFromJS(v)) {
        auto& a = il2cpp::api();
        Il2CppClass* c = a.object_get_class(o);
        if (c && a.class_is_valuetype && a.class_is_valuetype(c)) {
            if (outCls) *outCls = c;
            if (outSize) {
                uint32_t align = 0;
                *outSize = a.class_value_size
                               ? static_cast<size_t>(a.class_value_size(c, &align)) : 0;
            }
            return reinterpret_cast<char*>(o) + sizeof(Il2CppObject);
        }
    }
    return nullptr;
}

void installStructClass(JSContext* ctx) {
    JSRuntime* rt = JS_GetRuntime(ctx);
    JS_NewClassID(rt, &g_structId);
    static const JSClassDef def = {
        "GameStruct", gs_finalizer, nullptr, nullptr,
        const_cast<JSClassExoticMethods*>(&gs_exotic),
    };
    JS_NewClass(rt, g_structId, &def);
    JSValue proto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, proto, gs_proto, sizeof(gs_proto) / sizeof(gs_proto[0]));
    JS_SetClassProto(ctx, g_structId, proto);
}

} // namespace bl::script
#endif
