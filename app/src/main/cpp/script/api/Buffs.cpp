#include "script/api/Buffs.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "content/buffs/ModBuffs.h"
#include "script/bridge/Bridge.h"
#include "script/api/Items.h"
#include "script/api/Texture.h"

#include <cstdio>
#include <map>
#include <string>

namespace bl::script {

namespace {

// tipo -> a definicao que o mod passou (com o setStaticDefaults dele).
std::map<int, JSValue> g_defs;
// O contexto do motor: o setStaticDefaults roda fora de qualquer chamada JS.
JSContext* g_ctx = nullptr;

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

/** Texto, ou { 'pt-BR': ..., 'en-US': ... }: os pares cultura -> texto. */
std::vector<std::pair<std::string, std::string>> culturesProp(JSContext* ctx, JSValueConst obj,
                                                              const char* prop) {
    std::vector<std::pair<std::string, std::string>> out;
    JSValue v = JS_GetPropertyStr(ctx, obj, prop);
    if (JS_IsString(v)) {
        const char* c = JS_ToCString(ctx, v);
        if (c) { out.push_back({"", c}); JS_FreeCString(ctx, c); }
    } else if (JS_IsObject(v)) {
        JSPropertyEnum* props = nullptr;
        uint32_t n = 0;
        if (JS_GetOwnPropertyNames(ctx, &props, &n, v, JS_GPN_STRING_MASK | JS_GPN_ENUM_ONLY) == 0) {
            for (uint32_t i = 0; i < n; ++i) {
                const char* k = JS_AtomToCString(ctx, props[i].atom);
                JSValue e = JS_GetProperty(ctx, v, props[i].atom);
                const char* s = JS_IsString(e) ? JS_ToCString(ctx, e) : nullptr;
                if (k && s) out.push_back({k, s});
                if (s) JS_FreeCString(ctx, s);
                if (k) JS_FreeCString(ctx, k);
                JS_FreeValue(ctx, e);
                JS_FreeAtom(ctx, props[i].atom);
            }
            js_free(ctx, props);
        }
    }
    JS_FreeValue(ctx, v);
    return out;
}

bool fileExists(const std::string& path) {
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return false;
    std::fclose(f);
    return true;
}

std::string exceptionText(JSContext* ctx) {
    JSValue e = JS_GetException(ctx);
    const char* t = JS_ToCString(ctx, e);
    std::string s = t ? t : "?";
    if (t) JS_FreeCString(ctx, t);
    JS_FreeValue(ctx, e);
    return s;
}

/** Na instalacao (thread do jogo, fora do JS): o setStaticDefaults de cada buff. */
void onBuffsInstalled(int first, int last) {
    if (!g_ctx) return;
    JsLock lock;
    for (int type = first; type <= last; ++type) {
        auto it = g_defs.find(type);
        if (it == g_defs.end()) continue;
        JSValue fn = JS_GetPropertyStr(g_ctx, it->second, "setStaticDefaults");
        if (JS_IsFunction(g_ctx, fn)) {
            JSValue t = JS_NewInt32(g_ctx, type);
            JSValue r = JS_Call(g_ctx, fn, it->second, 1, &t);
            if (JS_IsException(r)) {
                const std::string name = stringProp(g_ctx, it->second, "name");
                BL_ERROR("buff de mod %s: setStaticDefaults lancou: %s", name.c_str(),
                         exceptionText(g_ctx).c_str());
            }
            JS_FreeValue(g_ctx, r);
        }
        JS_FreeValue(g_ctx, fn);
    }
}

/**
 * bl.buffs.register({ name, texture, displayName, description,
 *                     setStaticDefaults }) -> tipo
 *
 * O tipo sai na hora (BuffID.Count + a ordem de registro) e da para usar no
 * `buffType` de um item desde o topo do arquivo; o jogo passa a conhecer o
 * buff na tela de titulo. `displayName` e `description`: texto, ou
 * { 'pt-BR': ..., 'en-US': ... }. `setStaticDefaults(type)` roda uma vez,
 * quando as tabelas do jogo ja tem o tipo (Main.debuff[type] = true...).
 */
JSValue js_register(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsObject(argv[0])) {
        return JS_ThrowTypeError(ctx, "bl.buffs.register({ name, texture, displayName, description })");
    }
    JSValueConst def = argv[0];
    runtime::ModBuffDef d;
    d.mod = callerModId(ctx);
    d.name = stringProp(ctx, def, "name");
    const std::string texture = stringProp(ctx, def, "texture");
    if (d.name.empty()) return JS_ThrowTypeError(ctx, "bl.buffs.register: falta `name`");
    if (texture.empty()) return JS_ThrowTypeError(ctx, "bl.buffs.register(%s): falta `texture`", d.name.c_str());
    d.texture = resolveModPath(ctx, texture);
    if (!fileExists(d.texture)) {
        return JS_ThrowReferenceError(ctx, "bl.buffs.register(%s): textura nao existe: %s",
                                      d.name.c_str(), d.texture.c_str());
    }
    d.names = culturesProp(ctx, def, "displayName");
    d.descriptions = culturesProp(ctx, def, "description");

    const std::string mod = d.mod, name = d.name;
    const int type = runtime::registerModBuff(std::move(d));
    if (type < 0) {
        return JS_ThrowRangeError(ctx, "bl.buffs.register: '%s' ja foi registrado por este mod", name.c_str());
    }
    g_defs[type] = JS_DupValue(ctx, def);
    g_ctx = ctx;
    runtime::setBuffsInstalledHook(onBuffsInstalled);
    noteModForMenu(mod);   // a pasta "Buffs" do mod e montada sozinha
    BL_INFO("buff de mod %s/%s -> tipo %d", mod.c_str(), name.c_str(), type);
    return JS_NewInt32(ctx, type);
}

/** bl.buffs.typeOf(nome) — o tipo de um buff DESTE mod pelo nome, ou -1. */
JSValue js_typeOf(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* name = argc >= 1 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!name) return JS_ThrowTypeError(ctx, "bl.buffs.typeOf(nome)");
    const int type = runtime::modBuffTypeByName(callerModId(ctx), name);
    JS_FreeCString(ctx, name);
    return JS_NewInt32(ctx, type);
}

/** bl.buffs.isModBuff(tipo) */
JSValue js_isModBuff(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t t = -1;
    if (argc >= 1) JS_ToInt32(ctx, &t, argv[0]);
    return JS_NewBool(ctx, runtime::isModBuff(t));
}

} // namespace

void installBuffsApi(JSContext* ctx, JSValue bl) {
    JSValue buffs = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, buffs, "register", JS_NewCFunction(ctx, js_register, "register", 1));
    JS_SetPropertyStr(ctx, buffs, "isModBuff", JS_NewCFunction(ctx, js_isModBuff, "isModBuff", 1));
    JS_SetPropertyStr(ctx, buffs, "vanillaCount", JS_NewInt32(ctx, runtime::kVanillaBuffCount));
    JS_SetPropertyStr(ctx, buffs, "typeOf", JS_NewCFunction(ctx, js_typeOf, "typeOf", 1));
    JS_SetPropertyStr(ctx, bl, "buffs", buffs);
}

} // namespace bl::script
#endif
