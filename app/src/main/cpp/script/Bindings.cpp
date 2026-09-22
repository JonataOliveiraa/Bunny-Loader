#include "script/ScriptEngine.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include <cstdint>
#include <cstring>
#endif

// Ponte JavaScript <-> IL2CPP. Espelha a API do TL Pro. Estado atual:
//   [ok] tl.log
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

Il2CppClass* classOf(JSValueConst v) {
    return static_cast<Il2CppClass*>(JS_GetOpaque(v, g_nativeClassId));
}
Il2CppObject* objOf(JSValueConst v) {
    return static_cast<Il2CppObject*>(JS_GetOpaque(v, g_nativeObjectId));
}

// --- tl.log ---
JSValue js_tl_log(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
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

const JSCFunctionListEntry nc_proto[] = {
    JS_CFUNC_DEF("getStaticInt", 1, nc_getStaticInt),
    JS_CFUNC_DEF("getStaticFloat", 1, nc_getStaticFloat),
    JS_CFUNC_DEF("setStaticInt", 2, nc_setStaticInt),
    JS_CFUNC_DEF("setStaticFloat", 2, nc_setStaticFloat),
    JS_CFUNC_DEF("new", 0, nc_new),
};

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
    JSValue o = JS_NewObjectClass(ctx, g_nativeObjectId);
    if (!JS_IsException(o)) JS_SetOpaque(o, obj);
    return o;
}

} // namespace

void installBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSRuntime* rt = JS_GetRuntime(ctx);
    JSValue global = JS_GetGlobalObject(ctx);

    JSValue tl = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, tl, "log", JS_NewCFunction(ctx, js_tl_log, "log", 1));
    JS_SetPropertyStr(ctx, global, "tl", tl);

    // NativeClass
    JS_NewClassID(rt, &g_nativeClassId);
    static const JSClassDef ncDef = { "NativeClass", nullptr, nullptr, nullptr, nullptr };
    JS_NewClass(rt, g_nativeClassId, &ncDef);
    JSValue ncProto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, ncProto, nc_proto, sizeof(nc_proto)/sizeof(nc_proto[0]));
    JS_SetClassProto(ctx, g_nativeClassId, ncProto);
    JS_SetPropertyStr(ctx, global, "NativeClass",
                      JS_NewCFunction2(ctx, js_NativeClass, "NativeClass", 2,
                                       JS_CFUNC_constructor, 0));

    // NativeObject (sem constructor JS; criado por NativeClass.new)
    JS_NewClassID(rt, &g_nativeObjectId);
    static const JSClassDef noDef = { "NativeObject", nullptr, nullptr, nullptr, nullptr };
    JS_NewClass(rt, g_nativeObjectId, &noDef);
    JSValue noProto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, noProto, no_proto, sizeof(no_proto)/sizeof(no_proto[0]));
    JS_SetClassProto(ctx, g_nativeObjectId, noProto);

    JS_FreeValue(ctx, global);
    BL_INFO("bindings instalados (tl.log, NativeClass, NativeObject)");
}

#else

void installBindings(void*) {}

#endif

} // namespace bl::script
