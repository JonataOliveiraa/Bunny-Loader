#include "graphics/PocTwirl.h"
#include "graphics/GlApi.h"
#include "graphics/GlState.h"
#include "graphics/GpuTimer.h"
#include "core/Log.h"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <mutex>

namespace bl::graphics {

namespace {

const char* kVertex = R"(#version 300 es
layout(location = 0) in vec2 aPos;
void main() {
    gl_Position = vec4(aPos, 0.0, 1.0);
}
)";

// O quad cobre so a caixa do efeito (o viewport e ela); a posicao na tela vem
// do gl_FragCoord. uView: origem e tamanho do viewport da camera em pixels;
// uTexSize: a copia, que comeca no pixel (0,0) do framebuffer.
const char* kFragment = R"(#version 300 es
precision highp float;
out vec4 outColor;
uniform sampler2D uScreen;
uniform vec4 uView;
uniform vec2 uTexSize;
uniform vec2 uCenter;
uniform float uRadius;
uniform float uStrength;
uniform float uTime;

vec4 screenAt(vec2 uv) {
    return texture(uScreen, (uView.xy + uv * uView.zw) / uTexSize);
}

void main() {
    vec2 uv = (gl_FragCoord.xy - uView.xy) / uView.zw;
    float aspect = uView.z / uView.w;
    vec2 d = uv - uCenter;
    d.x *= aspect;
    float r = length(d);
    if (r > uRadius) discard;

    float f = 1.0 - smoothstep(0.0, uRadius, r);
    float a = (uStrength + uTime * 0.8) * f * f;
    vec2 rd = mat2(cos(a), sin(a), -sin(a), cos(a)) * d;
    rd.x /= aspect;
    vec4 c = screenAt(uCenter + rd);
    // Anel fino na borda: deixa a area do efeito visivel no print da prova.
    float ring = smoothstep(uRadius * 0.97, uRadius, r);
    outColor = vec4(mix(c.rgb, vec3(1.0, 0.3, 0.9), ring * 0.8), 1.0);
}
)";

struct Resources {
    bool tried = false, ok = false;
    GLuint program = 0, vao = 0, vbo = 0, fbo = 0, texture = 0, sampler = 0;
    GLint texW = 0, texH = 0;
    GLint uScreen = -1, uView = -1, uTexSize = -1, uCenter = -1, uRadius = -1, uStrength = -1, uTime = -1;
};

Resources g_res;
std::mutex g_statsMutex;
RenderStats g_local;   // so os campos do lado GL
bool g_loggedFirst = false;
// O glGetError espera a GPU em alguns drivers: so nos primeiros quadros,
// para descobrir se o blit vale neste aparelho.
int g_errorChecks = 30;
bool g_useCopy = false;   // o blit falhou aqui: copia simples

GLuint compile(GLenum type, const char* src) {
    auto& g = gl();
    GLuint s = g.CreateShader(type);
    g.ShaderSource(s, 1, &src, nullptr);
    g.CompileShader(s);
    GLint ok = 0;
    g.GetShaderiv(s, GL_COMPILE_STATUS, &ok);
    if (!ok) {
        char log[512] = {0};
        g.GetShaderInfoLog(s, sizeof log - 1, nullptr, log);
        BL_ERROR("efeitos de tela: shader nao compilou: %s", log);
        g.DeleteShader(s);
        return 0;
    }
    return s;
}

bool init() {
    if (g_res.tried) return g_res.ok;
    g_res.tried = true;
    auto& g = gl();
    GlStateGuard saved;

    GLuint vs = compile(GL_VERTEX_SHADER, kVertex);
    GLuint fs = compile(GL_FRAGMENT_SHADER, kFragment);
    if (!vs || !fs) return false;
    g_res.program = g.CreateProgram();
    g.AttachShader(g_res.program, vs);
    g.AttachShader(g_res.program, fs);
    g.LinkProgram(g_res.program);
    g.DeleteShader(vs);
    g.DeleteShader(fs);
    GLint linked = 0;
    g.GetProgramiv(g_res.program, GL_LINK_STATUS, &linked);
    if (!linked) {
        char log[512] = {0};
        g.GetProgramInfoLog(g_res.program, sizeof log - 1, nullptr, log);
        BL_ERROR("efeitos de tela: programa nao ligou: %s", log);
        return false;
    }
    g_res.uScreen = g.GetUniformLocation(g_res.program, "uScreen");
    g_res.uView = g.GetUniformLocation(g_res.program, "uView");
    g_res.uTexSize = g.GetUniformLocation(g_res.program, "uTexSize");
    g_res.uCenter = g.GetUniformLocation(g_res.program, "uCenter");
    g_res.uRadius = g.GetUniformLocation(g_res.program, "uRadius");
    g_res.uStrength = g.GetUniformLocation(g_res.program, "uStrength");
    g_res.uTime = g.GetUniformLocation(g_res.program, "uTime");
    g.UseProgram(g_res.program);
    g.Uniform1i(g_res.uScreen, static_cast<GLint>(kEffectTextureUnit - GL_TEXTURE0));

    static const float quad[8] = {-1.f, -1.f, 1.f, -1.f, -1.f, 1.f, 1.f, 1.f};
    g.GenVertexArrays(1, &g_res.vao);
    g.BindVertexArray(g_res.vao);
    g.GenBuffers(1, &g_res.vbo);
    g.BindBuffer(GL_ARRAY_BUFFER, g_res.vbo);
    g.BufferData(GL_ARRAY_BUFFER, sizeof quad, quad, GL_STATIC_DRAW);
    g.VertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, 0, nullptr);
    g.EnableVertexAttribArray(0);

    // Sampler proprio: a textura da copia nao depende do sampler que estiver
    // na unidade, nem mexe no da Unity.
    g.GenSamplers(1, &g_res.sampler);
    g.SamplerParameteri(g_res.sampler, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
    g.SamplerParameteri(g_res.sampler, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
    g.SamplerParameteri(g_res.sampler, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    g.SamplerParameteri(g_res.sampler, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

    g.GenFramebuffers(1, &g_res.fbo);
    g.GenTextures(1, &g_res.texture);
    g_res.ok = true;
    return true;
}

bool ensureTexture(GLint w, GLint h) {
    if (w == g_res.texW && h == g_res.texH) return true;
    auto& g = gl();
    GlStateGuard saved;
    g.BindBuffer(GL_PIXEL_UNPACK_BUFFER, 0);
    g.ActiveTexture(kEffectTextureUnit);
    g.BindTexture(GL_TEXTURE_2D, g_res.texture);
    g.TexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, w, h, 0, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
    g.BindFramebuffer(GL_DRAW_FRAMEBUFFER, g_res.fbo);
    g.FramebufferTexture2D(GL_DRAW_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, g_res.texture, 0);
    const GLenum status = g.CheckFramebufferStatus(GL_DRAW_FRAMEBUFFER);
    if (status != GL_FRAMEBUFFER_COMPLETE) {
        BL_ERROR("efeitos de tela: FBO da copia incompleto (0x%x) em %dx%d", status, w, h);
        g_res.texW = g_res.texH = 0;
        return false;
    }
    g_res.texW = w;
    g_res.texH = h;
    BL_INFO("efeitos de tela: textura da copia em %dx%d", w, h);
    return true;
}

void logFirst(const DrawTarget& target) {
    if (g_loggedFirst) return;
    g_loggedFirst = true;
    auto& g = gl();
    GLint samples = 0;
    g.GetIntegerv(GL_SAMPLES, &samples);
    const char* version = reinterpret_cast<const char*>(g.GetString(GL_VERSION));
    const char* renderer = reinterpret_cast<const char*>(g.GetString(GL_RENDERER));
    BL_INFO("efeitos de tela: GL '%s' em '%s'; fbo %d viewport=%d,%d %dx%d samples=%d",
            version ? version : "?", renderer ? renderer : "?", target.fbo,
            target.viewport[0], target.viewport[1], target.viewport[2], target.viewport[3], samples);
    std::lock_guard<std::mutex> lock(g_statsMutex);
    g_local.samples = samples;
}

struct Box {
    GLint x = 0, y = 0, w = 0, h = 0;
};

// A caixa do efeito em pixels do framebuffer, presa ao viewport, com 2 px de
// folga para o filtro linear. O redemoinho so amostra dentro do proprio
// circulo, entao a copia nao precisa de mais que isso.
Box effectBox(const PocParams& p, const DrawTarget& t) {
    const float r = p.radius * static_cast<float>(t.viewport[3]);
    const float cx = static_cast<float>(t.viewport[0]) + p.u * static_cast<float>(t.viewport[2]);
    const float cy = static_cast<float>(t.viewport[1]) + p.v * static_cast<float>(t.viewport[3]);
    const GLint x0 = std::max(t.viewport[0], static_cast<GLint>(std::floor(cx - r)) - 2);
    const GLint y0 = std::max(t.viewport[1], static_cast<GLint>(std::floor(cy - r)) - 2);
    const GLint x1 = std::min(t.viewport[0] + t.viewport[2], static_cast<GLint>(std::ceil(cx + r)) + 2);
    const GLint y1 = std::min(t.viewport[1] + t.viewport[3], static_cast<GLint>(std::ceil(cy + r)) + 2);
    return {x0, y0, std::max(0, x1 - x0), std::max(0, y1 - y0)};
}

// Copia a caixa do framebuffer da camera para o mesmo lugar da nossa
// textura (do tamanho da tela). O blit resolve multisample; se o driver o
// recusar nos primeiros quadros, fica a copia simples.
bool capture(const DrawTarget& t, const Box& box) {
    auto& g = gl();
    if (!ensureTexture(t.viewport[0] + t.viewport[2], t.viewport[1] + t.viewport[3])) return false;

    if (!g_useCopy) {
        if (g_errorChecks > 0) while (g.GetError() != GL_NO_ERROR) {}
        g.BindFramebuffer(GL_READ_FRAMEBUFFER, t.fbo);
        g.BindFramebuffer(GL_DRAW_FRAMEBUFFER, g_res.fbo);
        g.BlitFramebuffer(box.x, box.y, box.x + box.w, box.y + box.h,
                          box.x, box.y, box.x + box.w, box.y + box.h, GL_COLOR_BUFFER_BIT, GL_NEAREST);
        if (g_errorChecks <= 0) return true;
        g_errorChecks--;
        const GLenum error = g.GetError();
        if (error == GL_NO_ERROR) return true;
        g_useCopy = true;
        BL_WARN("efeitos de tela: blit recusado (0x%x); usando copia simples", error);
        std::lock_guard<std::mutex> lock(g_statsMutex);
        g_local.blitError = static_cast<int32_t>(error);
    }
    g.BindFramebuffer(GL_READ_FRAMEBUFFER, t.fbo);
    g.ActiveTexture(kEffectTextureUnit);
    g.BindTexture(GL_TEXTURE_2D, g_res.texture);
    g.CopyTexSubImage2D(GL_TEXTURE_2D, 0, box.x, box.y, box.x, box.y, box.w, box.h);
    std::lock_guard<std::mutex> lock(g_statsMutex);
    g_local.copyFallbacks++;
    return true;
}

float seconds() {
    using namespace std::chrono;
    static const auto start = steady_clock::now();
    return duration<float>(steady_clock::now() - start).count();
}

} // namespace

void PocTwirl::Draw(const PocParams& params, const DrawTarget& target, const GlShadow& state) {
    auto& g = gl();
    if (!g.load()) return;
    const bool context = g.eglGetCurrentContext() != nullptr;
    {
        std::lock_guard<std::mutex> lock(g_statsMutex);
        g_local.hasContext = context;
        g_local.drawFbo = target.fbo;
        g_local.readFbo = target.fbo;
        for (int i = 0; i < 4; ++i) g_local.viewport[i] = target.viewport[i];
    }
    if (!context || target.viewport[2] <= 0 || target.viewport[3] <= 0) return;
    const Box box = effectBox(params, target);
    if (box.w <= 0 || box.h <= 0) return;
    logFirst(target);
    if (!init()) return;

    const auto started = std::chrono::steady_clock::now();
    GlRestore restore(state);
    GpuTimer::Begin(GpuChannel::Effect);
    if (state.scissor) g.Disable(GL_SCISSOR_TEST);
    if (!capture(target, box)) {
        GpuTimer::End(GpuChannel::Effect);
        return;
    }

    g.BindFramebuffer(GL_FRAMEBUFFER, target.fbo);
    g.Viewport(box.x, box.y, box.w, box.h);
    if (state.blend) g.Disable(GL_BLEND);
    if (state.depth) g.Disable(GL_DEPTH_TEST);
    if (state.stencil) g.Disable(GL_STENCIL_TEST);
    if (state.cull) g.Disable(GL_CULL_FACE);
    if (!(state.colorMask[0] && state.colorMask[1] && state.colorMask[2] && state.colorMask[3])) {
        g.ColorMask(GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE);
    }

    g.UseProgram(g_res.program);
    g.ActiveTexture(kEffectTextureUnit);
    g.BindTexture(GL_TEXTURE_2D, g_res.texture);
    g.BindSampler(kEffectTextureUnit - GL_TEXTURE0, g_res.sampler);
    g.Uniform4f(g_res.uView, static_cast<float>(target.viewport[0]), static_cast<float>(target.viewport[1]),
                static_cast<float>(target.viewport[2]), static_cast<float>(target.viewport[3]));
    g.Uniform2f(g_res.uTexSize, static_cast<float>(g_res.texW), static_cast<float>(g_res.texH));
    g.Uniform2f(g_res.uCenter, params.u, params.v);
    g.Uniform1f(g_res.uRadius, params.radius);
    g.Uniform1f(g_res.uStrength, params.strength);
    g.Uniform1f(g_res.uTime, seconds());
    g.BindVertexArray(g_res.vao);
    g.DrawArrays(GL_TRIANGLE_STRIP, 0, 4);
    GpuTimer::End(GpuChannel::Effect);

    const auto cpuNs = std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now() - started).count();
    std::lock_guard<std::mutex> lock(g_statsMutex);
    g_local.draws++;
    g_local.renderCpuNs += cpuNs;
    g_local.boxPixels += static_cast<int64_t>(box.w) * box.h;
}

void PocTwirl::FillStats(RenderStats& stats) {
    std::lock_guard<std::mutex> lock(g_statsMutex);
    stats.draws = g_local.draws;
    stats.drawFbo = g_local.drawFbo;
    stats.readFbo = g_local.readFbo;
    for (int i = 0; i < 4; ++i) stats.viewport[i] = g_local.viewport[i];
    stats.samples = g_local.samples;
    stats.blitError = g_local.blitError;
    stats.copyFallbacks = g_local.copyFallbacks;
    stats.hasContext = g_local.hasContext;
    stats.renderCpuNs = g_local.renderCpuNs;
    stats.boxPixels = g_local.boxPixels;
    GpuTimer::Totals(GpuChannel::Effect, stats.gpuNs, stats.gpuSamples);
    GpuTimer::Totals(GpuChannel::Frame, stats.gpuFrameNs, stats.gpuFrameSamples);
    stats.gpuTimer = GpuTimer::Available();
}

} // namespace bl::graphics
