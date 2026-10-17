#include "script/api/Screen.h"

#if BL_HAVE_QUICKJS
#include "graphics/GlHooks.h"
#include "graphics/GpuTimer.h"
#include "graphics/RenderHook.h"
#include "graphics/ShaderRegistry.h"
#include "graphics/TextureRegistry.h"
#include "script/api/Texture.h"

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

// Um uniform: numero, array de 1 a 4 ou { components, values } (array de
// vetores: uniform vec4 uAlgo[8]). Ate 256 floats.
bool readUniform(JSContext* ctx, JSValueConst v, graphics::UniformValue& u) {
    if (JS_IsObject(v) && !JS_IsArray(v)) {
        u.components = static_cast<int>(number(ctx, v, "components", 4));
        if (u.components < 1 || u.components > 4) return false;
        JSValue values = JS_GetPropertyStr(ctx, v, "values");
        u.v.resize(256);
        const int n = numbers(ctx, values, u.v.data(), 256);
        JS_FreeValue(ctx, values);
        u.v.resize(static_cast<size_t>(n - n % u.components));
        return !u.v.empty();
    }
    float tmp[4];
    const int n = numbers(ctx, v, tmp, 4);
    if (n <= 0) return false;
    u.components = n;
    u.v.assign(tmp, tmp + n);
    return true;
}

// Um objeto { nome: valor } chamando `fn(nome, valor)` para cada chave.
template <typename Fn>
void forEachKey(JSContext* ctx, JSValueConst obj, const char* field, Fn fn) {
    JSValue map = JS_GetPropertyStr(ctx, obj, field);
    JSPropertyEnum* props = nullptr;
    uint32_t count = 0;
    if (JS_IsObject(map) && JS_GetOwnPropertyNames(ctx, &props, &count, map, JS_GPN_STRING_MASK | JS_GPN_ENUM_ONLY) == 0) {
        for (uint32_t i = 0; i < count; ++i) {
            const char* name = JS_AtomToCString(ctx, props[i].atom);
            JSValue v = JS_GetProperty(ctx, map, props[i].atom);
            if (name) fn(name, v);
            JS_FreeValue(ctx, v);
            JS_FreeCString(ctx, name);
            JS_FreeAtom(ctx, props[i].atom);
        }
        js_free(ctx, props);
    }
    JS_FreeValue(ctx, map);
}

uint32_t lengthOf(JSContext* ctx, JSValueConst array) {
    JSValue v = JS_GetPropertyStr(ctx, array, "length");
    uint32_t length = 0;
    JS_ToUint32(ctx, &length, v);
    JS_FreeValue(ctx, v);
    return length;
}

graphics::EffectDraw readDraw(JSContext* ctx, JSValueConst item) {
    graphics::EffectDraw d;
    d.shader = static_cast<int>(number(ctx, item, "shader", 0));
    d.layer = number(ctx, item, "layer", 0) >= 1 ? graphics::EffectLayer::Screen : graphics::EffectLayer::World;
    d.dest = static_cast<int>(number(ctx, item, "dest", 0));
    d.source = static_cast<int>(number(ctx, item, "source", graphics::kTexNone));
    readArray(ctx, item, "box", d.box, 4);
    readArray(ctx, item, "center", d.center, 2);
    d.radius = static_cast<float>(number(ctx, item, "radius", 0));
    d.intensity = static_cast<float>(number(ctx, item, "intensity", 1));
    d.progress = static_cast<float>(number(ctx, item, "progress", 0));
    d.age = static_cast<float>(number(ctx, item, "age", 0));
    readArray(ctx, item, "color", d.color, 4);
    forEachKey(ctx, item, "uniforms", [&](const char* name, JSValueConst v) {
        graphics::UniformValue u;
        u.name = name;
        if (readUniform(ctx, v, u)) d.uniforms.push_back(std::move(u));
    });
    forEachKey(ctx, item, "textures", [&](const char* name, JSValueConst v) {
        int32_t texture = 0;
        if (JS_ToInt32(ctx, &texture, v) == 0 && texture != graphics::kTexNone) d.textures.push_back({name, texture});
    });
    return d;
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

// bl.screen.submit(passos, alvos, { history }): o quadro. Passo: { shader,
// layer, dest, source, box, center, radius, intensity, progress, age, color,
// uniforms: { nome: valor }, textures: { nome: textura } }; alvo: { id,
// name, scale, persistent }. Texturas: -1 a tela, -2 o quadro anterior, > 0
// um alvo.
JSValue screen_submit(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    graphics::EffectFrame frame;
    if (argc > 0 && JS_IsArray(argv[0])) {
        const uint32_t length = lengthOf(ctx, argv[0]);
        frame.draws.reserve(length);
        for (uint32_t i = 0; i < length; ++i) {
            JSValue item = JS_GetPropertyUint32(ctx, argv[0], i);
            graphics::EffectDraw d = readDraw(ctx, item);
            JS_FreeValue(ctx, item);
            if (d.shader > 0) frame.draws.push_back(std::move(d));
        }
    }
    if (argc > 1 && JS_IsArray(argv[1])) {
        const uint32_t length = lengthOf(ctx, argv[1]);
        for (uint32_t i = 0; i < length; ++i) {
            JSValue item = JS_GetPropertyUint32(ctx, argv[1], i);
            graphics::TargetSpec t;
            t.id = static_cast<int>(number(ctx, item, "id", 0));
            t.scale = static_cast<float>(number(ctx, item, "scale", 1));
            JSValue name = JS_GetPropertyStr(ctx, item, "name");
            const char* text = JS_ToCString(ctx, name);
            t.name = text ? text : "";
            JS_FreeCString(ctx, text);
            JS_FreeValue(ctx, name);
            JSValue persistent = JS_GetPropertyStr(ctx, item, "persistent");
            t.persistent = JS_ToBool(ctx, persistent) > 0;
            JS_FreeValue(ctx, persistent);
            JS_FreeValue(ctx, item);
            if (t.id > 0 && t.scale > 0.f && t.scale <= 1.f) frame.targets.push_back(std::move(t));
        }
    }
    if (argc > 2 && JS_IsObject(argv[2])) {
        JSValue history = JS_GetPropertyStr(ctx, argv[2], "history");
        frame.history = JS_ToBool(ctx, history) > 0;
        JS_FreeValue(ctx, history);
    }
    graphics::submitEffects(std::move(frame));
    return JS_UNDEFINED;
}

// bl.screen.texture(caminho, linear, repetir) -> { id, width, height } ou null.
// Caminho relativo a pasta do mod de quem chama, como o bl.loadTexture.
JSValue screen_texture(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsString(argv[0])) return JS_ThrowTypeError(ctx, "bl.screen.texture(path, linear, repeat)");
    const char* text = JS_ToCString(ctx, argv[0]);
    const std::string path = resolveModPath(ctx, text ? text : "");
    JS_FreeCString(ctx, text);
    const bool linear = argc < 2 || JS_ToBool(ctx, argv[1]) > 0;
    const bool repeat = argc > 2 && JS_ToBool(ctx, argv[2]) > 0;
    int width = 0, height = 0;
    const int id = graphics::TextureRegistry::Load(path, linear, repeat, &width, &height);
    if (id == 0) return JS_NULL;
    JSValue o = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, o, "id", JS_NewInt32(ctx, id));
    JS_SetPropertyStr(ctx, o, "width", JS_NewInt32(ctx, width));
    JS_SetPropertyStr(ctx, o, "height", JS_NewInt32(ctx, height));
    return o;
}

// bl.screen.saveNeutralLut(caminho, tamanho) -> bool: a LUT neutra em tira
// (tamanho^2 x tamanho) para o modder editar.
JSValue screen_saveNeutralLut(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsString(argv[0])) return JS_ThrowTypeError(ctx, "bl.screen.saveNeutralLut(path, size)");
    const char* text = JS_ToCString(ctx, argv[0]);
    const std::string path = resolveModPath(ctx, text ? text : "");
    JS_FreeCString(ctx, text);
    int32_t size = 16;
    if (argc > 1) JS_ToInt32(ctx, &size, argv[1]);
    return JS_NewBool(ctx, graphics::TextureRegistry::SaveNeutralLut(path, size));
}

JSValue screen_stats(JSContext* ctx, JSValueConst, int, JSValueConst*) {
    graphics::GpuTimer::Enable();
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
        JS_CFUNC_DEF("submit", 3, screen_submit),
        JS_CFUNC_DEF("texture", 3, screen_texture),
        JS_CFUNC_DEF("saveNeutralLut", 2, screen_saveNeutralLut),
        JS_CFUNC_DEF("__stats", 0, screen_stats),
        JS_CFUNC_DEF("__trace", 0, screen_trace),
    };
    JSValue screen = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, screen, fns, sizeof fns / sizeof fns[0]);
    JS_SetPropertyStr(ctx, bl, "screen", screen);
}

} // namespace bl::script
#endif
