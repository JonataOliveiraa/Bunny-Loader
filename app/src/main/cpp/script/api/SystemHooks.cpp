#include "script/api/SystemHooks.h"
#include "script/bridge/ScriptEngine.h"
#if BL_HAVE_QUICKJS && defined(__aarch64__)
#include "content/systems/SystemStages.h"
#include "core/Log.h"
#include "quickjs.h"
#include <cmath>
#include <cstdint>
#include <cstring>
namespace {
struct JsStage {
    JSContext* context = nullptr;
    JSValue callback = JS_UNDEFINED;
};
JsStage jsStages[bl::runtime::kSystemStageCount];
void dispatchJs(int32_t index, double* value) {
    if (index < 0 || index >= bl::runtime::kSystemStageCount) return;
    bl::script::JsLock lock;
    JsStage& stage = jsStages[index];
    if (!stage.context) return;
    JSValue result = JS_Call(stage.context, stage.callback, JS_UNDEFINED, 0, nullptr);
    if (JS_IsException(result)) {
        JSValue exception = JS_GetException(stage.context);
        const char* message = JS_ToCString(stage.context, exception);
        BL_ERROR("ModSystem.%s: %s", bl::runtime::systemStageName(index), message ? message : "erro no callback");
        if (message) JS_FreeCString(stage.context, message);
        JS_FreeValue(stage.context, exception);
    } else if (value) {
        double number;
        if (!JS_ToFloat64(stage.context, &number, result)) {
            if (std::isfinite(number) && number >= 0) *value = number;
        } else {
            JSValue exception = JS_GetException(stage.context);
            JS_FreeValue(stage.context, exception);
        }
    }
    JS_FreeValue(stage.context, result);
}
JSValue installSystemStage(JSContext* context, JSValueConst, int argc, JSValueConst* argv) {
    if (argc != 2 || !JS_IsString(argv[0]) || !JS_IsFunction(context, argv[1])) return JS_ThrowTypeError(context, "installSystemStage(nome, funcao)");
    const char* name = JS_ToCString(context, argv[0]);
    if (!name) return JS_EXCEPTION;
    const int32_t index = bl::runtime::systemStageIndex(name);
    JS_FreeCString(context, name);
    if (index < 0) return JS_ThrowTypeError(context, "ModSystem: etapa desconhecida");
    JsStage& selected = jsStages[index];
    if (selected.context) return JS_ThrowTypeError(context, "%s ja instalado", bl::runtime::systemStageName(index));
    if (!bl::runtime::installSystemStage(index, bl::runtime::kStageClientJs, dispatchJs)) {
        return JS_ThrowInternalError(context, "%s: corpo nativo incompativel", bl::runtime::systemStageName(index));
    }
    selected.callback = JS_DupValue(context, argv[1]);
    selected.context = context;
    return JS_UNDEFINED;
}
}
namespace bl::script {
void installSystemBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSValue global = JS_GetGlobalObject(ctx);
    JSValue api = JS_GetPropertyStr(ctx, global, "bl");
    JS_SetPropertyStr(ctx, api, "installSystemStage", JS_NewCFunction(ctx, installSystemStage, "installSystemStage", 2));
    JS_FreeValue(ctx, api);
    JS_FreeValue(ctx, global);
}
void releaseSystemBindings(void* context) {
    for (JsStage& stage : jsStages) {
        if (stage.context != context) continue;
        JS_FreeValue(stage.context, stage.callback);
        stage.callback = JS_UNDEFINED;
        stage.context = nullptr;
    }
}
}
#else
namespace bl::script {
void installSystemBindings(void*) {}
void releaseSystemBindings(void*) {}
}
#endif
