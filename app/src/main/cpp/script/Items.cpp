#include "script/Items.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "mods/ModLoader.h"
#include "runtime/ModItems.h"
#include "script/Bridge.h"
#include "script/Texture.h"

#include <cstdio>
#include <map>
#include <string>

namespace bl::script {

namespace {

// tipo -> a definicao que o mod passou (com o setDefaults dele). Vive o
// runtime inteiro: o jogo cria item de mod a qualquer hora.
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

/** Mensagem da excecao pendente, para o log do mod. */
std::string exceptionText(JSContext* ctx) {
    JSValue e = JS_GetException(ctx);
    const char* t = JS_ToCString(ctx, e);
    std::string s = t ? t : "?";
    if (t) JS_FreeCString(ctx, t);
    JS_FreeValue(ctx, e);
    return s;
}

/**
 * O hook de Item.SetDefaults para os itens de mod — um so, para todos.
 *
 * Para tipo do jogo, repassa. Para tipo de mod, NAO chama o original: o
 * SetDefaults do jogo zera o tipo de quem passa de ItemID.Count, e o item
 * nasceria como "nada". Faz o que ele faria (ResetStats, tipo) e entrega o
 * item ao setDefaults do mod.
 *
 * Erro no setDefaults do mod fica AQUI: se escapasse, o despachante do hook
 * chamaria o original como protecao — e o original zeraria o tipo.
 */
JSValue js_modSetDefaults(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t type = 0;
    if (argc >= 3) JS_ToInt32(ctx, &type, argv[2]);
    auto it = g_defs.find(type);
    if (it == g_defs.end() || argc < 2) {
        return JS_Call(ctx, argv[0], JS_UNDEFINED, argc - 1, argv + 1);
    }
    Il2CppObject* self = objectFromJS(argv[1]);
    if (!self) return JS_UNDEFINED;

    runtime::prepareModItem(self, type);
    JSValue fn = JS_GetPropertyStr(ctx, it->second, "setDefaults");
    if (JS_IsFunction(ctx, fn)) {
        JSValue r = JS_Call(ctx, fn, it->second, 1, &argv[1]);
        if (JS_IsException(r)) {
            const std::string name = stringProp(ctx, it->second, "name");
            BL_ERROR("item de mod %s: setDefaults lancou: %s", name.c_str(), exceptionText(ctx).c_str());
        }
        JS_FreeValue(ctx, r);
    }
    JS_FreeValue(ctx, fn);
    runtime::finishModItem(self, type);
    return JS_UNDEFINED;
}

bool installSetDefaultsHook(JSContext* ctx) {
    if (g_hook) return true;
    Il2CppClass* item = il2cpp::findClass({"Terraria", "Item", {}});
    const MethodInfo* m = item ? il2cpp::findMethodBySignature(
        item, il2cpp::parseSignature("void SetDefaults(int Type, ItemVariant variant)")) : nullptr;
    if (!m) {
        JS_ThrowInternalError(ctx, "bl.items: Item.SetDefaults nao encontrado nesta versao");
        return false;
    }
    JSValue cb = JS_NewCFunction(ctx, js_modSetDefaults, "modSetDefaults", 4);
    g_hook = installJsHook(ctx, m, 2, true, cb);
    JS_FreeValue(ctx, cb);   // o hook guarda a copia dele
    return g_hook;
}

/** A categoria do mod: o nome do manifesto, o icon.png da raiz (se houver). */
int modCategoryFor(const std::string& mod, const std::string& fallbackIcon) {
    const std::string root = mods::rootOf(mod);
    std::string icon = root.empty() ? std::string() : root + "/icon.png";
    if (icon.empty() || !fileExists(icon)) icon = fallbackIcon;
    return runtime::modCategory(mod, mods::displayName(mod), icon);
}

/**
 * bl.items.register({ name, texture, displayName, setDefaults }) -> tipo
 *
 * O tipo sai na hora e e o mesmo a cada boot com os mesmos mods (ItemID.Count
 * + a ordem de registro): da para usar em receita, hook e comparacao desde o
 * topo do arquivo. O jogo passa a conhecer o item logo depois, na tela de
 * titulo — antes disso, dar o item nao funciona.
 */
JSValue js_register(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsObject(argv[0])) {
        return JS_ThrowTypeError(
            ctx, "bl.items.register({ name, texture, displayName, setDefaults })");
    }
    JSValueConst def = argv[0];
    runtime::ModItemDef d;
    d.mod = callerModId(ctx);
    d.name = stringProp(ctx, def, "name");
    const std::string texture = stringProp(ctx, def, "texture");
    if (d.name.empty()) return JS_ThrowTypeError(ctx, "bl.items.register: falta `name`");
    if (texture.empty()) {
        return JS_ThrowTypeError(ctx, "bl.items.register(%s): falta `texture`", d.name.c_str());
    }
    d.texture = resolveModPath(ctx, texture);
    if (!fileExists(d.texture)) {
        return JS_ThrowReferenceError(ctx, "bl.items.register(%s): textura nao existe: %s",
                                      d.name.c_str(), d.texture.c_str());
    }

    // displayName: texto, ou { 'pt-BR': ..., 'en-US': ... }.
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
    const std::string mod = d.mod, name = d.name, icon = d.texture;
    const int type = runtime::registerModItem(std::move(d));
    if (type < 0) {
        return JS_ThrowRangeError(ctx, "bl.items.register: '%s' ja foi registrado por este mod",
                                  name.c_str());
    }
    g_defs[type] = JS_DupValue(ctx, def);
    runtime::addToModCategory(modCategoryFor(mod, icon), type);
    BL_INFO("item de mod %s/%s -> tipo %d", mod.c_str(), name.c_str(), type);
    return JS_NewInt32(ctx, type);
}

/** bl.items.isModItem(tipo) */
JSValue js_isModItem(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t t = -1;
    if (argc >= 1) JS_ToInt32(ctx, &t, argv[0]);
    return JS_NewBool(ctx, runtime::isModItem(t));
}

/**
 * bl.menu.itemCategory(nome, icone?) -> categoria
 *
 * Uma categoria a mais no catalogo do menu, alem da que o mod ja ganha com o
 * nome dele. O icone e um PNG do mod; sem ele, a categoria usa o do primeiro
 * item posto nela.
 */
JSValue js_itemCategory(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* name = argc >= 1 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!name) return JS_ThrowTypeError(ctx, "bl.menu.itemCategory(nome, icone?)");
    std::string icon;
    if (argc >= 2 && JS_IsString(argv[1])) {
        const char* c = JS_ToCString(ctx, argv[1]);
        if (c) { icon = resolveModPath(ctx, c); JS_FreeCString(ctx, c); }
    }
    const int cat = runtime::modCategory(callerModId(ctx), name, icon);
    JS_FreeCString(ctx, name);
    return JS_NewInt32(ctx, cat);
}

/** bl.menu.addItem(categoria, tipo) */
JSValue js_addItem(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t cat = -1, t = -1;
    if (argc >= 2) { JS_ToInt32(ctx, &cat, argv[0]); JS_ToInt32(ctx, &t, argv[1]); }
    if (!runtime::addToModCategory(cat, t)) {
        return JS_ThrowRangeError(ctx, "bl.menu.addItem: categoria %d nao existe", cat);
    }
    return JS_UNDEFINED;
}

} // namespace

void installItemsApi(JSContext* ctx, JSValue bl) {
    JSValue items = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, items, "register", JS_NewCFunction(ctx, js_register, "register", 1));
    JS_SetPropertyStr(ctx, items, "isModItem", JS_NewCFunction(ctx, js_isModItem, "isModItem", 1));
    JS_SetPropertyStr(ctx, bl, "items", items);

    JSValue menu = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, menu, "itemCategory",
                      JS_NewCFunction(ctx, js_itemCategory, "itemCategory", 2));
    JS_SetPropertyStr(ctx, menu, "addItem", JS_NewCFunction(ctx, js_addItem, "addItem", 2));
    JS_SetPropertyStr(ctx, bl, "menu", menu);
}

} // namespace bl::script
#endif
