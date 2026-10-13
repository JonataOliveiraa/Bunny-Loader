#include "script/api/Screen.h"

#if BL_HAVE_QUICKJS
#include "graphics/GlHooks.h"
#include "graphics/RenderHook.h"

namespace bl::script {

namespace {

double number(JSContext* ctx, JSValueConst value, double fallback) {
    double out = fallback;
    if (JS_IsUndefined(value) || JS_ToFloat64(ctx, &out, value) < 0) return fallback;
    return out;
}

JSValue screen_poc(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    graphics::PocParams p;
    p.enabled = argc > 0 && JS_ToBool(ctx, argv[0]) > 0;
    p.u = static_cast<float>(number(ctx, argc > 1 ? argv[1] : JS_UNDEFINED, p.u));
    p.v = static_cast<float>(number(ctx, argc > 2 ? argv[2] : JS_UNDEFINED, p.v));
    p.radius = static_cast<float>(number(ctx, argc > 3 ? argv[3] : JS_UNDEFINED, p.radius));
    p.strength = static_cast<float>(number(ctx, argc > 4 ? argv[4] : JS_UNDEFINED, p.strength));
    graphics::setPoc(p);
    return JS_UNDEFINED;
}

JSValue screen_stats(JSContext* ctx, JSValueConst, int, JSValueConst*) {
    const graphics::RenderStats s = graphics::renderStats();
    JSValue o = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, o, "worldPostRender", JS_NewInt64(ctx, s.worldPostRender));
    JS_SetPropertyStr(ctx, o, "uiPostRender", JS_NewInt64(ctx, s.uiPostRender));
    JS_SetPropertyStr(ctx, o, "otherPostRender", JS_NewInt64(ctx, s.otherPostRender));
    JS_SetPropertyStr(ctx, o, "draws", JS_NewInt64(ctx, s.draws));
    JS_SetPropertyStr(ctx, o, "markersIssued", JS_NewInt64(ctx, s.markersIssued));
    JS_SetPropertyStr(ctx, o, "markerHits", JS_NewInt64(ctx, s.markerHits));
    JS_SetPropertyStr(ctx, o, "swapDraws", JS_NewInt64(ctx, s.swapDraws));
    JS_SetPropertyStr(ctx, o, "markerReady", JS_NewBool(ctx, s.markerReady));
    JS_SetPropertyStr(ctx, o, "renderCpuNs", JS_NewInt64(ctx, s.renderCpuNs));
    JS_SetPropertyStr(ctx, o, "markerCpuNs", JS_NewInt64(ctx, s.markerCpuNs));
    JS_SetPropertyStr(ctx, o, "gpuNs", JS_NewInt64(ctx, s.gpuNs));
    JS_SetPropertyStr(ctx, o, "gpuSamples", JS_NewInt64(ctx, s.gpuSamples));
    JS_SetPropertyStr(ctx, o, "gpuTimer", JS_NewBool(ctx, s.gpuTimer));
    JS_SetPropertyStr(ctx, o, "gpuFrameNs", JS_NewInt64(ctx, s.gpuFrameNs));
    JS_SetPropertyStr(ctx, o, "gpuFrameSamples", JS_NewInt64(ctx, s.gpuFrameSamples));
    JS_SetPropertyStr(ctx, o, "shadowChecks", JS_NewInt64(ctx, s.shadowChecks));
    JS_SetPropertyStr(ctx, o, "shadowMismatches", JS_NewInt64(ctx, s.shadowMismatches));
    JS_SetPropertyStr(ctx, o, "boxPixels", JS_NewInt64(ctx, s.boxPixels));
    JS_SetPropertyStr(ctx, o, "drawFbo", JS_NewInt32(ctx, s.drawFbo));
    JS_SetPropertyStr(ctx, o, "readFbo", JS_NewInt32(ctx, s.readFbo));
    JSValue vp = JS_NewArray(ctx);
    for (uint32_t i = 0; i < 4; ++i) JS_SetPropertyUint32(ctx, vp, i, JS_NewInt32(ctx, s.viewport[i]));
    JS_SetPropertyStr(ctx, o, "viewport", vp);
    JS_SetPropertyStr(ctx, o, "samples", JS_NewInt32(ctx, s.samples));
    JS_SetPropertyStr(ctx, o, "blitError", JS_NewInt32(ctx, s.blitError));
    JS_SetPropertyStr(ctx, o, "copyFallbacks", JS_NewInt32(ctx, s.copyFallbacks));
    JS_SetPropertyStr(ctx, o, "hookInstalled", JS_NewBool(ctx, s.hookInstalled));
    JS_SetPropertyStr(ctx, o, "hasContext", JS_NewBool(ctx, s.hasContext));
    return o;
}

JSValue screen_trace(JSContext*, JSValueConst, int, JSValueConst*) {
    graphics::requestGlTrace();
    return JS_UNDEFINED;
}

} // namespace

void installScreenApi(JSContext* ctx, JSValue bl) {
    static const JSCFunctionListEntry fns[] = {
        JS_CFUNC_DEF("__poc", 5, screen_poc),
        JS_CFUNC_DEF("__stats", 0, screen_stats),
        JS_CFUNC_DEF("__trace", 0, screen_trace),
    };
    JSValue screen = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, screen, fns, sizeof fns / sizeof fns[0]);
    JS_SetPropertyStr(ctx, bl, "screen", screen);
}

} // namespace bl::script
#endif
