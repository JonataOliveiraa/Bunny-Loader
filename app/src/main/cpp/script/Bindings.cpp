#include "script/ScriptEngine.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#endif

// Ponte JavaScript <-> IL2CPP. Espelha a API do TL Pro:
//
//   const Player = new NativeClass("Terraria", "Player");
//   Player.defaultItemGrabRange = 10;
//   const M = Player["float GetClosestRollLuck(int x, int y, int range)"];
//   M.hook((original, self, ...args) => { original(self, ...args); });
//
// TODO(Fase 4): implementar, nesta ordem —
//   1. tl.log            (prova que o motor roda dentro do jogo)
//   2. NativeClass       (resolve Il2CppClass*, expõe campos estáticos)
//   3. NativeMethod      (chamada direta, assinatura por string)
//   4. NativeObject      (instâncias, campos de instância, wrap/unwrap)
//   5. NativeMethod.hook (trampolim nativo -> callback JS -> original)
//   6. NativeArray       (length, índice, bounds-check)
//
// Regras:
//  - Nada resolvido por nome no caminho quente: cachear no objeto JS.
//  - Referência do jogo guardada em variável JS de vida longa => gchandle_new.
//  - Exceção no callback do mod é logada e NÃO propaga para o jogo.

namespace bl::script {

#if BL_HAVE_QUICKJS

namespace {

JSValue js_tl_log(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    for (int i = 0; i < argc; ++i) {
        const char* text = JS_ToCString(ctx, argv[i]);
        BL_INFO("[mod] %s", text ? text : "undefined");
        if (text) JS_FreeCString(ctx, text);
    }
    return JS_UNDEFINED;
}

} // namespace

void installBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSValue global = JS_GetGlobalObject(ctx);

    // tl.log — primeiro tijolo da API.
    JSValue tl = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, tl, "log", JS_NewCFunction(ctx, js_tl_log, "log", 1));
    JS_SetPropertyStr(ctx, global, "tl", tl);

    // TODO(Fase 4): NativeClass, NativeObject, NativeMethod, NativeArray.

    JS_FreeValue(ctx, global);
    BL_INFO("bindings instalados (tl.log)");
}

#else

void installBindings(void*) {}

#endif

} // namespace bl::script
