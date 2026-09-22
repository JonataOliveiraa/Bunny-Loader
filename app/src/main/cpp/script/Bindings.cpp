#include "script/ScriptEngine.h"
#include "script/Bridge.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "script/Marshal.h"
#include "il2cpp/Types.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include <cstdint>
#include <cstring>
#include <set>
#include <string>
#include <cstdlib>
#endif

// Ponte JavaScript <-> IL2CPP. Espelha a API do TL Pro. Estado atual:
//   [ok] bl.log
//   [ok] NativeClass('Ns','Class'); .getStaticInt/Float, .setStaticInt/Float
//   [ok] NativeClass.new() -> NativeObject (il2cpp_object_new)
//   [ok] NativeObject.getInt/setInt/getFloat/setFloat (campo de instancia)
//   [ ] NativeMethod (.call via runtime_invoke), .hook()
//
// Limitacao atual: get/setInt escrevem 4 bytes (int32). Campos short/byte/long
// serao tratados por tipo quando a API amadurecer. Suficiente para os campos
// int/float que os mods mais tocam (type, useTime, damage, velocity.X...).

namespace bl::script {

#if BL_HAVE_QUICKJS

namespace {

JSClassID g_nativeClassId;
JSClassID g_nativeObjectId;
JSClassID g_nativeMethodId;

// NativeMethod guarda isto (heap; liberado no finalizador).
struct MethodRef {
    const MethodInfo* method;
    int paramCount;
    bool isInstance;
};

// Embrulha um MethodInfo num NativeMethod para o JS.
JSValue makeMethod(JSContext* ctx, const MethodInfo* m) {
    // CUIDADO: il2cpp_method_get_flags DEVOLVE as flags e escreve as de
    // IMPLEMENTACAO no parametro de saida. Ler o parametro (o que este codigo
    // fazia) dava isInstance errado para todo metodo estatico.
    uint32_t iflags = 0;
    uint32_t flags = il2cpp::api().method_get_flags(m, &iflags);
    auto* ref = static_cast<MethodRef*>(std::malloc(sizeof(MethodRef)));
    if (!ref) return JS_ThrowOutOfMemory(ctx);
    ref->method = m;
    ref->paramCount = static_cast<int>(il2cpp::api().method_get_param_count(m));
    ref->isInstance = !(flags & 0x0010);  // METHOD_ATTRIBUTE_STATIC
    JSValue v = JS_NewObjectClass(ctx, g_nativeMethodId);
    if (JS_IsException(v)) std::free(ref);
    else JS_SetOpaque(v, ref);
    return v;
}

// Declaradas adiante: o exotic da classe e o leitor de campo precisam delas
// antes de as secoes GameArray/campo existirem no arquivo.
JSValue makeGameArray(JSContext* ctx, Il2CppArray* arr);
JSValue readStaticField(JSContext* ctx, FieldInfo* f);

/**
 * Propriedade C# é açúcar sobre métodos: `Main.myPlayer` é `get_myPlayer()`.
 * Sem isto, metade do que o modder lê no dump como campo não existe no JS —
 * `Main.myPlayer` voltava undefined e o índice de array virava lixo.
 */
const MethodInfo* accessor(Il2CppClass* cls, const std::string& prefix,
                           const std::string& name, int params) {
    if (!cls) return nullptr;
    return il2cpp::api().class_get_method_from_name(cls, (prefix + name).c_str(), params);
}

Il2CppClass* classOf(JSValueConst v) {
    return static_cast<Il2CppClass*>(JS_GetOpaque(v, g_nativeClassId));
}
Il2CppObject* objOf(JSValueConst v) {
    return static_cast<Il2CppObject*>(JS_GetOpaque(v, g_nativeObjectId));
}

// --- bl.log ---
JSValue js_bl_log(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    for (int i = 0; i < argc; ++i) {
        const char* text = JS_ToCString(ctx, argv[i]);
        BL_INFO("[mod] %s", text ? text : "undefined");
        if (text) JS_FreeCString(ctx, text);
    }
    return JS_UNDEFINED;
}

// ============================ NativeClass ============================

// Campo estatico -> FieldInfo (sobe a hierarquia).
FieldInfo* staticField(JSContext* ctx, JSValueConst self, JSValueConst nameArg,
                       const char** outName) {
    Il2CppClass* cls = classOf(self);
    *outName = JS_ToCString(ctx, nameArg);
    if (!cls || !*outName) return nullptr;
    return il2cpp::findField(cls, *outName);
}

JSValue nc_getStaticInt(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    if (argc < 1) return JS_EXCEPTION;
    const char* name = nullptr;
    FieldInfo* f = staticField(ctx, self, argv[0], &name);
    JSValue r;
    if (!f) r = JS_ThrowTypeError(ctx, "campo estatico '%s' nao encontrado", name ? name : "?");
    else {
        uint8_t buf[16] = {0};
        il2cpp::api().field_static_get_value(f, buf);
        int64_t v; std::memcpy(&v, buf, sizeof(v));
        r = JS_NewInt64(ctx, v);
    }
    if (name) JS_FreeCString(ctx, name);
    return r;
}

JSValue nc_getStaticFloat(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    if (argc < 1) return JS_EXCEPTION;
    const char* name = nullptr;
    FieldInfo* f = staticField(ctx, self, argv[0], &name);
    JSValue r;
    if (!f) r = JS_ThrowTypeError(ctx, "campo estatico '%s' nao encontrado", name ? name : "?");
    else {
        uint8_t buf[16] = {0};
        il2cpp::api().field_static_get_value(f, buf);
        float v; std::memcpy(&v, buf, sizeof(v));
        r = JS_NewFloat64(ctx, static_cast<double>(v));
    }
    if (name) JS_FreeCString(ctx, name);
    return r;
}

JSValue nc_setStaticInt(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_EXCEPTION;
    const char* name = nullptr;
    FieldInfo* f = staticField(ctx, self, argv[0], &name);
    JSValue r = JS_UNDEFINED;
    if (!f) r = JS_ThrowTypeError(ctx, "campo estatico '%s' nao encontrado", name ? name : "?");
    else {
        int32_t v = 0; JS_ToInt32(ctx, &v, argv[1]);
        il2cpp::api().field_static_set_value(f, &v);
    }
    if (name) JS_FreeCString(ctx, name);
    return r;
}

JSValue nc_setStaticFloat(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_EXCEPTION;
    const char* name = nullptr;
    FieldInfo* f = staticField(ctx, self, argv[0], &name);
    JSValue r = JS_UNDEFINED;
    if (!f) r = JS_ThrowTypeError(ctx, "campo estatico '%s' nao encontrado", name ? name : "?");
    else {
        double d = 0; JS_ToFloat64(ctx, &d, argv[1]);
        float v = static_cast<float>(d);
        il2cpp::api().field_static_set_value(f, &v);
    }
    if (name) JS_FreeCString(ctx, name);
    return r;
}

JSValue nc_new(JSContext* ctx, JSValueConst self, int, JSValueConst*);
JSValue nc_method(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv);

JSValue js_NativeClass(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_ThrowTypeError(ctx, "NativeClass(namespace, nome)");
    const char* ns = JS_ToCString(ctx, argv[0]);
    const char* name = JS_ToCString(ctx, argv[1]);
    JSValue r;
    if (!ns || !name) r = JS_EXCEPTION;
    else {
        Il2CppClass* cls = il2cpp::findClass({ns, name, {}});
        if (!cls) r = JS_ThrowTypeError(ctx, "classe '%s.%s' nao encontrada", ns, name);
        else {
            r = JS_NewObjectClass(ctx, g_nativeClassId);
            if (!JS_IsException(r)) JS_SetOpaque(r, cls);
        }
    }
    if (ns) JS_FreeCString(ctx, ns);
    if (name) JS_FreeCString(ctx, name);
    return r;
}

/**
 * Acesso por propriedade e por ASSINATURA no NativeClass.
 *
 *   Item['void SetDefaults(int Type, ItemVariant variant)']  -> NativeMethod
 *   Item.SetDefaults                                          -> NativeMethod (se único)
 *   Player.defaultItemGrabRange                               -> valor do campo estático
 *
 * Substitui o `.method(nome, nParams)`, que casava por ARIDADE e por isso não
 * desambiguava overloads — o `Item.NewItem` tem quatro com 9 parâmetros.
 * Quando o nome puro é ambíguo NÃO escolhemos um: erramos, listando os
 * overloads. Escolher em silêncio foi o bug que deu exceção a cada frame.
 */
JSValue nc_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    // 1. O que é nosso (method, new, getStaticInt, toString...) vem do
    //    protótipo. Sem isto o exotic engoliria a API inteira.
    JSValue proto = JS_GetClassProto(ctx, g_nativeClassId);
    JSValue fromProto = JS_GetProperty(ctx, proto, atom);
    JS_FreeValue(ctx, proto);
    if (!JS_IsUndefined(fromProto)) return fromProto;
    JS_FreeValue(ctx, fromProto);

    Il2CppClass* cls = classOf(obj);
    const char* key = JS_AtomToCString(ctx, atom);
    if (!cls || !key) {
        if (key) JS_FreeCString(ctx, key);
        return JS_UNDEFINED;
    }
    std::string name(key);
    JS_FreeCString(ctx, key);

    // 2. Tem parêntese? É assinatura.
    if (name.find('(') != std::string::npos) {
        il2cpp::Signature sig = il2cpp::parseSignature(name);
        if (!sig.valid) return JS_ThrowTypeError(ctx, "assinatura invalida: '%s'", name.c_str());
        bool ambiguous = false;
        const MethodInfo* m = il2cpp::findMethodBySignature(cls, sig, &ambiguous);
        if (m) return makeMethod(ctx, m);

        std::string msg = ambiguous ? "assinatura ambigua: '" : "metodo nao encontrado: '";
        msg += name + "'";
        auto overloads = il2cpp::listOverloads(cls, sig.name);
        if (!overloads.empty()) {
            msg += ". Existem: ";
            for (size_t i = 0; i < overloads.size(); ++i) {
                if (i) msg += " | ";
                msg += overloads[i];
            }
        }
        return JS_ThrowTypeError(ctx, "%s", msg.c_str());
    }

    // 3. Campo estático, pelo tipo declarado (array vira GameArray, float vira
    //    número, string vira string — antes tudo saía como int64).
    if (FieldInfo* f = il2cpp::findField(cls, name)) return readStaticField(ctx, f);

    // 3b. Propriedade estática: get_<nome>().
    if (const MethodInfo* g = accessor(cls, "get_", name, 0)) {
        Il2CppObject* exc = nullptr;
        Il2CppObject* r = il2cpp::api().runtime_invoke(g, nullptr, nullptr, &exc);
        if (exc) return JS_ThrowInternalError(ctx, "get_%s lancou excecao no jogo", name.c_str());
        return fromReturn(ctx, g, r);
    }

    // 4. Método pelo nome puro — só se houver UM.
    auto overloads = il2cpp::listOverloads(cls, name);
    if (overloads.size() == 1) {
        il2cpp::Signature bare;
        bare.name = name;
        bare.valid = true;
        // Um overload só: casa por nome + aridade dele mesmo.
        auto& a = il2cpp::api();
        for (Il2CppClass* c = cls; c; c = a.class_get_parent(c)) {
            void* iter = nullptr;
            while (const MethodInfo* m = a.class_get_methods(c, &iter)) {
                if (name == a.method_get_name(m)) return makeMethod(ctx, m);
            }
        }
    }
    if (overloads.size() > 1) {
        std::string msg = "'" + name + "' tem " + std::to_string(overloads.size()) +
            " overloads; use a assinatura. Existem: ";
        for (size_t i = 0; i < overloads.size(); ++i) {
            if (i) msg += " | ";
            msg += overloads[i];
        }
        return JS_ThrowTypeError(ctx, "%s", msg.c_str());
    }
    return JS_UNDEFINED;
}

int nc_exotic_set(JSContext* ctx, JSValueConst obj, JSAtom atom,
                  JSValueConst value, JSValueConst, int) {
    Il2CppClass* cls = classOf(obj);
    const char* key = JS_AtomToCString(ctx, atom);
    if (!cls || !key) { if (key) JS_FreeCString(ctx, key); return -1; }
    FieldInfo* f = il2cpp::findField(cls, key);
    JS_FreeCString(ctx, key);
    if (!f) {
        // Não é campo do jogo: vira propriedade JS comum no objeto.
        return JS_DefinePropertyValue(ctx, obj, atom, JS_DupValue(ctx, value),
                                      JS_PROP_C_W_E) < 0 ? -1 : true;
    }
    int64_t v = 0;
    if (JS_ToInt64(ctx, &v, value) < 0) return -1;
    il2cpp::api().field_static_set_value(f, &v);
    return true;
}

const JSClassExoticMethods nc_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr,
    nc_exotic_get, nc_exotic_set,
};

const JSCFunctionListEntry nc_proto[] = {
    JS_CFUNC_DEF("getStaticInt", 1, nc_getStaticInt),
    JS_CFUNC_DEF("getStaticFloat", 1, nc_getStaticFloat),
    JS_CFUNC_DEF("setStaticInt", 2, nc_setStaticInt),
    JS_CFUNC_DEF("setStaticFloat", 2, nc_setStaticFloat),
    JS_CFUNC_DEF("new", 0, nc_new),
    JS_CFUNC_DEF("method", 2, nc_method),
};

// Le/escreve um campo pelo TIPO dele. Ler um float como int devolve lixo, e o
// acesso por propriedade (obj.useTime) nao tem como o modder dizer qual e.
// Nucleo: interpreta `p` conforme o tipo `ft`. Serve para campo de instancia
// (p = objeto + offset) e para estatico (p = buffer preenchido por
// field_static_get_value) — o mesmo tipo, duas origens de memoria.
JSValue valueAt(JSContext* ctx, char* p, const Il2CppType* ft) {
    auto& a = il2cpp::api();
    char* tn = a.type_get_name(ft);
    std::string t(tn ? tn : "");
    if (tn) a.il2cpp_free(tn);

    if (t == "System.Single")  return JS_NewFloat64(ctx, *reinterpret_cast<float*>(p));
    if (t == "System.Double")  return JS_NewFloat64(ctx, *reinterpret_cast<double*>(p));
    if (t == "System.Boolean") return JS_NewBool(ctx, *reinterpret_cast<uint8_t*>(p) != 0);
    if (t == "System.Byte")    return JS_NewInt32(ctx, *reinterpret_cast<uint8_t*>(p));
    if (t == "System.SByte")   return JS_NewInt32(ctx, *reinterpret_cast<int8_t*>(p));
    if (t == "System.Int16")   return JS_NewInt32(ctx, *reinterpret_cast<int16_t*>(p));
    if (t == "System.UInt16")  return JS_NewInt32(ctx, *reinterpret_cast<uint16_t*>(p));
    if (t == "System.Int64")   return JS_NewInt64(ctx, *reinterpret_cast<int64_t*>(p));
    if (t == "System.UInt64")  return JS_NewInt64(ctx, static_cast<int64_t>(*reinterpret_cast<uint64_t*>(p)));
    if (t == "System.Int32" || t == "System.UInt32") {
        return JS_NewInt32(ctx, *reinterpret_cast<int32_t*>(p));
    }
    // Array: entrega um GameArray em vez de um objeto opaco.
    if (t.size() > 2 && t.compare(t.size() - 2, 2, "[]") == 0) {
        auto* arr = *reinterpret_cast<Il2CppArray**>(p);
        return arr ? makeGameArray(ctx, arr) : JS_NULL;
    }
    if (t == "System.String") {
        auto* str = *reinterpret_cast<Il2CppString**>(p);
        return str ? JS_NewString(ctx, stringToUtf8(str).c_str()) : JS_NULL;
    }

    // Struct por valor (Vector2, Color, Rectangle) fica GUARDADO EM LINHA —
    // nao ha ponteiro ali. Ler os primeiros 8 bytes como ponteiro devolveria um
    // GameObject apontando para dois floats, e o proximo acesso leria memoria
    // arbitraria. Recusa em vez de fabricar.
    if (a.class_from_il2cpp_type && a.class_is_valuetype) {
        if (Il2CppClass* fc = a.class_from_il2cpp_type(ft)) {
            if (a.class_is_valuetype(fc)) {
                return JS_ThrowTypeError(
                    ctx, "campo do tipo %s e struct por valor — ainda nao suportado",
                    t.c_str());
            }
        }
    }
    auto* ref = *reinterpret_cast<Il2CppObject**>(p);
    if (!ref) return JS_NULL;
    return makeNativeObject(ctx, ref);
}

JSValue readField(JSContext* ctx, void* base, FieldInfo* f) {
    auto& a = il2cpp::api();
    return valueAt(ctx, reinterpret_cast<char*>(base) + a.field_get_offset(f),
                   a.field_get_type(f));
}

/** Estatico: o valor vem por copia, nao por offset num objeto. */
JSValue readStaticField(JSContext* ctx, FieldInfo* f) {
    auto& a = il2cpp::api();
    uint8_t buf[32] = {0};
    a.field_static_get_value(f, buf);
    return valueAt(ctx, reinterpret_cast<char*>(buf), a.field_get_type(f));
}

int writeField(JSContext* ctx, void* base, FieldInfo* f, JSValueConst value) {
    auto& a = il2cpp::api();
    char* tn = a.type_get_name(a.field_get_type(f));
    std::string t(tn ? tn : "");
    if (tn) a.il2cpp_free(tn);
    char* p = reinterpret_cast<char*>(base) + a.field_get_offset(f);

    if (t == "System.Single" || t == "System.Double") {
        double d = 0;
        if (JS_ToFloat64(ctx, &d, value) < 0) return -1;
        if (t == "System.Single") *reinterpret_cast<float*>(p) = static_cast<float>(d);
        else *reinterpret_cast<double*>(p) = d;
        return true;
    }
    if (t == "System.Boolean") {
        *reinterpret_cast<uint8_t*>(p) = JS_ToBool(ctx, value) ? 1 : 0;
        return true;
    }
    int64_t v = 0;
    if (JS_ToInt64(ctx, &v, value) < 0) return -1;
    if (t == "System.Byte" || t == "System.SByte") *reinterpret_cast<uint8_t*>(p) = static_cast<uint8_t>(v);
    else if (t == "System.Int16" || t == "System.UInt16") *reinterpret_cast<int16_t*>(p) = static_cast<int16_t>(v);
    else if (t == "System.Int64" || t == "System.UInt64") *reinterpret_cast<int64_t*>(p) = v;
    else *reinterpret_cast<int32_t*>(p) = static_cast<int32_t>(v);
    return true;
}

// ============================ NativeObject ============================

// Campo de instancia -> offset (a partir do inicio do objeto, ja com header).
int32_t instanceOffset(JSContext* ctx, JSValueConst self, JSValueConst nameArg,
                       Il2CppObject** outObj, const char** outName) {
    *outObj = objOf(self);
    *outName = JS_ToCString(ctx, nameArg);
    if (!*outObj || !*outName) return -1;
    Il2CppClass* cls = il2cpp::api().object_get_class(*outObj);
    FieldInfo* f = il2cpp::findField(cls, *outName);
    if (!f) return -1;
    return static_cast<int32_t>(il2cpp::api().field_get_offset(f));
}

JSValue no_getInt(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    if (argc < 1) return JS_EXCEPTION;
    Il2CppObject* obj; const char* name;
    int32_t off = instanceOffset(ctx, self, argv[0], &obj, &name);
    JSValue r;
    if (off < 0) r = JS_ThrowTypeError(ctx, "campo '%s' nao encontrado", name ? name : "?");
    else {
        int32_t v = *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(obj) + off);
        r = JS_NewInt32(ctx, v);
    }
    if (name) JS_FreeCString(ctx, name);
    return r;
}

JSValue no_setInt(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_EXCEPTION;
    Il2CppObject* obj; const char* name;
    int32_t off = instanceOffset(ctx, self, argv[0], &obj, &name);
    JSValue r = JS_UNDEFINED;
    if (off < 0) r = JS_ThrowTypeError(ctx, "campo '%s' nao encontrado", name ? name : "?");
    else {
        int32_t v = 0; JS_ToInt32(ctx, &v, argv[1]);
        *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(obj) + off) = v;
    }
    if (name) JS_FreeCString(ctx, name);
    return r;
}

JSValue no_getFloat(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    if (argc < 1) return JS_EXCEPTION;
    Il2CppObject* obj; const char* name;
    int32_t off = instanceOffset(ctx, self, argv[0], &obj, &name);
    JSValue r;
    if (off < 0) r = JS_ThrowTypeError(ctx, "campo '%s' nao encontrado", name ? name : "?");
    else {
        float v = *reinterpret_cast<float*>(reinterpret_cast<char*>(obj) + off);
        r = JS_NewFloat64(ctx, static_cast<double>(v));
    }
    if (name) JS_FreeCString(ctx, name);
    return r;
}

JSValue no_setFloat(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_EXCEPTION;
    Il2CppObject* obj; const char* name;
    int32_t off = instanceOffset(ctx, self, argv[0], &obj, &name);
    JSValue r = JS_UNDEFINED;
    if (off < 0) r = JS_ThrowTypeError(ctx, "campo '%s' nao encontrado", name ? name : "?");
    else {
        double d = 0; JS_ToFloat64(ctx, &d, argv[1]);
        float v = static_cast<float>(d);
        *reinterpret_cast<float*>(reinterpret_cast<char*>(obj) + off) = v;
    }
    if (name) JS_FreeCString(ctx, name);
    return r;
}

/**
 * Campos de instância como propriedade: `item.useTime`, `item.useTime = 4`.
 *
 * O tipo do campo decide como ler/escrever (ver readField/writeField) — o
 * modder não tem como informar isso numa atribuição, e ler float como int
 * devolve lixo.
 */
JSValue no_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    JSValue proto = JS_GetClassProto(ctx, g_nativeObjectId);
    JSValue fromProto = JS_GetProperty(ctx, proto, atom);
    JS_FreeValue(ctx, proto);
    if (!JS_IsUndefined(fromProto)) return fromProto;
    JS_FreeValue(ctx, fromProto);

    Il2CppObject* o = objOf(obj);
    const char* key = JS_AtomToCString(ctx, atom);
    if (!o || !key) { if (key) JS_FreeCString(ctx, key); return JS_UNDEFINED; }
    std::string name(key);
    JS_FreeCString(ctx, key);

    Il2CppClass* cls = il2cpp::api().object_get_class(o);
    if (name.find('(') != std::string::npos) {
        il2cpp::Signature sig = il2cpp::parseSignature(name);
        bool amb = false;
        if (const MethodInfo* m = il2cpp::findMethodBySignature(cls, sig, &amb)) {
            return makeMethod(ctx, m);
        }
        return JS_ThrowTypeError(ctx, "metodo nao encontrado: '%s'", name.c_str());
    }
    if (FieldInfo* f = il2cpp::findField(cls, name)) return readField(ctx, o, f);

    if (const MethodInfo* g = accessor(cls, "get_", name, 0)) {
        Il2CppObject* exc = nullptr;
        Il2CppObject* r = il2cpp::api().runtime_invoke(g, o, nullptr, &exc);
        if (exc) return JS_ThrowInternalError(ctx, "get_%s lancou excecao no jogo", name.c_str());
        return fromReturn(ctx, g, r);
    }
    return JS_UNDEFINED;
}

int no_exotic_set(JSContext* ctx, JSValueConst obj, JSAtom atom,
                  JSValueConst value, JSValueConst, int) {
    Il2CppObject* o = objOf(obj);
    const char* key = JS_AtomToCString(ctx, atom);
    if (!o || !key) { if (key) JS_FreeCString(ctx, key); return -1; }
    Il2CppClass* ocls = il2cpp::api().object_get_class(o);
    FieldInfo* f = il2cpp::findField(ocls, key);
    if (!f) {
        if (const MethodInfo* sm = accessor(ocls, "set_", key, 1)) {
            JS_FreeCString(ctx, key);
            ArgPack pack;
            if (!pack.build(ctx, sm, 1, &value)) return -1;
            Il2CppObject* exc = nullptr;
            il2cpp::api().runtime_invoke(sm, o, pack.data(), &exc);
            if (exc) { JS_ThrowInternalError(ctx, "setter lancou excecao no jogo"); return -1; }
            return true;
        }
    }
    JS_FreeCString(ctx, key);
    if (!f) {
        return JS_DefinePropertyValue(ctx, obj, atom, JS_DupValue(ctx, value),
                                      JS_PROP_C_W_E) < 0 ? -1 : true;
    }
    return writeField(ctx, o, f, value);
}

const JSClassExoticMethods no_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr,
    no_exotic_get, no_exotic_set,
};

const JSCFunctionListEntry no_proto[] = {
    JS_CFUNC_DEF("getInt", 1, no_getInt),
    JS_CFUNC_DEF("setInt", 2, no_setInt),
    JS_CFUNC_DEF("getFloat", 1, no_getFloat),
    JS_CFUNC_DEF("setFloat", 2, no_setFloat),
};

// NativeClass.new(): cria uma instancia sem chamar o construtor (memoria
// zerada). Para inicializar de verdade, o mod deve chamar .ctor() depois
// (quando NativeMethod existir).
JSValue nc_new(JSContext* ctx, JSValueConst self, int, JSValueConst*) {
    Il2CppClass* cls = classOf(self);
    if (!cls) return JS_EXCEPTION;
    Il2CppObject* obj = il2cpp::api().object_new(cls);
    if (!obj) return JS_ThrowInternalError(ctx, "object_new falhou");
    return makeNativeObject(ctx, obj);
}

// ============================ NativeMethod ============================

void nm_finalizer(JSRuntime*, JSValue val) {
    if (auto* r = static_cast<MethodRef*>(JS_GetOpaque(val, g_nativeMethodId)))
        std::free(r);
}

/**
 * Chamar o método do jogo, como função JS.
 *
 *   Terraria.Main['void NewText(string text)']('oi');     // estático
 *   item['void SetDefaults(int Type)'](98);               // de instância
 *
 * De onde sai a instância, nesta ordem:
 *   1. `this` — é o caso natural de `obj.Metodo(args)`;
 *   2. o primeiro argumento, se for objeto do jogo — é a forma do
 *      `original(self, ...)` do callback de hook.
 *
 * Exceção do lado do jogo NÃO é ignorada: vira exceção JS. Os devs do TL Pro
 * avisam que engolir isso derruba o jogo quando um invariante falha dentro de
 * um hook.
 */
JSValue gm_call(JSContext* ctx, JSValueConst func, JSValueConst thisVal,
                int argc, JSValueConst* argv, int) {
    auto* r = static_cast<MethodRef*>(JS_GetOpaque(func, g_nativeMethodId));
    if (!r) return JS_ThrowTypeError(ctx, "metodo invalido");

    Il2CppObject* instance = nullptr;
    if (r->isInstance) {
        instance = objectFromJS(thisVal);
        if (!instance && argc > 0) {
            instance = objectFromJS(argv[0]);
            if (instance) { ++argv; --argc; }
        }
        if (!instance) {
            return JS_ThrowTypeError(
                ctx, "'%s' e metodo de instancia: chame obj.Metodo(...) ou passe o objeto como 1o argumento",
                il2cpp::api().method_get_name(r->method));
        }
    }

    if (argc < r->paramCount) {
        return JS_ThrowTypeError(ctx, "'%s' espera %d argumento(s), recebeu %d",
                                 il2cpp::api().method_get_name(r->method),
                                 r->paramCount, argc);
    }

    ArgPack pack;
    if (!pack.build(ctx, r->method, argc, argv)) return JS_EXCEPTION;

    // STRUCT: para tipo por valor o runtime_invoke quer o ponteiro para os
    // DADOS, nao para o objeto que os encaixota. Passar o objeto faz o metodo
    // ler o cabecalho como se fosse o campo — sem erro, com lixo. Vale para
    // Vector2, Color, Rectangle e companhia.
    void* thisPtr = instance;
    auto& a = il2cpp::api();
    if (instance && a.method_get_class && a.class_is_valuetype) {
        Il2CppClass* owner = a.method_get_class(r->method);
        if (owner && a.class_is_valuetype(owner)) {
            thisPtr = reinterpret_cast<char*>(instance) + sizeof(Il2CppObject);
        }
    }

    Il2CppObject* exc = nullptr;
    Il2CppObject* ret = il2cpp::api().runtime_invoke(r->method, thisPtr, pack.data(), &exc);
    if (exc) {
        return JS_ThrowInternalError(ctx, "'%s' lancou excecao no jogo",
                                     il2cpp::api().method_get_name(r->method));
    }
    return fromReturn(ctx, r->method, ret);
}

// NativeMethod.hook(callback)
JSValue nm_hook(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    auto* r = static_cast<MethodRef*>(JS_GetOpaque(self, g_nativeMethodId));
    if (!r || argc < 1) return JS_EXCEPTION;
    if (!JS_IsFunction(ctx, argv[0]))
        return JS_ThrowTypeError(ctx, "hook(callback): callback deve ser funcao");
    if (!installJsHook(ctx, r->method, r->paramCount, r->isInstance, argv[0]))
        return JS_ThrowInternalError(ctx, "hook: falha ao instalar");
    return JS_UNDEFINED;
}

const JSCFunctionListEntry nm_proto[] = {
    JS_CFUNC_DEF("hook", 1, nm_hook),
};

// NativeClass.method(nome, paramCount) -> NativeMethod
JSValue nc_method(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    Il2CppClass* cls = classOf(self);
    if (!cls || argc < 2) return JS_ThrowTypeError(ctx, "method(nome, paramCount)");
    const char* name = JS_ToCString(ctx, argv[0]);
    int paramCount = 0; JS_ToInt32(ctx, &paramCount, argv[1]);
    JSValue r;
    if (!name) return JS_EXCEPTION;

    const MethodInfo* m = il2cpp::api().class_get_method_from_name(cls, name, paramCount);
    if (!m) {
        r = JS_ThrowTypeError(ctx, "metodo '%s'(%d args) nao encontrado", name, paramCount);
    } else {
        r = makeMethod(ctx, m);
    }
    JS_FreeCString(ctx, name);
    return r;
}

// ============================ GameArray ============================
//
// `Main.player[Main.myPlayer]`, `arr.length`. Sem isto metade dos mods
// clássicos de Terraria é impossível: pegar o jogador, varrer inimigos, mexer
// no inventário — tudo passa por array.
//
// O índice vira offset com o TAMANHO DO ELEMENTO, que difere: um Player[]
// guarda ponteiros (8 B); um Vector2[] guarda os structs em linha. Errar isso
// anda na memória errada sem dar erro.

JSClassID g_gameArrayId;

Il2CppArray* arrayOf(JSValueConst v) {
    return static_cast<Il2CppArray*>(JS_GetOpaque(v, g_gameArrayId));
}

/** Classe do elemento e quantos bytes ele ocupa dentro do array. */
struct ElemInfo { Il2CppClass* cls; size_t size; bool byValue; };

ElemInfo elemInfoOf(Il2CppArray* arr) {
    auto& a = il2cpp::api();
    ElemInfo e{nullptr, sizeof(void*), false};
    if (!arr || !a.class_get_element_class) return e;
    Il2CppClass* arrCls = a.object_get_class(reinterpret_cast<Il2CppObject*>(arr));
    e.cls = a.class_get_element_class(arrCls);
    if (e.cls && a.class_is_valuetype && a.class_is_valuetype(e.cls)) {
        e.byValue = true;
        uint32_t align = 0;
        e.size = a.class_value_size ? static_cast<size_t>(a.class_value_size(e.cls, &align))
                                    : sizeof(void*);
    }
    return e;
}

JSValue ga_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    Il2CppArray* arr = arrayOf(obj);
    const char* key = JS_AtomToCString(ctx, atom);
    if (!arr || !key) { if (key) JS_FreeCString(ctx, key); return JS_UNDEFINED; }
    std::string name(key);
    JS_FreeCString(ctx, key);

    if (name == "length") return JS_NewInt64(ctx, static_cast<int64_t>(arr->length));

    // Índice numérico?
    char* end = nullptr;
    long idx = std::strtol(name.c_str(), &end, 10);
    if (!end || *end != '\0' || name.empty()) return JS_UNDEFINED;
    if (idx < 0 || static_cast<uintptr_t>(idx) >= arr->length) {
        return JS_ThrowRangeError(ctx, "indice %ld fora de 0..%llu", idx,
                                  static_cast<unsigned long long>(arr->length));
    }

    ElemInfo e = elemInfoOf(arr);
    char* p = reinterpret_cast<char*>(arrayData(arr)) + static_cast<size_t>(idx) * e.size;
    if (!e.byValue) {
        auto* o = *reinterpret_cast<Il2CppObject**>(p);
        return o ? makeNativeObject(ctx, o) : JS_NULL;
    }
    // Elemento por valor: primitivo sai como número; struct ainda não.
    auto& a = il2cpp::api();
    const char* cn = a.class_get_name(e.cls);
    std::string n(cn ? cn : "");
    if (n == "Single") return JS_NewFloat64(ctx, *reinterpret_cast<float*>(p));
    if (n == "Double") return JS_NewFloat64(ctx, *reinterpret_cast<double*>(p));
    if (n == "Boolean") return JS_NewBool(ctx, *p != 0);
    if (n == "Byte" || n == "SByte") return JS_NewInt32(ctx, *reinterpret_cast<int8_t*>(p));
    if (n == "Int16" || n == "UInt16") return JS_NewInt32(ctx, *reinterpret_cast<int16_t*>(p));
    if (n == "Int64" || n == "UInt64") return JS_NewInt64(ctx, *reinterpret_cast<int64_t*>(p));
    if (n == "Int32" || n == "UInt32") return JS_NewInt32(ctx, *reinterpret_cast<int32_t*>(p));
    return JS_ThrowTypeError(ctx, "elemento do tipo %s (struct) ainda nao suportado", n.c_str());
}

int ga_exotic_set(JSContext* ctx, JSValueConst obj, JSAtom atom,
                  JSValueConst value, JSValueConst, int) {
    Il2CppArray* arr = arrayOf(obj);
    const char* key = JS_AtomToCString(ctx, atom);
    if (!arr || !key) { if (key) JS_FreeCString(ctx, key); return -1; }
    std::string name(key);
    JS_FreeCString(ctx, key);

    char* end = nullptr;
    long idx = std::strtol(name.c_str(), &end, 10);
    if (!end || *end != '\0' || name.empty()) {
        return JS_DefinePropertyValue(ctx, obj, atom, JS_DupValue(ctx, value),
                                      JS_PROP_C_W_E) < 0 ? -1 : true;
    }
    if (idx < 0 || static_cast<uintptr_t>(idx) >= arr->length) {
        JS_ThrowRangeError(ctx, "indice %ld fora de 0..%llu", idx,
                           static_cast<unsigned long long>(arr->length));
        return -1;
    }

    ElemInfo e = elemInfoOf(arr);
    char* p = reinterpret_cast<char*>(arrayData(arr)) + static_cast<size_t>(idx) * e.size;
    if (!e.byValue) {
        *reinterpret_cast<Il2CppObject**>(p) = objectFromJS(value);
        return true;
    }
    auto& a = il2cpp::api();
    const char* cn = a.class_get_name(e.cls);
    std::string n(cn ? cn : "");
    if (n == "Single" || n == "Double") {
        double d = 0; if (JS_ToFloat64(ctx, &d, value) < 0) return -1;
        if (n == "Single") *reinterpret_cast<float*>(p) = static_cast<float>(d);
        else *reinterpret_cast<double*>(p) = d;
        return true;
    }
    if (n == "Boolean") { *p = JS_ToBool(ctx, value) ? 1 : 0; return true; }
    int64_t v = 0;
    if (JS_ToInt64(ctx, &v, value) < 0) return -1;
    if (n == "Byte" || n == "SByte") *reinterpret_cast<int8_t*>(p) = static_cast<int8_t>(v);
    else if (n == "Int16" || n == "UInt16") *reinterpret_cast<int16_t*>(p) = static_cast<int16_t>(v);
    else if (n == "Int64" || n == "UInt64") *reinterpret_cast<int64_t*>(p) = v;
    else *reinterpret_cast<int32_t*>(p) = static_cast<int32_t>(v);
    return true;
}

const JSClassExoticMethods ga_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr, ga_exotic_get, ga_exotic_set,
};

JSValue makeGameArray(JSContext* ctx, Il2CppArray* arr) {
    JSValue v = JS_NewObjectClass(ctx, g_gameArrayId);
    if (!JS_IsException(v)) JS_SetOpaque(v, arr);
    return v;
}


} // namespace

// Exportado (Bridge.h): usado pelo dispatcher de hook para entregar o `self`.
Il2CppObject* objectFromJS(JSValueConst v) {
    return static_cast<Il2CppObject*>(JS_GetOpaque(v, g_nativeObjectId));
}

JSValue makeNativeObject(JSContext* ctx, Il2CppObject* obj) {
    JSValue o = JS_NewObjectClass(ctx, g_nativeObjectId);
    if (!JS_IsException(o)) JS_SetOpaque(o, obj);
    return o;
}

// ============================ Namespace ============================
//
// `Terraria.Item`, `Terraria.ID.ItemID`. Cada objeto guarda um PREFIXO; o
// acesso a uma propriedade tenta primeiro resolver "<prefixo>.<nome>" como
// classe e, se não for, devolve outro Namespace com o prefixo estendido.
//
// Preguiçoso de propósito: o IL2CPP não tem API de "existe este namespace?",
// e materializar a árvore inteira no boot custaria varrer dezenas de milhares
// de classes. Se o caminho não levar a lugar nenhum, o erro aparece no acesso
// final, com o caminho completo.

JSClassID g_namespaceId;

JSValue makeNamespace(JSContext* ctx, const std::string& prefix) {
    JSValue v = JS_NewObjectClass(ctx, g_namespaceId);
    if (JS_IsException(v)) return v;
    auto* p = new std::string(prefix);
    JS_SetOpaque(v, p);
    return v;
}

void ns_finalizer(JSRuntime*, JSValue val) {
    delete static_cast<std::string*>(JS_GetOpaque(val, g_namespaceId));
}

JSValue ns_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    auto* prefix = static_cast<std::string*>(JS_GetOpaque(obj, g_namespaceId));
    const char* key = JS_AtomToCString(ctx, atom);
    if (!prefix || !key) { if (key) JS_FreeCString(ctx, key); return JS_UNDEFINED; }
    std::string name(key);
    JS_FreeCString(ctx, key);

    // Coisas que o motor consulta em qualquer objeto. Sem isto, um
    // `console.log(Terraria)` viraria um Namespace chamado "toString".
    if (name == "toString" || name == "valueOf" || name == "constructor" ||
        name == "then" || name == "length" || name.rfind("Symbol.", 0) == 0) {
        return JS_UNDEFINED;
    }

    if (Il2CppClass* cls = il2cpp::findClassQuiet(*prefix, name)) {
        JSValue v = JS_NewObjectClass(ctx, g_nativeClassId);
        if (!JS_IsException(v)) JS_SetOpaque(v, cls);
        return v;
    }
    return makeNamespace(ctx, prefix->empty() ? name : *prefix + "." + name);
}

const JSClassExoticMethods ns_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr, ns_exotic_get, nullptr,
};

/** bl.classOf(ns, nome) — quando a árvore não ajuda. */
JSValue js_classOf(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_ThrowTypeError(ctx, "bl.classOf(namespace, nome)");
    return js_NativeClass(ctx, JS_UNDEFINED, argc, argv);
}

/**
 * Publica um global por namespace RAIZ encontrado nas imagens do jogo.
 *
 * Varre as classes uma vez, no boot, só para saber quais raízes existem
 * (Terraria, System, Microsoft, ReLogic...). É o que permite escrever
 * `Terraria.Item` sem o mod declarar nada.
 */
void installNamespaceRoots(JSContext* ctx, JSValue global) {
    auto& a = il2cpp::api();
    if (!a.image_get_class_count || !a.image_get_class || !a.class_get_namespace) {
        BL_WARN("namespaces: API de enumeracao ausente; use bl.classOf()");
        return;
    }
    std::set<std::string> roots;
    for (const Il2CppImage* img : {a.gameImage, a.corlibImage}) {
        if (!img) continue;
        size_t n = a.image_get_class_count(img);
        for (size_t i = 0; i < n; ++i) {
            Il2CppClass* c = a.image_get_class(img, i);
            if (!c) continue;
            const char* ns = a.class_get_namespace(c);
            if (!ns || !*ns) continue;
            const char* dot = std::strchr(ns, '.');
            roots.insert(dot ? std::string(ns, dot - ns) : std::string(ns));
        }
    }
    for (const auto& r : roots) {
        JS_SetPropertyStr(ctx, global, r.c_str(), makeNamespace(ctx, r));
    }
    BL_INFO("namespaces: %zu raizes publicadas", roots.size());
}

void installBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSRuntime* rt = JS_GetRuntime(ctx);
    JSValue global = JS_GetGlobalObject(ctx);

    // `bl` de Bunny Loader. Era `tl`, herdado de espelhar a API do TL Pro —
    // nome de outro produto na API pública do nosso não faz sentido.
    JSValue bl = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, bl, "log", JS_NewCFunction(ctx, js_bl_log, "log", 1));
    JS_SetPropertyStr(ctx, bl, "classOf", JS_NewCFunction(ctx, js_classOf, "classOf", 2));
    JS_SetPropertyStr(ctx, global, "bl", bl);

    // NativeClass
    JS_NewClassID(rt, &g_nativeClassId);
    static const JSClassDef ncDef = {
        "GameClass", nullptr, nullptr, nullptr,
        const_cast<JSClassExoticMethods*>(&nc_exotic),
    };
    JS_NewClass(rt, g_nativeClassId, &ncDef);
    JSValue ncProto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, ncProto, nc_proto, sizeof(nc_proto)/sizeof(nc_proto[0]));
    JS_SetClassProto(ctx, g_nativeClassId, ncProto);


    // NativeObject (sem constructor JS; criado por NativeClass.new)
    JS_NewClassID(rt, &g_nativeObjectId);
    static const JSClassDef noDef = {
        "GameObject", nullptr, nullptr, nullptr,
        const_cast<JSClassExoticMethods*>(&no_exotic),
    };
    JS_NewClass(rt, g_nativeObjectId, &noDef);
    JSValue noProto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, noProto, no_proto, sizeof(no_proto)/sizeof(no_proto[0]));
    JS_SetClassProto(ctx, g_nativeObjectId, noProto);

    // NativeMethod (criado por NativeClass.method; tem finalizador)
    JS_NewClassID(rt, &g_nativeMethodId);
    static const JSClassDef nmDef = { "GameMethod", nm_finalizer, nullptr, gm_call, nullptr };
    JS_NewClass(rt, g_nativeMethodId, &nmDef);
    JSValue nmProto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, nmProto, nm_proto, sizeof(nm_proto)/sizeof(nm_proto[0]));
    JS_SetClassProto(ctx, g_nativeMethodId, nmProto);

    // GameArray
    JS_NewClassID(rt, &g_gameArrayId);
    static const JSClassDef gaDef = {
        "GameArray", nullptr, nullptr, nullptr,
        const_cast<JSClassExoticMethods*>(&ga_exotic),
    };
    JS_NewClass(rt, g_gameArrayId, &gaDef);
    JS_SetClassProto(ctx, g_gameArrayId, JS_NewObject(ctx));

    // Namespace (arvore Terraria.*) — depois do g_nativeClassId existir.
    JS_NewClassID(rt, &g_namespaceId);
    static const JSClassDef nsDef = {
        "Namespace", ns_finalizer, nullptr, nullptr,
        const_cast<JSClassExoticMethods*>(&ns_exotic),
    };
    JS_NewClass(rt, g_namespaceId, &nsDef);
    JS_SetClassProto(ctx, g_namespaceId, JS_NewObject(ctx));
    installNamespaceRoots(ctx, global);

    JS_FreeValue(ctx, global);
    BL_INFO("bindings instalados (bl.log/classOf, arvore de namespaces)");
}

#else

void installBindings(void*) {}

#endif

} // namespace bl::script
