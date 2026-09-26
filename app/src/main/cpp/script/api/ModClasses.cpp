#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "quickjs.h"
#include "content/common/ModContent.h"
#include "script/api/Texture.h"
#include "script/js/ModClassesJs.h"   // gerado: CMakeLists, de script/js/ModClasses.js
#include "script/js/ModHelpersJs.h"   // gerado: CMakeLists, de script/js/ModHelpers.js

#include <cstdio>
#include <string>
#include <vector>

namespace bl::script {

namespace {

/**
 * bl.readJson(caminho) — um JSON da pasta do mod de quem chama, ou undefined
 * se o arquivo nao existe. JSON quebrado lanca, com o nome do arquivo: um
 * pt-BR.json com virgula sobrando nao pode virar "o item ficou sem nome".
 */
JSValue js_readJson(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* rel = argc >= 1 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!rel) return JS_ThrowTypeError(ctx, "bl.readJson(caminho)");
    const std::string path = resolveModPath(ctx, rel);
    JS_FreeCString(ctx, rel);

    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return JS_UNDEFINED;
    std::string text;
    char buf[4096];
    size_t n;
    while ((n = std::fread(buf, 1, sizeof(buf), f)) > 0) text.append(buf, n);
    std::fclose(f);
    return JS_ParseJSON(ctx, text.c_str(), text.size(), path.c_str());
}

// bl.onContentReady(fn): as funcoes, na ordem. Chamadas uma vez, quando o
// conteudo de mod esta todo no jogo (content/common/ModContent.h).
JSContext* g_ctx = nullptr;
std::vector<JSValue> g_ready;

void onContentReady() {
    if (!g_ctx || g_ready.empty()) return;
    JsLock lock;
    for (JSValue fn : g_ready) {
        JSValue r = JS_Call(g_ctx, fn, JS_UNDEFINED, 0, nullptr);
        if (JS_IsException(r)) {
            JSValue e = JS_GetException(g_ctx);
            const char* t = JS_ToCString(g_ctx, e);
            BL_ERROR("conteudo de mod: onContentReady lancou: %s", t ? t : "?");
            if (t) JS_FreeCString(g_ctx, t);
            JS_FreeValue(g_ctx, e);
        }
        JS_FreeValue(g_ctx, r);
    }
}

/**
 * bl.onContentReady(fn) — fn() uma vez, com os itens, projeteis e NPCs de mod
 * no jogo, as receitas do jogo montadas e o Bestiario criado. E onde entram
 * as receitas e as entradas do Bestiario (o PostSetupContent do tModLoader).
 */
JSValue js_onContentReady(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsFunction(ctx, argv[0])) return JS_ThrowTypeError(ctx, "bl.onContentReady(funcao)");
    g_ctx = ctx;
    g_ready.push_back(JS_DupValue(ctx, argv[0]));
    runtime::setContentReadyHook(onContentReady);
    return JS_UNDEFINED;
}


} // namespace

void installModClasses(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSValue global = JS_GetGlobalObject(ctx);
    JSValue bl = JS_GetPropertyStr(ctx, global, "bl");
    JS_SetPropertyStr(ctx, bl, "readJson", JS_NewCFunction(ctx, js_readJson, "readJson", 1));
    JS_SetPropertyStr(ctx, bl, "onContentReady",
                      JS_NewCFunction(ctx, js_onContentReady, "onContentReady", 1));
    JS_FreeValue(ctx, bl);
    JS_FreeValue(ctx, global);

    // Os ajudantes primeiro: as classes e os mods podem usar Vector2, Rand...
    JSValue h = JS_Eval(ctx, kModHelpersJs, sizeof(kModHelpersJs) - 1, "bunny:ModHelpers.js",
                        JS_EVAL_TYPE_GLOBAL);
    if (JS_IsException(h)) {
        JSValue e = JS_GetException(ctx);
        const char* t = JS_ToCString(ctx, e);
        BL_ERROR("ajudantes dos mods (Vector2, Rand...) nao carregaram: %s", t ? t : "?");
        if (t) JS_FreeCString(ctx, t);
        JS_FreeValue(ctx, e);
    }
    JS_FreeValue(ctx, h);

    JSValue r = JS_Eval(ctx, kModClassesJs, sizeof(kModClassesJs) - 1, "bunny:ModClasses.js",
                        JS_EVAL_TYPE_GLOBAL);
    if (JS_IsException(r)) {
        JSValue e = JS_GetException(ctx);
        const char* t = JS_ToCString(ctx, e);
        BL_ERROR("classes dos mods (ModItem...) nao carregaram: %s", t ? t : "?");
        if (t) JS_FreeCString(ctx, t);
        JS_FreeValue(ctx, e);
    } else {
        BL_INFO("classes dos mods instaladas (ModItem, ModProjectile, ModNPC)");
    }
    JS_FreeValue(ctx, r);
}

} // namespace bl::script

#else
namespace bl::script {
void installModClasses(void*) {}
} // namespace bl::script
#endif
