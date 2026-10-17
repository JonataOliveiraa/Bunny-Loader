#include "script/api/Dusts.h"

#if BL_HAVE_QUICKJS
#include "content/dusts/ModDusts.h"
#include "core/Log.h"
#include "il2cpp/Resolver.h"
#include "script/bridge/Bridge.h"
#include "script/bridge/Value.h"
#include "script/api/Texture.h"

#include <cstdio>
#include <cstring>
#include <string>

namespace bl::script {

namespace {

// O contexto do motor e as funcoes do DustLoader (bl.dusts.setCallbacks).
JSContext* g_ctx = nullptr;
JSValue g_spawn = JS_UNDEFINED, g_update = JS_UNDEFINED, g_midUpdate = JS_UNDEFINED;
JSValue g_preDraw = JS_UNDEFINED, g_getAlpha = JS_UNDEFINED;
Il2CppClass* g_colorClass = nullptr;

// Hook de todo quadro: com o motor preso noutra thread, o quadro fica sem o mod.
constexpr int kLockMs = 100;

void report(const char* what) {
    JSValue e = JS_GetException(g_ctx);
    const char* t = JS_ToCString(g_ctx, e);
    BL_ERROR("ModDust.%s: %s", what, t ? t : "erro no callback");
    if (t) JS_FreeCString(g_ctx, t);
    JS_FreeValue(g_ctx, e);
}

// O objeto JS de cada Dust, guardado pela vaga: os Dust de Main.dust sao os
// mesmos o jogo inteiro (a pilha os reusa), e criar um objeto da ponte por
// poeira por quadro era a maior parte do custo do lote. O Dust.Reset troca
// os objetos: a vaga confere o ponteiro.
struct Cached {
    Il2CppObject* obj = nullptr;
    JSValue js = JS_UNDEFINED;
};
std::vector<Cached> g_wrappers;
int32_t g_dustIndex = -1, g_dustType = -1;

JSValue wrapDust(Il2CppObject* d) {
    if (g_dustIndex < 0) {
        Il2CppClass* cls = il2cpp::findClass({"Terraria", "Dust", {}});
        g_dustIndex = cls ? il2cpp::fieldOffset(cls, "dustIndex") : 0;
        g_dustType = cls ? il2cpp::fieldOffset(cls, "type") : 0;
        g_wrappers.resize(6001);
    }
    const int32_t i = g_dustIndex > 0 ? *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(d) + g_dustIndex) : -1;
    if (i < 0 || i >= static_cast<int32_t>(g_wrappers.size())) return makeNativeObject(g_ctx, d);
    Cached& c = g_wrappers[static_cast<size_t>(i)];
    if (c.obj != d) {
        JS_FreeValue(g_ctx, c.js);
        c.js = makeNativeObject(g_ctx, d);
        c.obj = d;
    }
    return JS_DupValue(g_ctx, c.js);
}

int32_t typeOf(Il2CppObject* d) {
    return g_dustType > 0 ? *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(d) + g_dustType) : -1;
}

/** fn(dusts, types) -> [bool...]: cada posicao que voltou boolean muda `flags`. */
void callBatch(JSValueConst fn, const char* what, const std::vector<Il2CppObject*>& dusts,
               std::vector<bool>& flags) {
    if (!g_ctx || !JS_IsFunction(g_ctx, fn)) return;
    JsLock lock(kLockMs);
    if (!lock.held()) return;
    JSValue args[2] = {JS_NewArray(g_ctx), JS_NewArray(g_ctx)};
    for (size_t i = 0; i < dusts.size(); ++i) {
        JS_SetPropertyUint32(g_ctx, args[0], static_cast<uint32_t>(i), wrapDust(dusts[i]));
        JS_SetPropertyUint32(g_ctx, args[1], static_cast<uint32_t>(i), JS_NewInt32(g_ctx, typeOf(dusts[i])));
    }
    JSValue r = JS_Call(g_ctx, fn, JS_UNDEFINED, 2, args);
    if (JS_IsException(r)) {
        report(what);
    } else if (JS_IsArray(r)) {
        for (size_t i = 0; i < flags.size(); ++i) {
            JSValue v = JS_GetPropertyUint32(g_ctx, r, static_cast<uint32_t>(i));
            if (JS_IsBool(v)) flags[i] = JS_ToBool(g_ctx, v) != 0;
            JS_FreeValue(g_ctx, v);
        }
    }
    JS_FreeValue(g_ctx, r);
    JS_FreeValue(g_ctx, args[0]);
    JS_FreeValue(g_ctx, args[1]);
}

void onSpawn(int type, Il2CppObject* dust) {
    if (!g_ctx || !JS_IsFunction(g_ctx, g_spawn)) return;
    JsLock lock(kLockMs);
    if (!lock.held()) return;
    JSValue args[2] = {JS_NewInt32(g_ctx, type), wrapDust(dust)};
    JSValue r = JS_Call(g_ctx, g_spawn, JS_UNDEFINED, 2, args);
    if (JS_IsException(r)) report("OnSpawn");
    JS_FreeValue(g_ctx, r);
    JS_FreeValue(g_ctx, args[1]);
}

void onUpdate(const std::vector<Il2CppObject*>& dusts, std::vector<bool>& vanilla) {
    callBatch(g_update, "Update", dusts, vanilla);
}

void onMidUpdate(const std::vector<Il2CppObject*>& dusts, std::vector<bool>& keepSpeed) {
    callBatch(g_midUpdate, "MidUpdate", dusts, keepSpeed);
}

void onPreDraw(const std::vector<Il2CppObject*>& dusts, std::vector<bool>& draw) {
    callBatch(g_preDraw, "PreDraw", dusts, draw);
}

bool onGetAlpha(Il2CppObject* dust, uint32_t& color) {
    if (!g_ctx || !g_colorClass || !JS_IsFunction(g_ctx, g_getAlpha)) return false;
    JsLock lock(kLockMs);
    if (!lock.held()) return false;
    JSValue args[3] = {wrapDust(dust), makeStructCopy(g_ctx, g_colorClass, &color, 4), JS_NewInt32(g_ctx, typeOf(dust))};
    JSValue r = JS_Call(g_ctx, g_getAlpha, JS_UNDEFINED, 3, args);
    bool changed = false;
    if (JS_IsException(r)) {
        report("GetAlpha");
    } else if (!JS_IsNull(r) && !JS_IsUndefined(r)) {
        Il2CppClass* cls = nullptr;
        size_t size = 0;
        void* data = structDataOf(r, &cls, &size);
        if (data && size >= 4) {
            std::memcpy(&color, data, 4);
            changed = true;
        }
    }
    JS_FreeValue(g_ctx, r);
    JS_FreeValue(g_ctx, args[0]);
    JS_FreeValue(g_ctx, args[1]);
    return changed;
}

std::string stringProp(JSContext* ctx, JSValueConst obj, const char* prop) {
    JSValue v = JS_GetPropertyStr(ctx, obj, prop);
    std::string s;
    if (JS_IsString(v)) {
        const char* c = JS_ToCString(ctx, v);
        if (c) { s = c; JS_FreeCString(ctx, c); }
    }
    JS_FreeValue(ctx, v);
    return s;
}

bool fileExists(const std::string& path) {
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return false;
    std::fclose(f);
    return true;
}

/**
 * bl.dusts.register({ name, texture, hooks }) -> tipo
 *
 * `texture`: o PNG do mod, ou "" para a folha do jogo (o `Texture => null`
 * do tModLoader). `hooks`: soma dos bits OnSpawn 1, Update 2, MidUpdate 4,
 * GetAlpha 8, PreDraw 16 — so eles chamam o JS.
 */
JSValue js_register(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsObject(argv[0])) return JS_ThrowTypeError(ctx, "bl.dusts.register({ name, texture, hooks })");
    runtime::ModDustDef d;
    d.mod = callerModId(ctx);
    d.name = stringProp(ctx, argv[0], "name");
    if (d.name.empty()) return JS_ThrowTypeError(ctx, "bl.dusts.register: missing `name`");
    const std::string texture = stringProp(ctx, argv[0], "texture");
    if (!texture.empty()) {
        d.texture = resolveModPath(ctx, texture);
        if (!fileExists(d.texture)) {
            return JS_ThrowReferenceError(ctx, "ModDust %s: texture not found: %s", d.name.c_str(), d.texture.c_str());
        }
    }
    JSValue hooks = JS_GetPropertyStr(ctx, argv[0], "hooks");
    uint32_t bits = 0;
    if (JS_IsNumber(hooks)) JS_ToUint32(ctx, &bits, hooks);
    JS_FreeValue(ctx, hooks);
    d.hooks = bits;

    const std::string name = d.name;
    const int type = runtime::registerModDust(std::move(d));
    if (type < 0) return JS_ThrowRangeError(ctx, "ModDust %s was already registered by this mod", name.c_str());
    g_ctx = ctx;
    return JS_NewInt32(ctx, type);
}

/** bl.dusts.setUpdateType(tipo, tipoDoJogo) — o ModDust.UpdateType (-1 = nenhum). */
JSValue js_setUpdateType(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t type = -1, update = -1;
    if (argc >= 1) JS_ToInt32(ctx, &type, argv[0]);
    if (argc >= 2) JS_ToInt32(ctx, &update, argv[1]);
    if (update >= runtime::kVanillaDustCount) {
        return JS_ThrowRangeError(ctx, "ModDust.UpdateType must be a vanilla DustID (got %d)", update);
    }
    runtime::setModDustUpdateType(type, update < 0 ? -1 : update);
    return JS_UNDEFINED;
}

/** bl.dusts.texture(tipo) -> Asset<Texture2D> (a folha do jogo se o mod nao tem PNG), ou null. */
JSValue js_texture(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t type = -1;
    if (argc >= 1) JS_ToInt32(ctx, &type, argv[0]);
    Il2CppObject* asset = runtime::modDustTextureAsset(type);
    return asset ? makeNativeObject(ctx, asset) : JS_NULL;
}

/** bl.dusts.typeOf(nome) — o tipo de uma poeira DESTE mod, ou -1. */
JSValue js_typeOf(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* name = argc >= 1 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!name) return JS_ThrowTypeError(ctx, "bl.dusts.typeOf(name)");
    const int type = runtime::modDustTypeByName(callerModId(ctx), name);
    JS_FreeCString(ctx, name);
    return JS_NewInt32(ctx, type);
}

JSValue js_isModDust(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t t = -1;
    if (argc >= 1) JS_ToInt32(ctx, &t, argv[0]);
    return JS_NewBool(ctx, runtime::isModDust(t));
}

void replace(JSContext* ctx, JSValue& slot, JSValueConst obj, const char* name) {
    JSValue v = JS_GetPropertyStr(ctx, obj, name);
    JS_FreeValue(ctx, slot);
    slot = JS_IsFunction(ctx, v) ? v : (JS_FreeValue(ctx, v), JS_UNDEFINED);
}

/** bl.dusts.setCallbacks({ spawn, update, midUpdate, preDraw, getAlpha }) — o DustLoader. */
JSValue js_setCallbacks(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsObject(argv[0])) return JS_ThrowTypeError(ctx, "bl.dusts.setCallbacks({...})");
    g_ctx = ctx;
    replace(ctx, g_spawn, argv[0], "spawn");
    replace(ctx, g_update, argv[0], "update");
    replace(ctx, g_midUpdate, argv[0], "midUpdate");
    replace(ctx, g_preDraw, argv[0], "preDraw");
    replace(ctx, g_getAlpha, argv[0], "getAlpha");
    if (!g_colorClass) g_colorClass = il2cpp::findClass({"Microsoft.Xna.Framework.Graphics", "Color", {}});
    runtime::DustCallbacks cb;
    cb.spawn = onSpawn;
    cb.update = onUpdate;
    cb.midUpdate = onMidUpdate;
    cb.preDraw = onPreDraw;
    cb.getAlpha = onGetAlpha;
    runtime::setDustCallbacks(cb);
    return JS_UNDEFINED;
}

/** bl.dusts.__pool() -> { active, activeCount, freeCount, repairs, modActive } (testes). */
JSValue js_pool(JSContext* ctx, JSValueConst, int, JSValueConst*) {
    const runtime::DustPoolStats s = runtime::dustPoolStats();
    JSValue o = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, o, "active", JS_NewInt32(ctx, s.active));
    JS_SetPropertyStr(ctx, o, "activeCount", JS_NewInt32(ctx, s.activeCount));
    JS_SetPropertyStr(ctx, o, "freeCount", JS_NewInt32(ctx, s.freeCount));
    JS_SetPropertyStr(ctx, o, "repairs", JS_NewInt32(ctx, s.repairs));
    JS_SetPropertyStr(ctx, o, "modActive", JS_NewInt32(ctx, s.modActive));
    JS_SetPropertyStr(ctx, o, "updateNs", JS_NewInt64(ctx, s.updateNs));
    JS_SetPropertyStr(ctx, o, "drawNs", JS_NewInt64(ctx, s.drawNs));
    JS_SetPropertyStr(ctx, o, "jsNs", JS_NewInt64(ctx, s.jsNs));
    JS_SetPropertyStr(ctx, o, "updates", JS_NewInt64(ctx, static_cast<int64_t>(s.updates)));
    JS_SetPropertyStr(ctx, o, "draws", JS_NewInt64(ctx, static_cast<int64_t>(s.draws)));
    JS_SetPropertyStr(ctx, o, "groupPasses", JS_NewInt64(ctx, s.groupPasses));
    JS_SetPropertyStr(ctx, o, "groupDusts", JS_NewInt64(ctx, s.groupDusts));
    return o;
}

/** bl.dusts.__repair() -> bool: confere a pilha agora (testes). */
JSValue js_repair(JSContext* ctx, JSValueConst, int, JSValueConst*) {
    return JS_NewBool(ctx, runtime::repairDustPool());
}

} // namespace

void installDustsApi(JSContext* ctx, JSValue bl) {
    JSValue dusts = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, dusts, "register", JS_NewCFunction(ctx, js_register, "register", 1));
    JS_SetPropertyStr(ctx, dusts, "setUpdateType", JS_NewCFunction(ctx, js_setUpdateType, "setUpdateType", 2));
    JS_SetPropertyStr(ctx, dusts, "texture", JS_NewCFunction(ctx, js_texture, "texture", 1));
    JS_SetPropertyStr(ctx, dusts, "typeOf", JS_NewCFunction(ctx, js_typeOf, "typeOf", 1));
    JS_SetPropertyStr(ctx, dusts, "isModDust", JS_NewCFunction(ctx, js_isModDust, "isModDust", 1));
    JS_SetPropertyStr(ctx, dusts, "setCallbacks", JS_NewCFunction(ctx, js_setCallbacks, "setCallbacks", 1));
    JS_SetPropertyStr(ctx, dusts, "vanillaCount", JS_NewInt32(ctx, runtime::kVanillaDustCount));
    JS_SetPropertyStr(ctx, dusts, "__pool", JS_NewCFunction(ctx, js_pool, "__pool", 0));
    JS_SetPropertyStr(ctx, dusts, "__repair", JS_NewCFunction(ctx, js_repair, "__repair", 0));
    JS_SetPropertyStr(ctx, bl, "dusts", dusts);
}

} // namespace bl::script
#endif
