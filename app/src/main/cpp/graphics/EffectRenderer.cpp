#include "graphics/EffectRenderer.h"
#include "graphics/GlApi.h"
#include "graphics/GlState.h"
#include "graphics/GpuTimer.h"
#include "graphics/ShaderRegistry.h"
#include "core/Log.h"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <mutex>

namespace bl::graphics {

namespace {

struct Resources {
    bool tried = false, ok = false;
    GLuint vao = 0, vbo = 0, fbo = 0, texture = 0, sampler = 0;
    GLint texW = 0, texH = 0;
};

Resources g_res;
std::mutex g_statsMutex;
RenderStats g_local;   // so os campos do lado GL
bool g_loggedFirst = false;
// O glGetError espera a GPU em alguns drivers: so nos primeiros quadros,
// para descobrir se o blit vale neste aparelho.
int g_errorChecks = 30;
bool g_useCopy = false;   // o blit falhou aqui: copia simples

bool init() {
    if (g_res.tried) return g_res.ok;
    g_res.tried = true;
    auto& g = gl();
    GlStateGuard saved;

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

// A caixa do efeito (fracao do viewport) em pixels do framebuffer, presa ao
// viewport, com 2 px de folga para o filtro linear.
Box pixelBox(const EffectDraw& e, const DrawTarget& t) {
    const float vx = static_cast<float>(t.viewport[0]), vy = static_cast<float>(t.viewport[1]);
    const float vw = static_cast<float>(t.viewport[2]), vh = static_cast<float>(t.viewport[3]);
    const GLint x0 = std::max(t.viewport[0], static_cast<GLint>(std::floor(vx + e.box[0] * vw)) - 2);
    const GLint y0 = std::max(t.viewport[1], static_cast<GLint>(std::floor(vy + e.box[1] * vh)) - 2);
    const GLint x1 = std::min(t.viewport[0] + t.viewport[2], static_cast<GLint>(std::ceil(vx + e.box[2] * vw)) + 2);
    const GLint y1 = std::min(t.viewport[1] + t.viewport[3], static_cast<GLint>(std::ceil(vy + e.box[3] * vh)) + 2);
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

void setUniform(GLint loc, const UniformValue& u) {
    if (loc < 0) return;
    auto& g = gl();
    switch (u.count) {
        case 1: g.Uniform1f(loc, u.v[0]); break;
        case 2: g.Uniform2f(loc, u.v[0], u.v[1]); break;
        case 3: g.Uniform3f(loc, u.v[0], u.v[1], u.v[2]); break;
        default: g.Uniform4f(loc, u.v[0], u.v[1], u.v[2], u.v[3]); break;
    }
}

// Um efeito: copia a caixa e redesenha com o shader dele.
bool drawOne(const EffectDraw& e, const DrawTarget& t, float now) {
    auto& g = gl();
    CompiledShader* shader = ShaderRegistry::Use(e.shader);
    if (!shader) return false;
    const Box box = pixelBox(e, t);
    if (box.w <= 0 || box.h <= 0 || !capture(t, box)) return false;

    g.BindFramebuffer(GL_FRAMEBUFFER, t.fbo);
    g.Viewport(box.x, box.y, box.w, box.h);
    g.UseProgram(shader->program);
    g.ActiveTexture(kEffectTextureUnit);
    g.BindTexture(GL_TEXTURE_2D, g_res.texture);
    g.BindSampler(kEffectTextureUnit - GL_TEXTURE0, g_res.sampler);

    const float vx = static_cast<float>(t.viewport[0]), vy = static_cast<float>(t.viewport[1]);
    const float vw = static_cast<float>(t.viewport[2]), vh = static_cast<float>(t.viewport[3]);
    const BuiltinUniforms& b = shader->builtin;
    // uBox em fracao do viewport, recalculada dos pixels: o vUV bate com o fragmento.
    if (b.box >= 0) {
        g.Uniform4f(b.box, (box.x - vx) / vw, (box.y - vy) / vh, (box.x + box.w - vx) / vw, (box.y + box.h - vy) / vh);
    }
    if (b.view >= 0) g.Uniform4f(b.view, vx, vy, vw, vh);
    if (b.texSize >= 0) g.Uniform2f(b.texSize, static_cast<float>(g_res.texW), static_cast<float>(g_res.texH));
    if (b.resolution >= 0) g.Uniform2f(b.resolution, vw, vh);
    if (b.aspect >= 0) g.Uniform1f(b.aspect, vw / vh);
    if (b.time >= 0) g.Uniform1f(b.time, now);
    if (b.age >= 0) g.Uniform1f(b.age, e.age);
    if (b.progress >= 0) g.Uniform1f(b.progress, e.progress);
    if (b.intensity >= 0) g.Uniform1f(b.intensity, e.intensity);
    if (b.center >= 0) g.Uniform2f(b.center, e.center[0], e.center[1]);
    if (b.radius >= 0) g.Uniform1f(b.radius, e.radius);
    if (b.color >= 0) g.Uniform4f(b.color, e.color[0], e.color[1], e.color[2], e.color[3]);
    for (const UniformValue& u : e.uniforms) setUniform(ShaderRegistry::Uniform(*shader, u.name), u);

    g.BindVertexArray(g_res.vao);
    g.DrawArrays(GL_TRIANGLE_STRIP, 0, 4);

    std::lock_guard<std::mutex> lock(g_statsMutex);
    g_local.boxPixels += static_cast<int64_t>(box.w) * box.h;
    return true;
}

} // namespace

void EffectRenderer::Execute(const EffectList& effects, const DrawTarget& target, const GlShadow& state) {
    auto& g = gl();
    if (effects.empty() || !g.load()) return;
    const bool context = g.eglGetCurrentContext() != nullptr;
    {
        std::lock_guard<std::mutex> lock(g_statsMutex);
        g_local.hasContext = context;
        g_local.drawFbo = target.fbo;
        g_local.readFbo = target.fbo;
        for (int i = 0; i < 4; ++i) g_local.viewport[i] = target.viewport[i];
    }
    if (!context || target.viewport[2] <= 0 || target.viewport[3] <= 0) return;
    logFirst(target);
    if (!init()) return;

    const auto started = std::chrono::steady_clock::now();
    const float now = seconds();
    GlRestore restore(state);
    GpuTimer::Begin(GpuChannel::Effect);
    if (state.scissor) g.Disable(GL_SCISSOR_TEST);
    if (state.blend) g.Disable(GL_BLEND);
    if (state.depth) g.Disable(GL_DEPTH_TEST);
    if (state.stencil) g.Disable(GL_STENCIL_TEST);
    if (state.cull) g.Disable(GL_CULL_FACE);
    if (!(state.colorMask[0] && state.colorMask[1] && state.colorMask[2] && state.colorMask[3])) {
        g.ColorMask(GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE);
    }

    int drawn = 0;
    for (const EffectDraw& e : effects) drawn += drawOne(e, target, now) ? 1 : 0;
    GpuTimer::End(GpuChannel::Effect);

    const auto cpuNs = std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now() - started).count();
    std::lock_guard<std::mutex> lock(g_statsMutex);
    g_local.draws += drawn;
    g_local.frames++;
    g_local.renderCpuNs += cpuNs;
}

void EffectRenderer::FillStats(RenderStats& stats) {
    std::lock_guard<std::mutex> lock(g_statsMutex);
    stats.draws = g_local.draws;
    stats.frames = g_local.frames;
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
