#include "script/api/Walls.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "content/tiles/TileAccess.h"
#include "content/walls/ModWalls.h"
#include "content/tiles/ModTileMap.h"
#include "script/bridge/Bridge.h"
#include "script/api/Items.h"
#include "script/api/Texture.h"

#include <atomic>
#include <cstdio>
#include <map>
#include <string>

namespace bl::script {

namespace {

std::map<int, JSValue> g_defs;
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

void onWallsInstalled(int first, int last) {
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
                BL_ERROR("parede de mod %s: setStaticDefaults lancou: %s", name.c_str(),
                         exceptionText(g_ctx).c_str());
            }
            JS_FreeValue(g_ctx, r);
        }
        JS_FreeValue(g_ctx, fn);
    }
}

JSValue js_register(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsObject(argv[0])) {
        return JS_ThrowTypeError(ctx, "bl.walls.register({ name, texture, setStaticDefaults })");
    }
    JSValueConst def = argv[0];
    runtime::ModWallDef d;
    d.mod = callerModId(ctx);
    d.name = stringProp(ctx, def, "name");
    const std::string texture = stringProp(ctx, def, "texture");
    if (d.name.empty()) return JS_ThrowTypeError(ctx, "bl.walls.register: falta `name`");
    if (texture.empty()) return JS_ThrowTypeError(ctx, "bl.walls.register(%s): falta `texture`", d.name.c_str());
    d.texture = resolveModPath(ctx, texture);
    if (!fileExists(d.texture)) {
        return JS_ThrowReferenceError(ctx, "bl.walls.register(%s): textura nao existe: %s",
                                      d.name.c_str(), d.texture.c_str());
    }

    const std::string mod = d.mod, name = d.name;
    const int type = runtime::registerModWall(std::move(d));
    if (type < 0) {
        return JS_ThrowRangeError(ctx, "bl.walls.register: '%s' ja foi registrada por este mod", name.c_str());
    }
    g_defs[type] = JS_DupValue(ctx, def);
    g_ctx = ctx;
    runtime::setWallsInstalledHook(onWallsInstalled);
    BL_DEBUG("parede de mod %s/%s -> tipo %d", mod.c_str(), name.c_str(), type);
    return JS_NewInt32(ctx, type);
}

JSValue js_typeOf(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* name = argc >= 1 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!name) return JS_ThrowTypeError(ctx, "bl.walls.typeOf(nome)");
    const int type = runtime::modWallTypeByName(callerModId(ctx), name);
    JS_FreeCString(ctx, name);
    return JS_NewInt32(ctx, type);
}

JSValue js_isModWall(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t t = -1;
    if (argc >= 1) JS_ToInt32(ctx, &t, argv[0]);
    return JS_NewBool(ctx, runtime::isModWall(t));
}

JSValue js_typeAt(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t x = -1, y = -1;
    if (argc >= 2) {
        JS_ToInt32(ctx, &x, argv[0]);
        JS_ToInt32(ctx, &y, argv[1]);
    }
    return JS_NewInt32(ctx, runtime::wallTypeAt(x, y));
}

JSValue js_addMapEntry(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t v[4] = {-1, 0, 0, 0};
    for (int i = 0; i < 4 && i < argc; ++i) JS_ToInt32(ctx, &v[i], argv[i]);
    const char* key = argc >= 5 ? JS_ToCString(ctx, argv[4]) : nullptr;
    const int option = runtime::addModWallMapEntry(v[0], v[1], v[2], v[3], key ? key : "");
    if (key) JS_FreeCString(ctx, key);
    return JS_NewInt32(ctx, option);
}

JSValue js_find(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* name = argc >= 5 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!name) return JS_ThrowTypeError(ctx, "bl.walls.find(marcas, x0, y0, x1, y1)");
    const std::atomic<uint8_t>* marks = hookMarks(name);
    JS_FreeCString(ctx, name);
    int32_t x0 = 0, y0 = 0, x1 = -1, y1 = -1;
    JS_ToInt32(ctx, &x0, argv[1]);
    JS_ToInt32(ctx, &y0, argv[2]);
    JS_ToInt32(ctx, &x1, argv[3]);
    JS_ToInt32(ctx, &y1, argv[4]);

    JSValue out = JS_NewArray(ctx);
    runtime::TileArrays t;
    if (!runtime::tileArrays(&t) || !t.wall) return out;
    if (x0 < 0) x0 = 0;
    if (y0 < 0) y0 = 0;
    if (x1 >= t.width) x1 = t.width - 1;
    if (y1 >= t.height) y1 = t.height - 1;
    uint32_t n = 0;
    for (int32_t y = y0; y <= y1; ++y) {
        const uint16_t* row = t.wall + static_cast<int64_t>(t.width) * y;
        for (int32_t x = x0; x <= x1; ++x) {
            const uint16_t wall = row[x];
            if (!wall || !marks[wall].load(std::memory_order_relaxed)) continue;
            JS_SetPropertyUint32(ctx, out, n++, JS_NewInt32(ctx, x));
            JS_SetPropertyUint32(ctx, out, n++, JS_NewInt32(ctx, y));
            JS_SetPropertyUint32(ctx, out, n++, JS_NewInt32(ctx, wall));
        }
    }
    return out;
}

} // namespace

void installWallsApi(JSContext* ctx, JSValue bl) {
    JSValue walls = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, walls, "register", JS_NewCFunction(ctx, js_register, "register", 1));
    JS_SetPropertyStr(ctx, walls, "isModWall", JS_NewCFunction(ctx, js_isModWall, "isModWall", 1));
    JS_SetPropertyStr(ctx, walls, "typeOf", JS_NewCFunction(ctx, js_typeOf, "typeOf", 1));
    JS_SetPropertyStr(ctx, walls, "typeAt", JS_NewCFunction(ctx, js_typeAt, "typeAt", 2));
    JS_SetPropertyStr(ctx, walls, "addMapEntry", JS_NewCFunction(ctx, js_addMapEntry, "addMapEntry", 5));
    JS_SetPropertyStr(ctx, walls, "find", JS_NewCFunction(ctx, js_find, "find", 5));
    JS_SetPropertyStr(ctx, walls, "vanillaCount", JS_NewInt32(ctx, runtime::kVanillaWallCount));
    JS_SetPropertyStr(ctx, bl, "walls", walls);
}

} // namespace bl::script
#endif
