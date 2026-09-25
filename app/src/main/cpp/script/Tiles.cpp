#include "script/Tiles.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "runtime/ModTiles.h"
#include "script/Bridge.h"
#include "script/Items.h"
#include "script/Texture.h"
#include "runtime/TileAccess.h"

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

/** Na instalacao (thread do jogo, fora do JS): o setStaticDefaults de cada tile. */
void onTilesInstalled(int first, int last) {
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
                BL_ERROR("tile de mod %s: setStaticDefaults lancou: %s", name.c_str(),
                         exceptionText(g_ctx).c_str());
            }
            JS_FreeValue(g_ctx, r);
        }
        JS_FreeValue(g_ctx, fn);
    }
}

/**
 * bl.tiles.register({ name, texture, setStaticDefaults }) -> tipo
 *
 * O tipo sai na hora (TileID.Count + a ordem de registro) e da para usar no
 * `createTile` de um item desde o topo do arquivo; o jogo passa a conhecer o
 * tile na tela de titulo. `texture`: a folha de quadros do tile (16x16 com 2 de
 * margem, como as do jogo). `setStaticDefaults(type)` roda uma vez, quando as
 * tabelas do jogo ja tem o tipo (Main.tileSolid[type] = true...).
 */
JSValue js_register(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsObject(argv[0])) {
        return JS_ThrowTypeError(ctx, "bl.tiles.register({ name, texture })");
    }
    JSValueConst def = argv[0];
    runtime::ModTileDef d;
    d.mod = callerModId(ctx);
    d.name = stringProp(ctx, def, "name");
    const std::string texture = stringProp(ctx, def, "texture");
    if (d.name.empty()) return JS_ThrowTypeError(ctx, "bl.tiles.register: falta `name`");
    if (texture.empty()) return JS_ThrowTypeError(ctx, "bl.tiles.register(%s): falta `texture`", d.name.c_str());
    d.texture = resolveModPath(ctx, texture);
    if (!fileExists(d.texture)) {
        return JS_ThrowReferenceError(ctx, "bl.tiles.register(%s): textura nao existe: %s",
                                      d.name.c_str(), d.texture.c_str());
    }
    const std::string mod = d.mod, name = d.name;
    const int type = runtime::registerModTile(std::move(d));
    if (type < 0) {
        return JS_ThrowRangeError(ctx, "bl.tiles.register: '%s' ja foi registrado por este mod", name.c_str());
    }
    g_defs[type] = JS_DupValue(ctx, def);
    g_ctx = ctx;
    runtime::setTilesInstalledHook(onTilesInstalled);
    BL_INFO("tile de mod %s/%s -> tipo %d", mod.c_str(), name.c_str(), type);
    return JS_NewInt32(ctx, type);
}

/** bl.tiles.typeOf(nome) — o tipo de um tile DESTE mod pelo nome, ou -1. */
JSValue js_typeOf(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* name = argc >= 1 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!name) return JS_ThrowTypeError(ctx, "bl.tiles.typeOf(nome)");
    const int type = runtime::modTileTypeByName(callerModId(ctx), name);
    JS_FreeCString(ctx, name);
    return JS_NewInt32(ctx, type);
}

/** bl.tiles.isModTile(tipo) */
JSValue js_isModTile(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t t = -1;
    if (argc >= 1) JS_ToInt32(ctx, &t, argv[0]);
    return JS_NewBool(ctx, runtime::isModTile(t));
}

/** bl.tiles.setMapColor(tipo, r, g, b) — a cor no mapa (a do jogo mais proxima). */
JSValue js_setMapColor(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t v[4] = {-1, 0, 0, 0};
    for (int i = 0; i < 4 && i < argc; ++i) JS_ToInt32(ctx, &v[i], argv[i]);
    runtime::setModTileMapColor(v[0], v[1], v[2], v[3]);
    return JS_UNDEFINED;
}

/** bl.tiles.typeAt(x, y) — o tipo do tile ativo em (x, y), ou -1. */
JSValue js_typeAt(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t x = -1, y = -1;
    if (argc >= 2) { JS_ToInt32(ctx, &x, argv[0]); JS_ToInt32(ctx, &y, argv[1]); }
    return JS_NewInt32(ctx, runtime::tileTypeAt(x, y));
}

} // namespace

void installTilesApi(JSContext* ctx, JSValue bl) {
    JSValue tiles = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, tiles, "register", JS_NewCFunction(ctx, js_register, "register", 1));
    JS_SetPropertyStr(ctx, tiles, "isModTile", JS_NewCFunction(ctx, js_isModTile, "isModTile", 1));
    JS_SetPropertyStr(ctx, tiles, "typeAt", JS_NewCFunction(ctx, js_typeAt, "typeAt", 2));
    JS_SetPropertyStr(ctx, tiles, "setMapColor", JS_NewCFunction(ctx, js_setMapColor, "setMapColor", 4));
    JS_SetPropertyStr(ctx, tiles, "vanillaCount", JS_NewInt32(ctx, runtime::kVanillaTileCount));
    JS_SetPropertyStr(ctx, tiles, "typeOf", JS_NewCFunction(ctx, js_typeOf, "typeOf", 1));
    JS_SetPropertyStr(ctx, bl, "tiles", tiles);
}

} // namespace bl::script
#endif
