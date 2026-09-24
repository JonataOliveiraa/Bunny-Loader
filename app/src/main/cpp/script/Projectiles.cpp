#include "script/Projectiles.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "runtime/ModProjectiles.h"
#include "script/Bridge.h"
#include "script/Texture.h"

#include <cstdio>
#include <map>
#include <string>

namespace bl::script {

namespace {

// tipo -> a definicao que o mod passou (com o setDefaults dele).
std::map<int, JSValue> g_defs;
bool g_hook = false;

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

std::string exceptionText(JSContext* ctx) {
    JSValue e = JS_GetException(ctx);
    const char* t = JS_ToCString(ctx, e);
    std::string s = t ? t : "?";
    if (t) JS_FreeCString(ctx, t);
    JS_FreeValue(ctx, e);
    return s;
}

/**
 * O hook de Projectile.SetDefaults para os projeteis de mod — um so, para
 * todos. Diferente do item, o original RODA tambem para tipo de mod: ele nao
 * zera tipo desconhecido, so zera os campos, grava o tipo e nao casa nenhum
 * ramo. Depois dele, o setDefaults do mod preenche.
 *
 * Erro no setDefaults do mod fica AQUI, como no item: o projetil fica com os
 * valores zerados do original, e o jogo segue.
 */
JSValue js_modSetDefaults(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    JSValue r = JS_Call(ctx, argv[0], JS_UNDEFINED, argc - 1, argv + 1);
    if (JS_IsException(r)) return r;
    JS_FreeValue(ctx, r);

    int32_t type = 0;
    if (argc >= 3) JS_ToInt32(ctx, &type, argv[2]);
    auto it = g_defs.find(type);
    if (it == g_defs.end()) return JS_UNDEFINED;
    Il2CppObject* self = objectFromJS(argv[1]);
    if (!self) return JS_UNDEFINED;

    JSValue fn = JS_GetPropertyStr(ctx, it->second, "setDefaults");
    if (JS_IsFunction(ctx, fn)) {
        JSValue res = JS_Call(ctx, fn, it->second, 1, &argv[1]);
        if (JS_IsException(res)) {
            const std::string name = stringProp(ctx, it->second, "name");
            BL_ERROR("projetil de mod %s: setDefaults lancou: %s", name.c_str(), exceptionText(ctx).c_str());
        }
        JS_FreeValue(ctx, res);
    }
    JS_FreeValue(ctx, fn);
    runtime::finishModProjectile(self, type);
    return JS_UNDEFINED;
}

bool installSetDefaultsHook(JSContext* ctx) {
    if (g_hook) return true;
    Il2CppClass* proj = il2cpp::findClass({"Terraria", "Projectile", {}});
    const MethodInfo* m = proj ? il2cpp::findMethodBySignature(
        proj, il2cpp::parseSignature("void SetDefaults(int Type)")) : nullptr;
    if (!m) {
        JS_ThrowInternalError(ctx, "bl.projectiles: Projectile.SetDefaults nao encontrado nesta versao");
        return false;
    }
    JSValue cb = JS_NewCFunction(ctx, js_modSetDefaults, "modProjectileSetDefaults", 3);
    g_hook = installJsHook(ctx, m, 1, true, cb);
    JS_FreeValue(ctx, cb);
    return g_hook;
}

/**
 * bl.projectiles.register({ name, texture, displayName, frames, setDefaults }) -> tipo
 *
 * Como bl.items.register: o tipo sai na hora (ProjectileID.Count + a ordem de
 * registro) e da para usar no `shoot` de um item desde o topo do arquivo; o
 * jogo passa a conhecer o projetil na tela de titulo. `frames`: quadros de
 * animacao empilhados na vertical na textura (padrao 1).
 */
JSValue js_register(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsObject(argv[0])) {
        return JS_ThrowTypeError(
            ctx, "bl.projectiles.register({ name, texture, displayName, frames, setDefaults })");
    }
    JSValueConst def = argv[0];
    runtime::ModProjectileDef d;
    d.mod = callerModId(ctx);
    d.name = stringProp(ctx, def, "name");
    const std::string texture = stringProp(ctx, def, "texture");
    if (d.name.empty()) return JS_ThrowTypeError(ctx, "bl.projectiles.register: falta `name`");
    if (texture.empty()) {
        return JS_ThrowTypeError(ctx, "bl.projectiles.register(%s): falta `texture`", d.name.c_str());
    }
    d.texture = resolveModPath(ctx, texture);
    if (!fileExists(d.texture)) {
        return JS_ThrowReferenceError(ctx, "bl.projectiles.register(%s): textura nao existe: %s",
                                      d.name.c_str(), d.texture.c_str());
    }
    JSValue frames = JS_GetPropertyStr(ctx, def, "frames");
    if (!JS_IsUndefined(frames)) {
        int32_t n = 1;
        if (JS_ToInt32(ctx, &n, frames) < 0 || n < 1) {
            JS_FreeValue(ctx, frames);
            return JS_ThrowRangeError(ctx, "bl.projectiles.register(%s): `frames` tem de ser >= 1",
                                      d.name.c_str());
        }
        d.frames = n;
    }
    JS_FreeValue(ctx, frames);

    // displayName: texto, ou { 'pt-BR': ..., 'en-US': ... }. Aparece na
    // mensagem de morte ("... foi morto pela <projetil> de ...").
    JSValue dn = JS_GetPropertyStr(ctx, def, "displayName");
    if (JS_IsString(dn)) {
        const char* c = JS_ToCString(ctx, dn);
        if (c) { d.names.push_back({"", c}); JS_FreeCString(ctx, c); }
    } else if (JS_IsObject(dn)) {
        JSPropertyEnum* props = nullptr;
        uint32_t n = 0;
        if (JS_GetOwnPropertyNames(ctx, &props, &n, dn, JS_GPN_STRING_MASK | JS_GPN_ENUM_ONLY) == 0) {
            for (uint32_t i = 0; i < n; ++i) {
                const char* k = JS_AtomToCString(ctx, props[i].atom);
                JSValue v = JS_GetProperty(ctx, dn, props[i].atom);
                const char* s = JS_IsString(v) ? JS_ToCString(ctx, v) : nullptr;
                if (k && s) d.names.push_back({k, s});
                if (s) JS_FreeCString(ctx, s);
                if (k) JS_FreeCString(ctx, k);
                JS_FreeValue(ctx, v);
                JS_FreeAtom(ctx, props[i].atom);
            }
            js_free(ctx, props);
        }
    }
    JS_FreeValue(ctx, dn);

    if (!installSetDefaultsHook(ctx)) return JS_EXCEPTION;
    const std::string mod = d.mod, name = d.name;
    const int type = runtime::registerModProjectile(std::move(d));
    if (type < 0) {
        return JS_ThrowRangeError(ctx, "bl.projectiles.register: '%s' ja foi registrado por este mod",
                                  name.c_str());
    }
    g_defs[type] = JS_DupValue(ctx, def);
    BL_INFO("projetil de mod %s/%s -> tipo %d", mod.c_str(), name.c_str(), type);
    return JS_NewInt32(ctx, type);
}

/** bl.projectiles.isModProjectile(tipo) */
JSValue js_isModProjectile(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t t = -1;
    if (argc >= 1) JS_ToInt32(ctx, &t, argv[0]);
    return JS_NewBool(ctx, runtime::isModProjectile(t));
}

} // namespace

void installProjectilesApi(JSContext* ctx, JSValue bl) {
    JSValue projectiles = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, projectiles, "register", JS_NewCFunction(ctx, js_register, "register", 1));
    JS_SetPropertyStr(ctx, projectiles, "isModProjectile",
                      JS_NewCFunction(ctx, js_isModProjectile, "isModProjectile", 1));
    JS_SetPropertyStr(ctx, bl, "projectiles", projectiles);
}

} // namespace bl::script
#endif
