#include "script/Value.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "script/Bridge.h"
#include "script/Marshal.h"

#include <cstdlib>
#include <cstring>
#include <unordered_map>

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
};

StructRef* refOf(JSValueConst v) {
    return static_cast<StructRef*>(JS_GetOpaque(v, g_structId));
}

StructRef* newRef(JSContext* ctx, JSValue* out, Il2CppClass* cls, size_t size) {
    JSValue v = JS_NewObjectClass(ctx, g_structId);
    if (JS_IsException(v)) { *out = v; return nullptr; }
    auto* r = new StructRef();
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
    delete r;
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
            TypeDesc d = describe(a.field_get_type(f));
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

JSValue gs_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    JSValue proto = JS_GetClassProto(ctx, g_structId);
    JSValue fromProto = JS_GetProperty(ctx, proto, atom);
    JS_FreeValue(ctx, proto);
    if (!JS_IsUndefined(fromProto)) return fromProto;
    JS_FreeValue(ctx, fromProto);

    auto* r = refOf(obj);
    const char* key = JS_AtomToCString(ctx, atom);
    if (!r || !key) { if (key) JS_FreeCString(ctx, key); return JS_UNDEFINED; }
    std::string name(key);
    JS_FreeCString(ctx, key);

    auto& a = il2cpp::api();
    if (name.find('(') != std::string::npos) {
        il2cpp::Signature sig = il2cpp::parseSignature(name);
        bool amb = false;
        if (const MethodInfo* m = il2cpp::findMethodBySignature(r->cls, sig, &amb)) {
            return makeGameMethod(ctx, m);
        }
        return JS_ThrowTypeError(ctx, "metodo nao encontrado em %s: %s",
                                 a.class_get_name(r->cls), name.c_str());
    }
    if (FieldInfo* f = il2cpp::findField(r->cls, name)) {
        return readAt(ctx,
                      static_cast<char*>(r->data) + structFieldOffset(r->cls, a.field_get_offset(f)),
                      describe(a.field_get_type(f)), obj);
    }
    // Propriedade C# do struct.
    if (const MethodInfo* g = a.class_get_method_from_name(r->cls, ("get_" + name).c_str(), 0)) {
        Il2CppObject* exc = nullptr;
        Il2CppObject* ret = a.runtime_invoke(g, r->data, nullptr, &exc);
        if (exc) return JS_ThrowInternalError(ctx, "get_%s lancou excecao no jogo", name.c_str());
        return fromReturn(ctx, g, ret);
    }
    return JS_UNDEFINED;
}

int gs_exotic_set(JSContext* ctx, JSValueConst obj, JSAtom atom,
                  JSValueConst value, JSValueConst, int) {
    auto* r = refOf(obj);
    const char* key = JS_AtomToCString(ctx, atom);
    if (!r || !key) { if (key) JS_FreeCString(ctx, key); return -1; }
    std::string name(key);
    JS_FreeCString(ctx, key);

    auto& a = il2cpp::api();
    if (FieldInfo* f = il2cpp::findField(r->cls, name)) {
        int ok = writeAt(ctx,
                         static_cast<char*>(r->data) + structFieldOffset(r->cls, a.field_get_offset(f)),
                         describe(a.field_get_type(f)), value);
        if (ok > 0) flush(r);
        return ok;
    }
    if (const MethodInfo* sm = a.class_get_method_from_name(r->cls, ("set_" + name).c_str(), 1)) {
        ArgPack pack;
        if (!pack.build(ctx, sm, 1, &value)) return -1;
        Il2CppObject* exc = nullptr;
        a.runtime_invoke(sm, r->data, pack.data(), &exc);
        if (exc) {
            JS_ThrowInternalError(ctx, "set_%s lancou excecao no jogo", name.c_str());
            return -1;
        }
        flush(r);
        return true;
    }
    JS_ThrowTypeError(ctx, "%s nao tem o campo %s", a.class_get_name(r->cls), name.c_str());
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
            TypeDesc d = describe(a.field_get_type(f));
            if (a.field_get_offset(f) + d.size > valueSize) {
                adjust = sizeof(Il2CppObject);
                break;
            }
        }
    }
    const char* n = a.class_get_name(cls);
    BL_INFO("struct %s: valor=%zu bytes, offsets %s cabecalho",
            n ? n : "?", valueSize, adjust ? "COM" : "sem");
    cache[cls] = adjust;
    return adjust;
}

size_t structFieldOffset(Il2CppClass* cls, size_t fieldOffset) {
    size_t adjust = headerAdjustOf(cls);
    return fieldOffset >= adjust ? fieldOffset - adjust : fieldOffset;
}

TypeDesc describe(const Il2CppType* t) {
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
        return d;
    }
    d.prim = Prim::Object;
    d.size = sizeof(void*);
    return d;
}

int hfaOf(Il2CppClass* cls, bool* isDouble) {
    auto& a = il2cpp::api();
    if (!cls || !a.class_get_fields) return 0;
    int count = 0;
    bool sawDouble = false, sawFloat = false;
    void* iter = nullptr;
    while (FieldInfo* f = a.class_get_fields(cls, &iter)) {
        if (a.field_get_flags && (a.field_get_flags(f) & 0x0010)) continue;  // estatico
        TypeDesc d = describe(a.field_get_type(f));
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
            // Sem dono conhecido a vista ficaria pendurada quando o buffer
            // sumisse; copia e o unico desfecho seguro.
            if (JS_IsUndefined(owner)) return makeStructCopy(ctx, d.cls, b, d.size);
            return makeStructView(ctx, d.cls, b, owner);
        case Prim::Object: {
            auto* o = *reinterpret_cast<Il2CppObject**>(b);
            return o ? makeNativeObject(ctx, o) : JS_NULL;
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
            if (JS_ToFloat64(ctx, &x, v) < 0) return -1;
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
        case Prim::Array:
            *reinterpret_cast<Il2CppArray**>(b) = arrayFromJS(v);
            return true;
        case Prim::Object:
            *reinterpret_cast<Il2CppObject**>(b) = objectFromJS(v);
            return true;
        case Prim::Struct: {
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
    int64_t x = 0;
    if (JS_ToInt64(ctx, &x, v) < 0) return -1;
    std::memcpy(b, &x, d.size);  // little-endian: os bytes baixos sao os certos
    return true;
}

// ============================== GameStruct ==============================

JSValue makeStructView(JSContext* ctx, Il2CppClass* cls, void* data, JSValueConst owner) {
    uint32_t align = 0;
    auto& a = il2cpp::api();
    size_t n = a.class_value_size ? static_cast<size_t>(a.class_value_size(cls, &align)) : 0;
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
