#include "script/api/ItemDrawHooks.h"
#include "script/bridge/ScriptEngine.h"
#if BL_HAVE_QUICKJS && defined(__aarch64__)
#include "script/bridge/Bridge.h"
#include "script/bridge/Value.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "content/items/ItemDrawStages.h"
#include "core/Log.h"
#include "quickjs.h"
#include <cmath>
#include <cstring>

namespace {
struct Js {
    const char* name;
    JSContext* context = nullptr;
    JSValue before = JS_UNDEFINED, after = JS_UNDEFINED;
};
Js engines[2] = {{"World"}, {"Inventory"}};
Il2CppClass *colorClass = nullptr, *vectorClass = nullptr, *rectangleClass = nullptr;

JSValue copy(JSContext* ctx, Il2CppClass* cls, const void* data, size_t size) {
    return bl::script::makeStructCopy(ctx, cls, data, size);
}
void report(JSContext* ctx, const char* name) {
    JSValue error = JS_GetException(ctx);
    const char* text = JS_ToCString(ctx, error);
    BL_ERROR("ItemDraw.%s: %s", name, text ? text : "erro no callback");
    if (text) JS_FreeCString(ctx, text);
    JS_FreeValue(ctx, error);
}

bool worldBefore(void* user, void* raw) {
    Js& js = *static_cast<Js*>(user);
    auto& args = *static_cast<bl::runtime::content::ItemDrawWorld*>(raw);
    bl::script::JsLock lock;
    if (!lock.held()) return true;
    JSContext* ctx = js.context;
    JSValue values[4] = {
        copy(ctx, colorClass, &args.light, 4),
        copy(ctx, colorClass, &args.alpha, 4),
        JS_NewFloat64(ctx, args.rotation),
        JS_NewFloat64(ctx, args.scale),
    };
    JSValue result = JS_Call(ctx, js.before, JS_UNDEFINED, 4, values);
    bool go = true;
    if (JS_IsException(result)) report(ctx, js.name);
    else if (JS_IsArray(result)) {
        JSValue allowed = JS_GetPropertyUint32(ctx, result, 0);
        go = !JS_IsBool(allowed) || JS_ToBool(ctx, allowed);
        JS_FreeValue(ctx, allowed);
        for (unsigned i = 1; i <= 2; i++) {
            JSValue value = JS_GetPropertyUint32(ctx, result, i);
            double number;
            if (JS_IsNumber(value) && !JS_ToFloat64(ctx, &number, value) && std::isfinite(number)) {
                const float changed = static_cast<float>(number);
                if (std::isfinite(changed)) (i == 1 ? args.rotation : args.scale) = changed;
            }
            JS_FreeValue(ctx, value);
        }
    } else go = !JS_IsBool(result) || JS_ToBool(ctx, result);
    JS_FreeValue(ctx, result);
    for (JSValue& v : values) JS_FreeValue(ctx, v);
    return go;
}

bool inventoryBefore(void* user, void* raw) {
    Js& js = *static_cast<Js*>(user);
    const auto& args = *static_cast<const bl::runtime::content::ItemDrawInventory*>(raw);
    bl::script::JsLock lock;
    if (!lock.held()) return true;
    JSContext* ctx = js.context;
    JSValue values[8] = {
        bl::script::makeNativeObject(ctx, static_cast<Il2CppObject*>(args.item)),
        bl::script::makeNativeObject(ctx, static_cast<Il2CppObject*>(args.batch)),
        copy(ctx, vectorClass, args.position, 8),
        copy(ctx, rectangleClass, args.frame, 16),
        copy(ctx, colorClass, &args.light, 4),
        copy(ctx, colorClass, &args.environment, 4),
        copy(ctx, vectorClass, args.origin, 8),
        JS_NewFloat64(ctx, args.scale),
    };
    JSValue result = JS_Call(ctx, js.before, JS_UNDEFINED, 8, values);
    bool go = true;
    if (JS_IsException(result)) report(ctx, js.name);
    else go = !JS_IsBool(result) || JS_ToBool(ctx, result);
    JS_FreeValue(ctx, result);
    for (JSValue& v : values) JS_FreeValue(ctx, v);
    return go;
}

void after(void* user) {
    Js& js = *static_cast<Js*>(user);
    bl::script::JsLock lock;
    if (!lock.held()) return;
    JSValue result = JS_Call(js.context, js.after, JS_UNDEFINED, 0, nullptr);
    if (JS_IsException(result)) report(js.context, js.name);
    JS_FreeValue(js.context, result);
}

JSValue install(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc != 3 || !JS_IsString(argv[0]) || !JS_IsFunction(ctx, argv[1]) || !JS_IsFunction(ctx, argv[2]))
        return JS_ThrowTypeError(ctx, "installItemDrawHooks(World|Inventory, pre, post)");
    const char* name = JS_ToCString(ctx, argv[0]);
    if (!name) return JS_EXCEPTION;
    int index = -1;
    for (int i = 0; i < 2; i++) if (!std::strcmp(engines[i].name, name)) index = i;
    JS_FreeCString(ctx, name);
    if (index < 0 || engines[index].context) return JS_ThrowTypeError(ctx, "ItemDraw: etapa desconhecida ou já instalada");
    Js& js = engines[index];
    using namespace bl::il2cpp;
    colorClass = findClass({"Microsoft.Xna.Framework.Graphics", "Color", ""});
    vectorClass = findClass({"Microsoft.Xna.Framework", "Vector2", ""});
    rectangleClass = findClass({"Microsoft.Xna.Framework", "Rectangle", ""});
    if (!colorClass || !vectorClass || !rectangleClass) return JS_ThrowInternalError(ctx, "ItemDraw: estruturas nativas ausentes");
    js.before = JS_DupValue(ctx, argv[1]);
    js.after = JS_DupValue(ctx, argv[2]);
    js.context = ctx;
    bl::runtime::content::ItemDrawListener listener;
    listener.active = bl::script::hookFlag(std::string("item.Draw") + js.name);
    listener.pre = index == 0 ? worldBefore : inventoryBefore;
    listener.post = after;
    listener.user = &js;
    if (!bl::runtime::content::addItemDrawListener(static_cast<bl::runtime::content::ItemDrawKind>(index), listener)) {
        JS_FreeValue(ctx, js.before);
        JS_FreeValue(ctx, js.after);
        js.before = js.after = JS_UNDEFINED;
        js.context = nullptr;
        return JS_ThrowInternalError(ctx, "ItemDraw.%s: corpo nativo incompatível ou hook recusado", js.name);
    }
    return JS_UNDEFINED;
}
}
namespace bl::script {
void installItemDrawBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSValue global = JS_GetGlobalObject(ctx), api = JS_GetPropertyStr(ctx, global, "bl");
    JS_SetPropertyStr(ctx, api, "installItemDrawHooks", JS_NewCFunction(ctx, install, "installItemDrawHooks", 3));
    JS_FreeValue(ctx, api); JS_FreeValue(ctx, global);
}
}
#else
namespace bl::script { void installItemDrawBindings(void*) {} }
#endif
