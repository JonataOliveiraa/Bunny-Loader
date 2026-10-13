#include "graphics/EffectRenderer.h"
#include "graphics/GlApi.h"
#include "graphics/GlState.h"
#include "graphics/GpuTimer.h"
#include "graphics/ShaderRegistry.h"
#include "graphics/TextureRegistry.h"
#include "core/Log.h"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <mutex>
#include <unordered_map>

namespace bl::graphics {

namespace {

// Uma textura com FBO proprio, do tamanho do viewport (vezes a escala).
struct Surface {
    GLuint texture = 0, fbo = 0;
    GLint w = 0, h = 0;
};

struct Resources {
    bool tried = false, ok = false;
    GLuint vao = 0, vbo = 0, sampler = 0;
    Surface copy;      // a copia da tela (uScreen)
    Surface history;   // o mundo do quadro anterior
    bool historyValid = false;
    std::unordered_map<std::string, Surface> targets;   // nome@escala -> alvo
};

Resources g_res;
std::mutex g_statsMutex;
RenderStats g_local;   // so os campos do lado GL
bool g_loggedFirst = false;
bool g_warnedOffset = false;
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

    // Sampler proprio: as texturas dos efeitos nao dependem do sampler que
    // estiver na unidade, nem mexem no da Unity.
    g.GenSamplers(1, &g_res.sampler);
    g.SamplerParameteri(g_res.sampler, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
    g.SamplerParameteri(g_res.sampler, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
    g.SamplerParameteri(g_res.sampler, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    g.SamplerParameteri(g_res.sampler, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    g_res.ok = true;
    return true;
}

// (Re)cria a superficie se o tamanho mudou. true se ela ficou nova (sem conteudo).
bool ensureSurface(Surface& s, GLint w, GLint h, const char* what, bool& fresh) {
    fresh = false;
    if (s.texture && s.w == w && s.h == h) return true;
    auto& g = gl();
    GlStateGuard saved;
    if (!s.texture) g.GenTextures(1, &s.texture);
    if (!s.fbo) g.GenFramebuffers(1, &s.fbo);
    g.BindBuffer(GL_PIXEL_UNPACK_BUFFER, 0);
    g.ActiveTexture(kScreenUnit);
    g.BindTexture(GL_TEXTURE_2D, s.texture);
    g.TexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, w, h, 0, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
    g.BindFramebuffer(GL_DRAW_FRAMEBUFFER, s.fbo);
    g.FramebufferTexture2D(GL_DRAW_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, s.texture, 0);
    const GLenum status = g.CheckFramebufferStatus(GL_DRAW_FRAMEBUFFER);
    if (status != GL_FRAMEBUFFER_COMPLETE) {
        BL_ERROR("efeitos de tela: FBO de %s incompleto (0x%x) em %dx%d", what, status, w, h);
        s.w = s.h = 0;
        return false;
    }
    s.w = w;
    s.h = h;
    fresh = true;
    BL_DEBUG("efeitos de tela: %s em %dx%d", what, w, h);
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

// Pixels relativos a origem do viewport.
struct Box {
    GLint x = 0, y = 0, w = 0, h = 0;
    bool contains(const Box& o) const {
        return w > 0 && o.x >= x && o.y >= y && o.x + o.w <= x + w && o.y + o.h <= y + h;
    }
};

// A caixa do passo (fracao do viewport) em pixels de uma superficie de
// largura x altura, com 2 px de folga para o filtro linear.
Box pixelBox(const float* box, GLint width, GLint height) {
    const float fw = static_cast<float>(width), fh = static_cast<float>(height);
    const GLint x0 = std::max(0, static_cast<GLint>(std::floor(box[0] * fw)) - 2);
    const GLint y0 = std::max(0, static_cast<GLint>(std::floor(box[1] * fh)) - 2);
    const GLint x1 = std::min(width, static_cast<GLint>(std::ceil(box[2] * fw)) + 2);
    const GLint y1 = std::min(height, static_cast<GLint>(std::ceil(box[3] * fh)) + 2);
    return {x0, y0, std::max(0, x1 - x0), std::max(0, y1 - y0)};
}

// Copia um pedaco da tela (pixels relativos ao viewport) para uma superficie
// do tamanho do viewport. O blit resolve multisample; se o driver o recusar
// nos primeiros quadros, fica a copia simples.
bool copyScreen(const DrawTarget& t, Surface& dst, const Box& box) {
    auto& g = gl();
    const GLint vx = t.viewport[0], vy = t.viewport[1];
    if (!g_useCopy) {
        if (g_errorChecks > 0) while (g.GetError() != GL_NO_ERROR) {}
        g.BindFramebuffer(GL_READ_FRAMEBUFFER, t.fbo);
        g.BindFramebuffer(GL_DRAW_FRAMEBUFFER, dst.fbo);
        g.BlitFramebuffer(vx + box.x, vy + box.y, vx + box.x + box.w, vy + box.y + box.h,
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
    g.ActiveTexture(kScreenUnit);
    g.BindTexture(GL_TEXTURE_2D, dst.texture);
    g.CopyTexSubImage2D(GL_TEXTURE_2D, 0, box.x, box.y, vx + box.x, vy + box.y, box.w, box.h);
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
    if (loc < 0 || u.v.empty()) return;
    auto& g = gl();
    const GLsizei count = static_cast<GLsizei>(u.v.size() / std::max(1, u.components));
    switch (u.components) {
        case 1: g.Uniform1fv(loc, count, u.v.data()); break;
        case 2: g.Uniform2fv(loc, count, u.v.data()); break;
        case 3: g.Uniform3fv(loc, count, u.v.data()); break;
        default: g.Uniform4fv(loc, count, u.v.data()); break;
    }
}

/** O estado de um quadro em execucao. */
class FrameRun {
public:
    FrameRun(const EffectFrame& frame, const DrawTarget& target) : frame_(frame), t_(target) {}

    bool Prepare() {
        bool fresh = false;
        if (!ensureSurface(g_res.copy, t_.viewport[2], t_.viewport[3], "copia da tela", fresh)) return false;
        for (const TargetSpec& spec : frame_.targets) {
            const GLint w = std::max(1, static_cast<GLint>(std::lround(t_.viewport[2] * spec.scale)));
            const GLint h = std::max(1, static_cast<GLint>(std::lround(t_.viewport[3] * spec.scale)));
            Surface& s = g_res.targets[spec.name + "@" + std::to_string(spec.scale)];
            if (!ensureSurface(s, w, h, "alvo", fresh)) return false;
            byId_[spec.id] = &s;
        }
        return true;
    }

    bool Draw(const EffectDraw& e, float now) {
        auto& g = gl();
        CompiledShader* shader = ShaderRegistry::Use(e.shader);
        if (!shader) return false;

        Surface* dest = e.dest > 0 ? find(e.dest) : nullptr;
        if (e.dest > 0 && !dest) return false;
        const GLint dw = dest ? dest->w : t_.viewport[2], dh = dest ? dest->h : t_.viewport[3];
        const Box box = pixelBox(e.box, dw, dh);
        if (box.w <= 0 || box.h <= 0) return false;

        // O que o passo le da tela, na caixa dele em pixels da tela.
        const bool readsScreen = shader->builtin.screen >= 0 || e.source == kTexScreen || readsTexture(e, kTexScreen);
        if (readsScreen && !refreshCopy(pixelBox(e.box, t_.viewport[2], t_.viewport[3]))) return false;

        if (dest) {
            g.BindFramebuffer(GL_FRAMEBUFFER, dest->fbo);
            g.Viewport(box.x, box.y, box.w, box.h);
        } else {
            g.BindFramebuffer(GL_FRAMEBUFFER, t_.fbo);
            g.Viewport(t_.viewport[0] + box.x, t_.viewport[1] + box.y, box.w, box.h);
        }
        g.UseProgram(shader->program);

        bind(kScreenUnit, texture(kTexScreen));
        const Bound source = texture(e.source);
        bind(kSourceUnit, source);
        int unit = 0;
        for (const TextureBinding& tb : e.textures) {
            const GLint loc = ShaderRegistry::Uniform(*shader, tb.name);
            if (loc < 0 || unit >= kModUnits) continue;
            const GLenum glUnit = kFirstModUnit - unit++;
            bind(glUnit, texture(tb.texture));
            g.Uniform1i(loc, static_cast<GLint>(glUnit - GL_TEXTURE0));
        }

        const float vw = static_cast<float>(t_.viewport[2]), vh = static_cast<float>(t_.viewport[3]);
        const BuiltinUniforms& b = shader->builtin;
        // uBox em fracao do viewport, recalculada dos pixels: o vUV bate com o fragmento.
        if (b.box >= 0) {
            const float fw = static_cast<float>(dw), fh = static_cast<float>(dh);
            g.Uniform4f(b.box, box.x / fw, box.y / fh, (box.x + box.w) / fw, (box.y + box.h) / fh);
        }
        if (b.screenTexel >= 0) g.Uniform2f(b.screenTexel, 1.f / vw, 1.f / vh);
        if (b.sourceTexel >= 0 && source.texture) g.Uniform2f(b.sourceTexel, 1.f / source.w, 1.f / source.h);
        if (b.view >= 0) g.Uniform4f(b.view, static_cast<float>(t_.viewport[0]), static_cast<float>(t_.viewport[1]), vw, vh);
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
        if (!dest) screenDirty_ = true;

        std::lock_guard<std::mutex> lock(g_statsMutex);
        g_local.boxPixels += static_cast<int64_t>(box.w) * box.h;
        return true;
    }

    // O mundo com os efeitos, para o History do proximo quadro.
    void SaveHistory() {
        bool fresh = false;
        if (!ensureSurface(g_res.history, t_.viewport[2], t_.viewport[3], "quadro anterior", fresh)) return;
        g_res.historyValid = copyScreen(t_, g_res.history, {0, 0, t_.viewport[2], t_.viewport[3]});
    }

private:
    Surface* find(int id) {
        auto it = byId_.find(id);
        return it == byId_.end() ? nullptr : it->second;
    }

    struct Bound {
        GLuint texture = 0, sampler = 0;
        GLint w = 1, h = 1;
    };

    // A textura de um kTex*, alvo ou textura do mod; o quadro anterior que
    // ainda nao existe vira a copia da tela.
    Bound texture(int id) {
        const Surface* s = nullptr;
        if (id == kTexScreen) s = &g_res.copy;
        else if (id == kTexHistory) s = g_res.historyValid ? &g_res.history : &g_res.copy;
        else if (id > 0) s = find(id);
        else if (id <= kFirstModTexture) {
            const LoadedTexture* t = TextureRegistry::Use(id);
            return t ? Bound{t->texture, t->sampler, t->width, t->height} : Bound{};
        }
        return s ? Bound{s->texture, g_res.sampler, s->w, s->h} : Bound{};
    }

    static bool readsTexture(const EffectDraw& e, int texture) {
        for (const TextureBinding& tb : e.textures) {
            if (tb.texture == texture || (texture == kTexScreen && tb.texture == kTexHistory && !g_res.historyValid)) {
                return true;
            }
        }
        return false;
    }

    // A copia so e refeita se a tela mudou desde a ultima ou se a caixa nova
    // sai da que ja esta copiada.
    bool refreshCopy(const Box& box) {
        if (!screenDirty_ && copied_.contains(box)) return true;
        if (!copyScreen(t_, g_res.copy, box)) return false;
        copied_ = box;
        screenDirty_ = false;
        return true;
    }

    void bind(GLenum unit, const Bound& t) {
        if (!t.texture) return;
        auto& g = gl();
        g.ActiveTexture(unit);
        g.BindTexture(GL_TEXTURE_2D, t.texture);
        g.BindSampler(unit - GL_TEXTURE0, t.sampler);
    }

    const EffectFrame& frame_;
    DrawTarget t_;
    std::unordered_map<int, Surface*> byId_;
    Box copied_;
    bool screenDirty_ = true;
};

} // namespace

void EffectRenderer::Execute(const EffectFrame& frame, const DrawTarget& target, const GlShadow& state) {
    auto& g = gl();
    if ((frame.empty() && !frame.history) || !g.load()) return;
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
    GLint samples = 0;
    if (!g_warnedOffset && (target.viewport[0] != 0 || target.viewport[1] != 0)) {
        g.GetIntegerv(GL_SAMPLES, &samples);
        if (samples > 0) {
            g_warnedOffset = true;
            BL_WARN("efeitos de tela: viewport com origem %d,%d e multisample; a copia pode falhar",
                    target.viewport[0], target.viewport[1]);
        }
    }

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

    FrameRun run(frame, target);
    int drawn = 0;
    if (run.Prepare()) {
        for (const EffectDraw& e : frame.draws) drawn += run.Draw(e, now) ? 1 : 0;
        if (frame.history) run.SaveHistory();
    }
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
