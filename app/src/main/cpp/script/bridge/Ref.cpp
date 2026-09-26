#include "script/bridge/Ref.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"

#include <new>

namespace bl::script {

namespace {

JSClassID g_refId = 0;

struct GameRef {
    void* ptr = nullptr;        // preso: a variavel do jogo; solto: nullptr
    TypeDesc pointee;           // o tipo da variavel (so vale preso)
    JSValue stored = JS_UNDEFINED;   // o valor quando solto
};

GameRef* refOf(JSValueConst v) {
    return static_cast<GameRef*>(JS_GetOpaque(v, g_refId));
}

/** Le a variavel presa. Struct sai copia: o endereco nao sobrevive ao hook. */
JSValue readBound(JSContext* ctx, const GameRef* r) {
    const TypeDesc& d = r->pointee;
    if (d.prim == Prim::Struct && !d.nullable()) return makeStructCopy(ctx, d.cls, r->ptr, d.size);
    return readAt(ctx, r->ptr, d, JS_UNDEFINED);
}

void ref_finalizer(JSRuntime* rt, JSValue v) {
    GameRef* r = refOf(v);
    if (!r) return;
    JS_FreeValueRT(rt, r->stored);
    delete r;
}

void ref_mark(JSRuntime* rt, JSValueConst v, JS_MarkFunc* mark) {
    if (GameRef* r = refOf(v)) JS_MarkValue(rt, r->stored, mark);
}

JSValue ref_ctor(JSContext* ctx, JSValueConst newTarget, int argc, JSValueConst* argv) {
    JSValue proto = JS_GetPropertyStr(ctx, newTarget, "prototype");
    if (JS_IsException(proto)) return proto;
    JSValue obj = JS_NewObjectProtoClass(ctx, proto, g_refId);
    JS_FreeValue(ctx, proto);
    if (JS_IsException(obj)) return obj;
    auto* r = new (std::nothrow) GameRef();
    if (!r) { JS_FreeValue(ctx, obj); return JS_ThrowOutOfMemory(ctx); }
    r->stored = argc >= 1 ? JS_DupValue(ctx, argv[0]) : JS_UNDEFINED;
    JS_SetOpaque(obj, r);
    return obj;
}

JSValue ref_get(JSContext* ctx, JSValueConst self) {
    GameRef* r = refOf(self);
    if (!r) return JS_ThrowTypeError(ctx, "nao e um Ref");
    return r->ptr ? readBound(ctx, r) : JS_DupValue(ctx, r->stored);
}

JSValue ref_set(JSContext* ctx, JSValueConst self, JSValueConst v) {
    GameRef* r = refOf(self);
    if (!r) return JS_ThrowTypeError(ctx, "nao e um Ref");
    if (r->ptr) {
        if (writeAt(ctx, r->ptr, r->pointee, v) < 0) return JS_EXCEPTION;
    } else {
        JS_FreeValue(ctx, r->stored);
        r->stored = JS_DupValue(ctx, v);
    }
    return JS_UNDEFINED;
}

JSValue ref_toString(JSContext* ctx, JSValueConst self, int, JSValueConst*) {
    JSValue v = ref_get(ctx, self);
    if (JS_IsException(v)) return v;
    JSValue s = JS_ToString(ctx, v);
    JS_FreeValue(ctx, v);
    if (JS_IsException(s)) return s;
    const char* c = JS_ToCString(ctx, s);
    JSValue out = JS_NewString(ctx, (std::string("Ref(") + (c ? c : "?") + ")").c_str());
    if (c) JS_FreeCString(ctx, c);
    JS_FreeValue(ctx, s);
    return out;
}

const JSCFunctionListEntry kRefProto[] = {
    JS_CGETSET_DEF("value", ref_get, ref_set),
    JS_CFUNC_DEF("toString", 0, ref_toString),
};

} // namespace

void installRefClass(JSContext* ctx, JSValueConst global) {
    JSRuntime* rt = JS_GetRuntime(ctx);
    JS_NewClassID(rt, &g_refId);
    static const JSClassDef def = {"Ref", ref_finalizer, ref_mark, nullptr, nullptr};
    JS_NewClass(rt, g_refId, &def);
    JSValue proto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, proto, kRefProto, sizeof(kRefProto) / sizeof(kRefProto[0]));
    JSValue ctor = JS_NewCFunction2(ctx, ref_ctor, "Ref", 1, JS_CFUNC_constructor, 0);
    JS_SetConstructor(ctx, ctor, proto);
    JS_SetClassProto(ctx, g_refId, proto);
    JS_SetPropertyStr(ctx, global, "Ref", ctor);
}

JSValue makeBoundRef(JSContext* ctx, void* ptr, const TypeDesc& pointee) {
    JSValue obj = JS_NewObjectClass(ctx, static_cast<int>(g_refId));
    if (JS_IsException(obj)) return obj;
    auto* r = new (std::nothrow) GameRef();
    if (!r) { JS_FreeValue(ctx, obj); return JS_ThrowOutOfMemory(ctx); }
    r->ptr = ptr;
    r->pointee = pointee;
    r->pointee.byRef = false;
    JS_SetOpaque(obj, r);
    return obj;
}

void unbindRef(JSContext* ctx, JSValueConst ref) {
    GameRef* r = refOf(ref);
    if (!r || !r->ptr) return;
    JSValue v = readBound(ctx, r);
    if (JS_IsException(v)) {
        JS_FreeValue(ctx, JS_GetException(ctx));
        v = JS_UNDEFINED;
    }
    JS_FreeValue(ctx, r->stored);
    r->stored = v;
    r->ptr = nullptr;
}

bool isRef(JSValueConst v) { return refOf(v) != nullptr; }

void* boundRefPtr(JSValueConst ref, const TypeDesc** type) {
    GameRef* r = refOf(ref);
    if (!r || !r->ptr) return nullptr;
    if (type) *type = &r->pointee;
    return r->ptr;
}

JSValue refStoredValue(JSContext* ctx, JSValueConst ref) {
    GameRef* r = refOf(ref);
    return r ? JS_DupValue(ctx, r->stored) : JS_UNDEFINED;
}

void refStore(JSContext* ctx, JSValueConst ref, JSValue v) {
    GameRef* r = refOf(ref);
    if (!r) { JS_FreeValue(ctx, v); return; }
    JS_FreeValue(ctx, r->stored);
    r->stored = v;
}

} // namespace bl::script
#endif
