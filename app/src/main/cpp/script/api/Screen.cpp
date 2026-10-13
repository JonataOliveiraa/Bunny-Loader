#include "script/api/Screen.h"

#if BL_HAVE_QUICKJS
#include "graphics/GlHooks.h"
#include "graphics/RenderHook.h"
#include "graphics/ShaderRegistry.h"

namespace bl::script {

namespace {

double number(JSContext* ctx, JSValueConst obj, const char* name, double fallback) {
    JSValue v = JS_GetPropertyStr(ctx, obj, name);
    double out = fallback;
    if (!JS_IsUndefined(v) && !JS_IsNull(v) && JS_ToFloat64(ctx, &out, v) < 0) out = fallback;
    JS_FreeValue(ctx, v);
    return out;
}

// Um numero ou um array de ate `max` numeros em `out`; quantos leu (0 se nada).
int numbers(JSContext* ctx, JSValueConst v, float* out, int max) {
    if (JS_IsNumber(v)) {
        double d = 0;
        JS_ToFloat64(ctx, &d, v);
        out[0] = static_cast<float>(d);
        return 1;
    }
    if (!JS_IsArray(v)) return 0;
    int count = 0;
    for (uint32_t i = 0; i < static_cast<uint32_t>(max); ++i) {
        JSValue item = JS_GetPropertyUint32(ctx, v, i);
        if (JS_IsUndefined(item)) break;
        double d = 0;
        JS_ToFloat64(ctx, &d, item);
        JS_FreeValue(ctx, item);
        out[count++] = static_cast<float>(d);
    }
    return count;
}

void readArray(JSContext* ctx, JSValueConst obj, const char* name, float* out, int max) {
    JSValue v = JS_GetPropertyStr(ctx, obj, name);
    numbers(ctx, v, out, max);
    JS_FreeValue(ctx, v);
}

void readUniforms(JSContext* ctx, JSValueConst obj, graphics::EffectDraw& draw) {
    JSValue map = JS_GetPropertyStr(ctx, obj, "uniforms");
    JSPropertyEnum* props = nullptr;
    uint32_t count = 0;
    if (JS_IsObject(map) && JS_GetOwnPropertyNames(ctx, &props, &count, map, JS_GPN_STRING_MASK | JS_GPN_ENUM_ONLY) == 0) {
        for (uint32_t i = 0; i < count; ++i) {
            const char* name = JS_AtomToCString(ctx, props[i].atom);
            JSValue v = JS_GetProperty(ctx, map, props[i].atom);
            graphics::UniformValue u;
            u.count = numbers(ctx, v, u.v, 4);
            if (name && u.count > 0) {
                u.name = name;
                draw.uniforms.push_back(std::move(u));
            }
            JS_FreeValue(ctx, v);
            JS_FreeCString(ctx, name);
            JS_FreeAtom(ctx, props[i].atom);
        }
        js_free(ctx, props);
    }
    JS_FreeValue(ctx, map);
}

// bl.screen.shader(nome, codigo) -> id. O GLSL compila na thread de render.
JSValue screen_shader(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 2 || !JS_IsString(argv[0]) || !JS_IsString(argv[1])) {
        return JS_ThrowTypeError(ctx, "bl.screen.shader(name, source)");
    }
    const char* name = JS_ToCString(ctx, argv[0]);
    const char* source = JS_ToCString(ctx, argv[1]);
    const int id = graphics::ShaderRegistry::Register(name ? name : "", source ? source : "");
    JS_FreeCString(ctx, name);
    JS_FreeCString(ctx, source);
    return JS_NewInt32(ctx, id);
}

// bl.screen.shaderStatus(id) -> { state: 'pending' | 'ready' | 'failed', error }
JSValue screen_shaderStatus(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t id = 0;
    if (argc > 0) JS_ToInt32(ctx, &id, argv[0]);
    std::string error;
    const graphics::ShaderState state = graphics::ShaderRegistry::Status(id, &error);
    JSValue o = JS_NewObject(ctx);
    const char* text = state == graphics::ShaderState::Ready ? "ready"
                     : state == graphics::ShaderState::Failed ? "failed" : "pending";
    JS_SetPropertyStr(ctx, o, "state", JS_NewString(ctx, text));
    JS_SetPropertyStr(ctx, o, "error", JS_NewString(ctx, error.c_str()));
    return o;
}

// bl.screen.submit([{shader, layer, box, center, radius, intensity, progress,
// age, color, uniforms}]): os efeitos do quadro.
JSValue screen_submit(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    graphics::EffectList list;
    if (argc > 0 && JS_IsArray(argv[0])) {
        JSValue lengthValue = JS_GetPropertyStr(ctx, argv[0], "length");
        uint32_t length = 0;
        JS_ToUint32(ctx, &length, lengthValue);
        JS_FreeValue(ctx, lengthValue);
        list.reserve(length);
        for (uint32_t i = 0; i < length; ++i) {
            JSValue item = JS_GetPropertyUint32(ctx, argv[0], i);
            graphics::EffectDraw d;
            d.shader = static_cast<int>(number(ctx, item, "shader", 0));
            d.layer = number(ctx, item, "layer", 0) >= 1 ? graphics::EffectLayer::Screen : graphics::EffectLayer::World;
            readArray(ctx, item, "box", d.box, 4);
            readArray(ctx, item, "center", d.center, 2);
            d.radius = static_cast<float>(number(ctx, item, "radius", 0));
            d.intensity = static_cast<float>(number(ctx, item, "intensity", 1));
            d.progress = static_cast<float>(number(ctx, item, "progress", 0));
            d.age = static_cast<float>(number(ctx, item, "age", 0));
            readArray(ctx, item, "color", d.color, 4);
            readUniforms(ctx, item, d);
            JS_FreeValue(ctx, item);
            if (d.shader > 0) list.push_back(std::move(d));
        }
    }
    graphics::submitEffects(std::move(list));
    return JS_UNDEFINED;
}

JSValue screen_stats(JSContext* ctx, JSValueConst, int, JSValueConst*) {
    const graphics::RenderStats s = graphics::renderStats();
    JSValue o = JS_NewObject(ctx);
    auto i64 = [&](const char* name, int64_t v) { JS_SetPropertyStr(ctx, o, name, JS_NewInt64(ctx, v)); };
    auto flag = [&](const char* name, bool v) { JS_SetPropertyStr(ctx, o, name, JS_NewBool(ctx, v)); };
    i64("worldPostRender", s.worldPostRender);
    i64("uiPostRender", s.uiPostRender);
    i64("otherPostRender", s.otherPostRender);
    i64("draws", s.draws);
    i64("frames", s.frames);
    i64("markersIssued", s.markersIssued);
    i64("markerHits", s.markerHits);
    i64("screenFrames", s.screenFrames);
    i64("renderCpuNs", s.renderCpuNs);
    i64("markerCpuNs", s.markerCpuNs);
    i64("gpuNs", s.gpuNs);
    i64("gpuSamples", s.gpuSamples);
    i64("gpuFrameNs", s.gpuFrameNs);
    i64("gpuFrameSamples", s.gpuFrameSamples);
    i64("shadowChecks", s.shadowChecks);
    i64("shadowMismatches", s.shadowMismatches);
    i64("boxPixels", s.boxPixels);
    i64("drawFbo", s.drawFbo);
    i64("samples", s.samples);
    i64("blitError", s.blitError);
    i64("copyFallbacks", s.copyFallbacks);
    JSValue vp = JS_NewArray(ctx);
    for (uint32_t i = 0; i < 4; ++i) JS_SetPropertyUint32(ctx, vp, i, JS_NewInt32(ctx, s.viewport[i]));
    JS_SetPropertyStr(ctx, o, "viewport", vp);
    flag("hookInstalled", s.hookInstalled);
    flag("hasContext", s.hasContext);
    flag("markerReady", s.markerReady);
    flag("gpuTimer", s.gpuTimer);
    return o;
}

JSValue screen_trace(JSContext*, JSValueConst, int, JSValueConst*) {
    graphics::requestGlTrace();
    return JS_UNDEFINED;
}

} // namespace

void installScreenApi(JSContext* ctx, JSValue bl) {
    static const JSCFunctionListEntry fns[] = {
        JS_CFUNC_DEF("shader", 2, screen_shader),
        JS_CFUNC_DEF("shaderStatus", 1, screen_shaderStatus),
        JS_CFUNC_DEF("submit", 1, screen_submit),
        JS_CFUNC_DEF("__stats", 0, screen_stats),
        JS_CFUNC_DEF("__trace", 0, screen_trace),
    };
    JSValue screen = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, screen, fns, sizeof fns / sizeof fns[0]);
    JS_SetPropertyStr(ctx, bl, "screen", screen);
}

} // namespace bl::script
#endif
