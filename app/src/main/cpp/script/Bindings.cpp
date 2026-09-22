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
//   [ok] NativeClass('Ns','Class') -> objeto com o Il2CppClass*
//   [ok] .getStaticInt(name) / .getStaticFloat(name)  (ler campos estaticos)
//   [ ] metodos (.call via runtime_invoke), campos de instancia, .hook()
//
// Proximos: NativeMethod + runtime_invoke; NativeObject; e o .hook() ligando
// callback JS ao HookManager (o recurso matador). Ver docs/ARCHITECTURE.

namespace bl::script {

#if BL_HAVE_QUICKJS

namespace {

JSClassID g_nativeClassId;

// --- tl.log ---
JSValue js_tl_log(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    for (int i = 0; i < argc; ++i) {
        const char* text = JS_ToCString(ctx, argv[i]);
        BL_INFO("[mod] %s", text ? text : "undefined");
        if (text) JS_FreeCString(ctx, text);
    }
    return JS_UNDEFINED;
}

// --- NativeClass ---
Il2CppClass* classOf(JSValueConst v) {
    return static_cast<Il2CppClass*>(JS_GetOpaque(v, g_nativeClassId));
}

// Le um campo estatico primitivo (byte/short/int/enum/long) como inteiro.
JSValue nc_getStaticInt(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    Il2CppClass* cls = classOf(self);
    if (!cls || argc < 1) return JS_EXCEPTION;
    const char* name = JS_ToCString(ctx, argv[0]);
    if (!name) return JS_EXCEPTION;

    FieldInfo* f = il2cpp::findField(cls, name);
    JSValue r;
    if (!f) {
        r = JS_ThrowTypeError(ctx, "campo estatico '%s' nao encontrado", name);
    } else {
        uint8_t buf[16] = {0};
        il2cpp::api().field_static_get_value(f, buf);
        int64_t v;
        std::memcpy(&v, buf, sizeof(v));  // baixa; ok para ate 8 bytes
        r = JS_NewInt64(ctx, v);
    }
    JS_FreeCString(ctx, name);
    return r;
}

JSValue nc_getStaticFloat(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    Il2CppClass* cls = classOf(self);
    if (!cls || argc < 1) return JS_EXCEPTION;
    const char* name = JS_ToCString(ctx, argv[0]);
    if (!name) return JS_EXCEPTION;

    FieldInfo* f = il2cpp::findField(cls, name);
    JSValue r;
    if (!f) {
        r = JS_ThrowTypeError(ctx, "campo estatico '%s' nao encontrado", name);
    } else {
        uint8_t buf[16] = {0};
        il2cpp::api().field_static_get_value(f, buf);
        float v;
        std::memcpy(&v, buf, sizeof(v));
        r = JS_NewFloat64(ctx, static_cast<double>(v));
    }
    JS_FreeCString(ctx, name);
    return r;
}

// NativeClass('Namespace', 'Class') -> objeto NativeClass
JSValue js_NativeClass(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_ThrowTypeError(ctx, "NativeClass(namespace, nome)");
    const char* ns = JS_ToCString(ctx, argv[0]);
    const char* name = JS_ToCString(ctx, argv[1]);
    JSValue r;
    if (!ns || !name) {
        r = JS_EXCEPTION;
    } else {
        Il2CppClass* cls = il2cpp::findClass({ns, name, {}});
        if (!cls) {
            r = JS_ThrowTypeError(ctx, "classe '%s.%s' nao encontrada", ns, name);
        } else {
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
};

} // namespace

void installBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSRuntime* rt = JS_GetRuntime(ctx);
    JSValue global = JS_GetGlobalObject(ctx);

    // tl.log
    JSValue tl = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, tl, "log", JS_NewCFunction(ctx, js_tl_log, "log", 1));
    JS_SetPropertyStr(ctx, global, "tl", tl);

    // Classe NativeClass (objeto com Il2CppClass* opaco + metodos no prototipo).
    JS_NewClassID(rt, &g_nativeClassId);
    static const JSClassDef def = { "NativeClass", nullptr, nullptr, nullptr, nullptr };
    JS_NewClass(rt, g_nativeClassId, &def);
    JSValue proto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, proto, nc_proto,
                               sizeof(nc_proto) / sizeof(nc_proto[0]));
    JS_SetClassProto(ctx, g_nativeClassId, proto);

    // Constructor: mods usam `new NativeClass(...)` (como no TL Pro).
    JS_SetPropertyStr(ctx, global, "NativeClass",
                      JS_NewCFunction2(ctx, js_NativeClass, "NativeClass", 2,
                                       JS_CFUNC_constructor, 0));

    JS_FreeValue(ctx, global);
    BL_INFO("bindings instalados (tl.log, NativeClass.getStatic*)");
}

#else

void installBindings(void*) {}

#endif

} // namespace bl::script
