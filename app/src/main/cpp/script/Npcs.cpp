#include "script/Npcs.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "runtime/ModNpcs.h"
#include "script/Bridge.h"
#include "script/Items.h"
#include "script/Texture.h"

#include <cstdio>
#include <map>
#include <string>

namespace bl::script {

namespace {

// tipo -> a definicao que o mod passou (setDefaults, setStaticDefaults, hitEffect).
std::map<int, JSValue> g_defs;
// O contexto do motor: o setStaticDefaults roda fora de qualquer chamada JS
// (na instalacao, chamada pelo nativo), e nao ha outro jeito de chega-lo.
JSContext* g_ctx = nullptr;
bool g_setDefaultsHook = false;
bool g_hitEffectHook = false;

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

/** def[fnName](args...), com o erro do mod no log e nao no jogo. */
void callDef(JSContext* ctx, JSValueConst def, const char* fnName, int argc, JSValueConst* argv) {
    JSValue fn = JS_GetPropertyStr(ctx, def, fnName);
    if (JS_IsFunction(ctx, fn)) {
        JSValue r = JS_Call(ctx, fn, def, argc, argv);
        if (JS_IsException(r)) {
            const std::string name = stringProp(ctx, def, "name");
            BL_ERROR("NPC de mod %s: %s lancou: %s", name.c_str(), fnName, exceptionText(ctx).c_str());
        }
        JS_FreeValue(ctx, r);
    }
    JS_FreeValue(ctx, fn);
}

/**
 * Hook de NPC.SetDefaults(tipo, spawnparams). Como o do projetil, o original
 * RODA tambem para tipo de mod (zera os campos e grava o tipo); depois vem o
 * setDefaults do mod, e o nativo refaz o final do SetDefaults do jogo com os
 * valores dele (vida, escala de dificuldade — ver ModNpcs.h).
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
    callDef(ctx, it->second, "setDefaults", 1, &argv[1]);
    runtime::finishModNpc(self, type);
    return JS_UNDEFINED;
}

/** Hook de NPC.HitEffect(direcao, dano): o original, e depois o hitEffect do mod. */
JSValue js_modHitEffect(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    JSValue r = JS_Call(ctx, argv[0], JS_UNDEFINED, argc - 1, argv + 1);
    if (JS_IsException(r)) return r;
    JS_FreeValue(ctx, r);
    Il2CppObject* self = argc >= 2 ? objectFromJS(argv[1]) : nullptr;
    if (!self) return JS_UNDEFINED;
    JSValue typeValue = JS_GetPropertyStr(ctx, argv[1], "type");
    int32_t type = 0;
    JS_ToInt32(ctx, &type, typeValue);
    JS_FreeValue(ctx, typeValue);
    auto it = g_defs.find(type);
    if (it != g_defs.end()) callDef(ctx, it->second, "hitEffect", argc - 1, argv + 1);
    return JS_UNDEFINED;
}

bool installHook(JSContext* ctx, const char* signature, int params, JSCFunction* fn, const char* name) {
    Il2CppClass* npc = il2cpp::findClass({"Terraria", "NPC", {}});
    const MethodInfo* m = npc ? il2cpp::findMethodBySignature(npc, il2cpp::parseSignature(signature)) : nullptr;
    if (!m) {
        JS_ThrowInternalError(ctx, "bl.npcs: NPC.%s nao encontrado nesta versao", name);
        return false;
    }
    JSValue cb = JS_NewCFunction(ctx, fn, name, params + 1);
    const bool ok = installJsHook(ctx, m, params, true, cb);
    JS_FreeValue(ctx, cb);
    return ok;
}

/** Na instalacao (thread do jogo, fora do JS): o setStaticDefaults de cada NPC. */
void onNpcsInstalled(int first, int last) {
    if (!g_ctx) return;
    JsLock lock;
    for (int type = first; type <= last; ++type) {
        auto it = g_defs.find(type);
        if (it == g_defs.end()) continue;
        JSValue t = JS_NewInt32(g_ctx, type);
        callDef(g_ctx, it->second, "setStaticDefaults", 1, &t);
    }
}

/**
 * bl.npcs.register({ name, texture, frames, animationType, displayName,
 *                    setDefaults, setStaticDefaults, hitEffect }) -> tipo
 *
 * O tipo sai na hora (NPCID.Count + a ordem de registro). `setDefaults(npc)`
 * recebe o NPC zerado, com o tipo certo; vida, dano e escala de dificuldade
 * sao calculados depois dele. `setStaticDefaults(type)` roda uma vez, quando o
 * jogo ja conhece o tipo (tabela de drop, sets). `hitEffect(npc, direcao,
 * dano)` a cada acerto, depois do efeito do jogo.
 */
JSValue js_register(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsObject(argv[0])) {
        return JS_ThrowTypeError(ctx, "bl.npcs.register({ name, texture, frames, animationType, "
                                      "displayName, setDefaults, setStaticDefaults, hitEffect })");
    }
    JSValueConst def = argv[0];
    runtime::ModNpcDef d;
    d.mod = callerModId(ctx);
    d.name = stringProp(ctx, def, "name");
    const std::string texture = stringProp(ctx, def, "texture");
    if (d.name.empty()) return JS_ThrowTypeError(ctx, "bl.npcs.register: falta `name`");
    if (texture.empty()) return JS_ThrowTypeError(ctx, "bl.npcs.register(%s): falta `texture`", d.name.c_str());
    d.texture = resolveModPath(ctx, texture);
    if (!fileExists(d.texture)) {
        return JS_ThrowReferenceError(ctx, "bl.npcs.register(%s): textura nao existe: %s",
                                      d.name.c_str(), d.texture.c_str());
    }
    for (const char* key : {"frames", "animationType"}) {
        JSValue v = JS_GetPropertyStr(ctx, def, key);
        if (!JS_IsUndefined(v)) {
            int32_t n = 0;
            const bool frames = key[0] == 'f';
            if (JS_ToInt32(ctx, &n, v) < 0 || n < (frames ? 1 : 0)) {
                JS_FreeValue(ctx, v);
                return JS_ThrowRangeError(ctx, "bl.npcs.register(%s): `%s` invalido", d.name.c_str(), key);
            }
            (frames ? d.frames : d.animationType) = n;
        }
        JS_FreeValue(ctx, v);
    }
    if (d.animationType >= runtime::kVanillaNpcCount) {
        return JS_ThrowRangeError(ctx, "bl.npcs.register(%s): `animationType` tem de ser um NPC do jogo",
                                  d.name.c_str());
    }

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

    if (!g_setDefaultsHook) {
        g_setDefaultsHook = installHook(ctx, "void SetDefaults(int Type, NPCSpawnParams spawnparams)", 2,
                                        js_modSetDefaults, "modNpcSetDefaults");
        if (!g_setDefaultsHook) return JS_EXCEPTION;
    }
    JSValue hit = JS_GetPropertyStr(ctx, def, "hitEffect");
    const bool wantsHit = JS_IsFunction(ctx, hit);
    JS_FreeValue(ctx, hit);
    if (wantsHit && !g_hitEffectHook) {
        g_hitEffectHook = installHook(ctx, "void HitEffect(int hitDirection, double dmg)", 2,
                                      js_modHitEffect, "modNpcHitEffect");
        if (!g_hitEffectHook) return JS_EXCEPTION;
    }

    const std::string mod = d.mod, name = d.name;
    const int type = runtime::registerModNpc(std::move(d));
    if (type < 0) {
        return JS_ThrowRangeError(ctx, "bl.npcs.register: '%s' ja foi registrado por este mod", name.c_str());
    }
    g_ctx = ctx;
    runtime::setNpcsInstalledHook(onNpcsInstalled);
    g_defs[type] = JS_DupValue(ctx, def);
    noteModForMenu(mod);   // a pasta "NPCs" do mod e montada sozinha
    BL_INFO("NPC de mod %s/%s -> tipo %d", mod.c_str(), name.c_str(), type);
    return JS_NewInt32(ctx, type);
}

/** bl.npcs.typeOf(nome) — o tipo de um npc DESTE mod pelo nome, ou -1. */
JSValue js_typeOf(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* name = argc >= 1 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!name) return JS_ThrowTypeError(ctx, "bl.npcs.typeOf(nome)");
    const int type = runtime::modNpcTypeByName(callerModId(ctx), name);
    JS_FreeCString(ctx, name);
    return JS_NewInt32(ctx, type);
}

/** bl.npcs.setFrames(tipo, quadros) — quadros definidos depois do registro. */
JSValue js_setFrames(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t type = -1, frames = 0;
    if (argc < 2 || JS_ToInt32(ctx, &type, argv[0]) < 0 || JS_ToInt32(ctx, &frames, argv[1]) < 0) {
        return JS_ThrowTypeError(ctx, "bl.npcs.setFrames(tipo, quadros)");
    }
    runtime::setModNpcFrames(type, frames);
    return JS_UNDEFINED;
}

/** bl.npcs.setAnimationType(tipo, npcDoJogo) — AnimationType definido depois do registro. */
JSValue js_setAnimationType(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t type = -1, animation = 0;
    if (argc < 2 || JS_ToInt32(ctx, &type, argv[0]) < 0 || JS_ToInt32(ctx, &animation, argv[1]) < 0) {
        return JS_ThrowTypeError(ctx, "bl.npcs.setAnimationType(tipo, npcDoJogo)");
    }
    if (animation < 0 || animation >= runtime::kVanillaNpcCount) {
        return JS_ThrowRangeError(ctx, "bl.npcs.setAnimationType: tem de ser um NPC do jogo");
    }
    runtime::setModNpcAnimation(type, animation);
    return JS_UNDEFINED;
}

/** bl.npcs.isModNpc(tipo) */
JSValue js_isModNpc(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t t = -1;
    if (argc >= 1) JS_ToInt32(ctx, &t, argv[0]);
    return JS_NewBool(ctx, runtime::isModNpc(t));
}

} // namespace

void installNpcsApi(JSContext* ctx, JSValue bl) {
    JSValue npcs = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, npcs, "register", JS_NewCFunction(ctx, js_register, "register", 1));
    JS_SetPropertyStr(ctx, npcs, "isModNpc", JS_NewCFunction(ctx, js_isModNpc, "isModNpc", 1));
    JS_SetPropertyStr(ctx, npcs, "typeOf", JS_NewCFunction(ctx, js_typeOf, "typeOf", 1));
    JS_SetPropertyStr(ctx, npcs, "setFrames", JS_NewCFunction(ctx, js_setFrames, "setFrames", 2));
    JS_SetPropertyStr(ctx, npcs, "setAnimationType",
                      JS_NewCFunction(ctx, js_setAnimationType, "setAnimationType", 2));
    JS_SetPropertyStr(ctx, bl, "npcs", npcs);
}

} // namespace bl::script
#endif
