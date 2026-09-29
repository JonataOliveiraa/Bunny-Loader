#include "script/api/Tiles.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "content/tiles/ModDoors.h"
#include "content/tiles/ModTileMap.h"
#include "content/tiles/ModTiles.h"
#include "content/tiles/ModWater.h"
#include "script/bridge/Bridge.h"
#include "script/api/Items.h"
#include "script/api/Texture.h"
#include "content/tiles/TileAccess.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <cstdio>
#include <cstdlib>
#include <cstring>
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
    BL_DEBUG("tile de mod %s/%s -> tipo %d", mod.c_str(), name.c_str(), type);
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

/**
 * bl.tiles.addMapEntry(tipo, r, g, b, chave) — uma entrada de mapa (cor e nome,
 * pela chave de um texto do jogo). Cada chamada e uma opcao a mais do tipo.
 */
JSValue js_addMapEntry(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t v[4] = {-1, 0, 0, 0};
    for (int i = 0; i < 4 && i < argc; ++i) JS_ToInt32(ctx, &v[i], argv[i]);
    const char* key = argc >= 5 ? JS_ToCString(ctx, argv[4]) : nullptr;
    const int option = runtime::addModTileMapEntry(v[0], v[1], v[2], v[3], key ? key : "");
    if (key) JS_FreeCString(ctx, key);
    return JS_NewInt32(ctx, option);
}

/** bl.tiles.mapTileAt(x, y) — { Type, Light, Color } da celula do mapa, ou undefined. */
JSValue js_mapTileAt(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t x = -1, y = -1;
    if (argc >= 2) { JS_ToInt32(ctx, &x, argv[0]); JS_ToInt32(ctx, &y, argv[1]); }
    int type = 0, light = 0, paint = 0;
    if (!runtime::readMapTile(x, y, &type, &light, &paint)) return JS_UNDEFINED;
    JSValue o = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, o, "Type", JS_NewInt32(ctx, type));
    JS_SetPropertyStr(ctx, o, "Light", JS_NewInt32(ctx, light));
    JS_SetPropertyStr(ctx, o, "Color", JS_NewInt32(ctx, paint));
    return o;
}

/** bl.tiles.typeAt(x, y) — o tipo do tile ativo em (x, y), ou -1. */
JSValue js_typeAt(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t x = -1, y = -1;
    if (argc >= 2) { JS_ToInt32(ctx, &x, argv[0]); JS_ToInt32(ctx, &y, argv[1]); }
    return JS_NewInt32(ctx, runtime::tileTypeAt(x, y));
}



// O callback do bl.tiles.onDrawData e o objeto que ele recebe (reusado).
JSValue g_drawDataFn = JS_UNDEFINED;
JSValue g_drawDataObj = JS_UNDEFINED;

struct DrawField {
    const char* name;
    int32_t runtime::TileDrawData::*member;
};
const DrawField kDrawFields[] = {
    {"frameX", &runtime::TileDrawData::frameX}, {"frameY", &runtime::TileDrawData::frameY},
    {"width", &runtime::TileDrawData::width},   {"height", &runtime::TileDrawData::height},
    {"top", &runtime::TileDrawData::top},       {"addFrX", &runtime::TileDrawData::addFrX},
    {"addFrY", &runtime::TileDrawData::addFrY}, {"effects", &runtime::TileDrawData::effects},
};

/**
 * No desenho de cada celula marcada (thread do desenho): o JS muda os campos
 * do objeto e o desenho usa o que voltar. Motor ocupado por muito tempo: a
 * celula sai como o jogo a montou (false: nao entra no cache).
 */
bool onDrawData(int x, int y, int type, runtime::TileDrawData* d) {
    JsLock lock(500);
    if (!lock.held() || !g_ctx || !JS_IsFunction(g_ctx, g_drawDataFn)) return false;
    JSContext* ctx = g_ctx;
    for (const DrawField& f : kDrawFields) JS_SetPropertyStr(ctx, g_drawDataObj, f.name, JS_NewInt32(ctx, d->*f.member));
    JSValue args[4] = {JS_NewInt32(ctx, x), JS_NewInt32(ctx, y), JS_NewInt32(ctx, type), JS_DupValue(ctx, g_drawDataObj)};
    JSValue r = JS_Call(ctx, g_drawDataFn, JS_UNDEFINED, 4, args);
    for (JSValue& a : args) JS_FreeValue(ctx, a);
    if (JS_IsException(r)) {
        static bool warned = false;
        if (!warned) {
            warned = true;
            BL_ERROR("tiles de mod: o desenho (SetDrawPositions...) lancou: %s", exceptionText(ctx).c_str());
        } else {
            JS_FreeValue(ctx, JS_GetException(ctx));
        }
        JS_FreeValue(ctx, r);
        return false;
    }
    JS_FreeValue(ctx, r);
    for (const DrawField& f : kDrawFields) {
        JSValue v = JS_GetPropertyStr(ctx, g_drawDataObj, f.name);
        int32_t n = d->*f.member;
        if (JS_ToInt32(ctx, &n, v) == 0) d->*f.member = n;
        JS_FreeValue(ctx, v);
    }
    return true;
}

/** bl.tiles.onDrawData(fn(x, y, type, d)): so os tipos marcados em 'tile.drawdata'. */
JSValue js_onDrawData(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsFunction(ctx, argv[0])) return JS_ThrowTypeError(ctx, "bl.tiles.onDrawData(fn)");
    JS_FreeValue(ctx, g_drawDataFn);
    g_drawDataFn = JS_DupValue(ctx, argv[0]);
    if (JS_IsUndefined(g_drawDataObj)) g_drawDataObj = JS_NewObject(ctx);
    g_ctx = ctx;
    runtime::setDrawDataHook(onDrawData, hookMarks("tile.drawdata"), hookMarks("tile.drawdata.uncached"));
    return JS_UNDEFINED;
}

using AddLightFn = void (*)(int32_t, int32_t, float, float, float, const MethodInfo*);

/** O Lighting.AddLight(int i, int j, float r, float g, float b) do jogo. */
const MethodInfo* addLightMethod() {
    static const MethodInfo* found = [] () -> const MethodInfo* {
        auto& a = il2cpp::api();
        Il2CppClass* lighting = il2cpp::findClass({"Terraria", "Lighting", {}});
        void* it = nullptr;
        while (const MethodInfo* m = lighting ? a.class_get_methods(lighting, &it) : nullptr) {
            if (std::strcmp(a.method_get_name(m), "AddLight") != 0 || a.method_get_param_count(m) != 5) continue;
            char* first = a.type_get_name(a.method_get_param(m, 0));
            char* third = a.type_get_name(a.method_get_param(m, 2));
            const bool match = first && third && std::strcmp(first, "System.Int32") == 0 && std::strcmp(third, "System.Single") == 0;
            std::free(first);
            std::free(third);
            if (match) return m;
        }
        BL_ERROR("tiles de mod: Lighting.AddLight(int, int, float, float, float) nao achado; tile de mod sem luz");
        return nullptr;
    }();
    return found;
}

/**
 * bl.tiles.addLights([i, j, r, g, b, i, j, r, g, b, ...]): o Lighting.AddLight
 * de cada tile, num laço nativo. A luz dos tiles de mod entra a cada quadro, e
 * uma ida à ponte por tile (150 tochas na tela) pesava no quadro.
 */
JSValue js_addLights(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const MethodInfo* m = addLightMethod();
    if (!m || argc < 1) return JS_UNDEFINED;
    const auto fn = reinterpret_cast<AddLightFn>(il2cpp::methodPointer(m));
    JSValue len = JS_GetPropertyStr(ctx, argv[0], "length");
    uint32_t n = 0;
    JS_ToUint32(ctx, &n, len);
    JS_FreeValue(ctx, len);

    double v[5];
    for (uint32_t k = 0; k + 5 <= n; k += 5) {
        for (uint32_t f = 0; f < 5; ++f) {
            JSValue x = JS_GetPropertyUint32(ctx, argv[0], k + f);
            JS_ToFloat64(ctx, &v[f], x);
            JS_FreeValue(ctx, x);
        }
        fn(static_cast<int32_t>(v[0]), static_cast<int32_t>(v[1]), static_cast<float>(v[2]),
           static_cast<float>(v[3]), static_cast<float>(v[4]), m);
    }
    return JS_UNDEFINED;
}
/** bl.tiles.setAnimationFrameHeight(tipo, altura): o AnimationFrameHeight (ModTiles.h). */
JSValue js_setAnimationFrameHeight(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t type = -1, height = 0;
    if (argc >= 2) { JS_ToInt32(ctx, &type, argv[0]); JS_ToInt32(ctx, &height, argv[1]); }
    runtime::setModTileAnimation(type, height);
    return JS_UNDEFINED;
}

/** bl.tiles.setDoor(fechada, aberta): o par de uma porta de mod (ModDoors.h). */
JSValue js_setDoor(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t closed = -1, open = -1;
    if (argc >= 2) { JS_ToInt32(ctx, &closed, argv[0]); JS_ToInt32(ctx, &open, argv[1]); }
    runtime::setModDoorPair(closed, open);
    return JS_UNDEFINED;
}

/**
 * bl.tiles.find(marcas, x0, y0, x1, y1[, quadros]) -> [i, j, i, j, ...] (com
 * `quadros`: [i, j, frameX, frameY, ...]): os tiles ativos
 * do retangulo (x1/y1 inclusos) cujo tipo esta marcado em bl.hookMarks. Varre
 * a memoria do mundo direto: o desenho das chamas e o NearbyEffects olham a
 * tela toda a cada quadro, e por Tile no JS seriam milhares de chamadas.
 */
JSValue js_find(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* name = argc >= 5 ? JS_ToCString(ctx, argv[0]) : nullptr;
    const bool frames = argc >= 6 && JS_ToBool(ctx, argv[5]) > 0;
    if (!name) return JS_ThrowTypeError(ctx, "bl.tiles.find(marcas, x0, y0, x1, y1)");
    const std::atomic<uint8_t>* marks = hookMarks(name);
    JS_FreeCString(ctx, name);
    int32_t x0 = 0, y0 = 0, x1 = -1, y1 = -1;
    JS_ToInt32(ctx, &x0, argv[1]);
    JS_ToInt32(ctx, &y0, argv[2]);
    JS_ToInt32(ctx, &x1, argv[3]);
    JS_ToInt32(ctx, &y1, argv[4]);

    JSValue out = JS_NewArray(ctx);
    runtime::TileArrays t;
    if (!runtime::tileArrays(&t)) return out;
    if (x0 < 0) x0 = 0;
    if (y0 < 0) y0 = 0;
    if (x1 >= t.width) x1 = t.width - 1;
    if (y1 >= t.height) y1 = t.height - 1;
    uint32_t n = 0;
    for (int32_t y = y0; y <= y1; ++y) {
        for (int32_t x = x0; x <= x1; ++x) {
            const uint32_t def = t.lookup[static_cast<int64_t>(t.width) * y + x];
            if (!(t.sHeader[def] & runtime::kTileActiveBit)) continue;
            const uint16_t type = t.type[def];
            if (!marks[type].load(std::memory_order_relaxed)) continue;
            JS_SetPropertyUint32(ctx, out, n++, JS_NewInt32(ctx, x));
            JS_SetPropertyUint32(ctx, out, n++, JS_NewInt32(ctx, y));
            if (frames) {
                JS_SetPropertyUint32(ctx, out, n++, JS_NewInt32(ctx, t.frameX[def]));
                JS_SetPropertyUint32(ctx, out, n++, JS_NewInt32(ctx, t.frameY[def]));
            }
        }
    }
    return out;
}
/**
 * bl.tiles.setWaterStyleCount(total) -> bool: os lacos do Main.DrawWaters vao
 * ate `total` estilos de agua (ModWater.h). As tabelas por estilo crescem antes.
 */
JSValue js_setWaterStyleCount(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t total = 0;
    if (argc >= 1) JS_ToInt32(ctx, &total, argv[0]);
    return JS_NewBool(ctx, runtime::setWaterStyleCount(total));
}
} // namespace

void installTilesApi(JSContext* ctx, JSValue bl) {
    JSValue tiles = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, tiles, "register", JS_NewCFunction(ctx, js_register, "register", 1));
    JS_SetPropertyStr(ctx, tiles, "isModTile", JS_NewCFunction(ctx, js_isModTile, "isModTile", 1));
    JS_SetPropertyStr(ctx, tiles, "typeAt", JS_NewCFunction(ctx, js_typeAt, "typeAt", 2));
    JS_SetPropertyStr(ctx, tiles, "mapTileAt", JS_NewCFunction(ctx, js_mapTileAt, "mapTileAt", 2));
    JS_SetPropertyStr(ctx, tiles, "addMapEntry", JS_NewCFunction(ctx, js_addMapEntry, "addMapEntry", 5));
    JS_SetPropertyStr(ctx, tiles, "vanillaCount", JS_NewInt32(ctx, runtime::kVanillaTileCount));
    JS_SetPropertyStr(ctx, tiles, "typeOf", JS_NewCFunction(ctx, js_typeOf, "typeOf", 1));
    JS_SetPropertyStr(ctx, tiles, "setAnimationFrameHeight",
                      JS_NewCFunction(ctx, js_setAnimationFrameHeight, "setAnimationFrameHeight", 2));
    JS_SetPropertyStr(ctx, tiles, "find", JS_NewCFunction(ctx, js_find, "find", 6));
    JS_SetPropertyStr(ctx, tiles, "addLights", JS_NewCFunction(ctx, js_addLights, "addLights", 1));
    JS_SetPropertyStr(ctx, tiles, "setDoor", JS_NewCFunction(ctx, js_setDoor, "setDoor", 2));
    JS_SetPropertyStr(ctx, tiles, "setWaterStyleCount",
                      JS_NewCFunction(ctx, js_setWaterStyleCount, "setWaterStyleCount", 1));
    JS_SetPropertyStr(ctx, tiles, "onDrawData", JS_NewCFunction(ctx, js_onDrawData, "onDrawData", 1));
    JS_SetPropertyStr(ctx, bl, "tiles", tiles);
}

} // namespace bl::script
#endif
