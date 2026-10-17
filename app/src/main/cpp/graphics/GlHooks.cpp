#include "graphics/GlHooks.h"
#include "graphics/GlApi.h"
#include "graphics/GlShadow.h"
#include "graphics/GpuTimer.h"
#include "graphics/EffectRenderer.h"
#include "graphics/RenderHook.h"
#include "core/Log.h"
#include "hook/HookManager.h"

#include <EGL/egl.h>
#include <atomic>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <dlfcn.h>
#include <mutex>
#include <sys/prctl.h>

namespace bl::graphics {

namespace {

using SwapFn = EGLBoolean (*)(EGLDisplay, EGLSurface);
using BindFramebufferFn = void (*)(GLenum, GLuint);
using ViewportFn = void (*)(GLint, GLint, GLsizei, GLsizei);
using ClearFn = void (*)(GLbitfield);
using DrawElementsFn = void (*)(GLenum, GLsizei, GLenum, const void*);
using DrawArraysFn = void (*)(GLenum, GLint, GLsizei);
using BlitFn = void (*)(GLint, GLint, GLint, GLint, GLint, GLint, GLint, GLint, GLbitfield, GLenum);
using UintFn = void (*)(GLuint);
using EnumFn = void (*)(GLenum);
using ColorMaskFn = void (*)(GLboolean, GLboolean, GLboolean, GLboolean);

SwapFn g_origSwap = nullptr;
BindFramebufferFn g_origBindFramebuffer = nullptr;
ViewportFn g_origViewport = nullptr;
ClearFn g_origClear = nullptr;
DrawElementsFn g_origDrawElements = nullptr;
DrawArraysFn g_origDrawArrays = nullptr;
BlitFn g_origBlit = nullptr;
UintFn g_origUseProgram = nullptr;
UintFn g_origBindVertexArray = nullptr;
EnumFn g_origActiveTexture = nullptr;
EnumFn g_origEnable = nullptr;
EnumFn g_origDisable = nullptr;
ColorMaskFn g_origColorMask = nullptr;

// As nossas proprias chamadas GL (o desenho do efeito) passam pelos mesmos
// hooks: nao entram no rastro nem mudam o espelho do estado da Unity.
thread_local bool t_inside = false;

// So a thread de render da Unity conta. O RenderThread do Android (as Views
// do app, o botao flutuante) chama as mesmas funcoes num OUTRO contexto GL, e
// estragaria o espelho. Com render multithread a Unity desenha na
// UnityGfxDeviceW; sem, na UnityMain.
thread_local int t_unity = -1;

bool unityThread() {
    if (t_unity < 0) {
        char name[17] = {0};   // PR_GET_NAME: ate 16 bytes (pthread_getname_np so na API 26)
        prctl(PR_GET_NAME, name);
        t_unity = std::strncmp(name, "UnityGfxDevice", 14) == 0 || std::strcmp(name, "UnityMain") == 0;
    }
    return t_unity == 1;
}

bool tracked() {
    return !t_inside && unityThread();
}

// O espelho do estado da Unity (GlShadow.h) e onde foi o ultimo desenho dela:
// quando o marcador aparece, o framebuffer ja e o do marcador, e o do mundo e
// o do desenho anterior.
GlShadow g_shadow;
DrawTarget g_lastDraw;
bool g_synced = false;
bool g_trustShadow = true;
std::atomic<int64_t> g_shadowChecks{0};
std::atomic<int64_t> g_shadowMismatches{0};
constexpr int64_t kShadowChecks = 120;

// O estado da Unity agora: o espelho, conferido com o driver nos primeiros
// usos. Uma diferenca e o espelho deixa de valer (o glGet* volta de vez).
GlShadow unityState() {
    if (!g_synced) {
        g_shadow = queryGlState();
        g_synced = true;
    }
    if (!g_trustShadow) return queryGlState();
    if (g_shadowChecks < kShadowChecks) {
        g_shadowChecks++;
        const GlShadow real = queryGlState();
        char diff[160];
        if (compareGlState(g_shadow, real, diff, sizeof diff) > 0) {
            g_shadowMismatches++;
            g_trustShadow = false;
            BL_ERROR("efeitos de tela: espelho do estado GL errado (%s); volta para glGet*", diff);
            g_shadow = real;
            return real;
        }
    }
    return g_shadow;
}

void noteDraw() {
    g_lastDraw.fbo = static_cast<int>(g_shadow.drawFbo);
    for (int i = 0; i < 4; ++i) g_lastDraw.viewport[i] = g_shadow.viewport[i];
}

// O rastro: pedido pela thread do jogo, gravado pela de render entre dois
// eglSwapBuffers.
std::atomic<bool> g_traceRequested{false};
bool g_tracing = false;
std::string g_trace;
int g_pendingDraws = 0;

void flushDraws() {
    if (g_pendingDraws == 0) return;
    g_trace += " D" + std::to_string(g_pendingDraws);
    g_pendingDraws = 0;
}

void traceEvent(const char* fmt, ...) __attribute__((format(printf, 1, 2)));
void traceEvent(const char* fmt, ...) {
    if (!g_tracing || g_trace.size() > 12000) return;
    flushDraws();
    char buf[96];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buf, sizeof buf, fmt, args);
    va_end(args);
    g_trace += ' ';
    g_trace += buf;
}

void logTrace() {
    flushDraws();
    BL_INFO("rastro GL: %zu caracteres", g_trace.size());
    for (size_t i = 0; i < g_trace.size(); i += 700) {
        BL_INFO("rastro GL [%zu]:%s", i / 700, g_trace.substr(i, 700).c_str());
    }
}

// O blit do marcador (RenderHook.cpp) chegou: a camera do mundo acabou e a
// da interface ainda nao comecou.
void onMarker() {
    traceEvent("MARCADOR");
    noteMarkerHit();
    EffectFrame effects;
    if (!takeWorldEffects(effects) || g_lastDraw.viewport[2] <= 0) return;
    const GlShadow state = unityState();
    t_inside = true;
    EffectRenderer::Execute(effects, g_lastDraw, state);
    t_inside = false;
}

void hkBindFramebuffer(GLenum target, GLuint fbo) {
    if (tracked()) {
        if (target != GL_READ_FRAMEBUFFER) g_shadow.drawFbo = fbo;
        if (target != GL_DRAW_FRAMEBUFFER) g_shadow.readFbo = fbo;
        traceEvent("FB%s(%u)", target == GL_READ_FRAMEBUFFER ? "r" : target == GL_DRAW_FRAMEBUFFER ? "d" : "", fbo);
    }
    g_origBindFramebuffer(target, fbo);
}

void hkViewport(GLint x, GLint y, GLsizei w, GLsizei h) {
    if (tracked()) {
        traceEvent("VP(%d,%d,%d,%d)", x, y, w, h);
        if (w == kMarkerWidth && h == kMarkerHeight) onMarker();
        g_shadow.viewport[0] = x;
        g_shadow.viewport[1] = y;
        g_shadow.viewport[2] = w;
        g_shadow.viewport[3] = h;
    }
    g_origViewport(x, y, w, h);
}

void hkUseProgram(GLuint program) {
    if (tracked()) g_shadow.program = program;
    g_origUseProgram(program);
}

void hkBindVertexArray(GLuint vao) {
    if (tracked()) g_shadow.vertexArray = vao;
    g_origBindVertexArray(vao);
}

void hkActiveTexture(GLenum unit) {
    if (tracked()) g_shadow.activeTexture = unit;
    g_origActiveTexture(unit);
}

bool* capSlot(GLenum cap) {
    switch (cap) {
        case GL_BLEND: return &g_shadow.blend;
        case GL_DEPTH_TEST: return &g_shadow.depth;
        case GL_STENCIL_TEST: return &g_shadow.stencil;
        case GL_CULL_FACE: return &g_shadow.cull;
        case GL_SCISSOR_TEST: return &g_shadow.scissor;
        default: return nullptr;
    }
}

void hkEnable(GLenum cap) {
    if (tracked()) {
        if (bool* slot = capSlot(cap)) *slot = true;
    }
    g_origEnable(cap);
}

void hkDisable(GLenum cap) {
    if (tracked()) {
        if (bool* slot = capSlot(cap)) *slot = false;
    }
    g_origDisable(cap);
}

void hkColorMask(GLboolean r, GLboolean gr, GLboolean b, GLboolean a) {
    if (tracked()) {
        g_shadow.colorMask[0] = r;
        g_shadow.colorMask[1] = gr;
        g_shadow.colorMask[2] = b;
        g_shadow.colorMask[3] = a;
    }
    g_origColorMask(r, gr, b, a);
}

void hkClear(GLbitfield mask) {
    if (tracked()) {
        traceEvent("CL(%s%s%s)", mask & GL_COLOR_BUFFER_BIT ? "c" : "", mask & GL_DEPTH_BUFFER_BIT ? "d" : "",
                   mask & GL_STENCIL_BUFFER_BIT ? "s" : "");
    }
    g_origClear(mask);
}

void hkDrawElements(GLenum mode, GLsizei count, GLenum type, const void* indices) {
    if (tracked()) {
        noteDraw();
        if (g_tracing) g_pendingDraws++;
    }
    g_origDrawElements(mode, count, type, indices);
}

void hkDrawArrays(GLenum mode, GLint first, GLsizei count) {
    if (tracked()) {
        noteDraw();
        if (g_tracing) g_pendingDraws++;
    }
    g_origDrawArrays(mode, first, count);
}

void hkBlit(GLint sx0, GLint sy0, GLint sx1, GLint sy1, GLint dx0, GLint dy0, GLint dx1, GLint dy1,
            GLbitfield mask, GLenum filter) {
    if (tracked()) traceEvent("BL(%d,%d,%d,%d->%d,%d,%d,%d)", sx0, sy0, sx1, sy1, dx0, dy0, dx1, dy1);
    g_origBlit(sx0, sy0, sx1, sy1, dx0, dy0, dx1, dy1, mask, filter);
}

int g_frame = 0;

EGLBoolean hkSwap(EGLDisplay display, EGLSurface surface) {
    if (!unityThread()) return g_origSwap(display, surface);

    if (g_tracing) {
        traceEvent("SWAP");
        g_tracing = false;
        logTrace();
        g_trace.clear();
    } else if (g_traceRequested.exchange(false)) {
        g_tracing = true;
        g_trace = "SWAP";
    }

    // A camada Screen (e os do mundo, quando nao ha marcador): no fim do
    // quadro, por cima da interface.
    EffectFrame effects;
    if (takeScreenEffects(effects)) {
        const GlShadow state = unityState();
        DrawTarget target;
        target.fbo = static_cast<int>(state.drawFbo);
        for (int i = 0; i < 4; ++i) target.viewport[i] = state.viewport[i];
        t_inside = true;
        EffectRenderer::Execute(effects, target, state);
        t_inside = false;
    }

    // O quadro inteiro na GPU, de um swap ao outro, em quadros alternados (os
    // outros medem o efeito sozinho; consultas de tempo nao se aninham).
    GpuTimer::End(GpuChannel::Frame);
    const EGLBoolean result = g_origSwap(display, surface);
    if (++g_frame % 2 == 0) GpuTimer::Begin(GpuChannel::Frame);
    return result;
}

template <typename Fn>
bool hookGl(const char* name, void* address, Fn replacement, Fn* original) {
    if (address && hook::install(address, reinterpret_cast<void*>(replacement), reinterpret_cast<void**>(original))) {
        return true;
    }
    BL_ERROR("efeitos de tela: %s nao hookado", name);
    return false;
}

} // namespace

void installGlHooks() {
    static std::once_flag once;
    std::call_once(once, [] {
        auto& g = gl();
        if (!g.load()) return;
        void* egl = dlopen("libEGL.so", RTLD_NOW);
        void* swap = egl ? dlsym(egl, "eglSwapBuffers") : nullptr;

        bool ok = hookGl("eglSwapBuffers", swap, hkSwap, &g_origSwap);
        ok &= hookGl("glBindFramebuffer", reinterpret_cast<void*>(g.BindFramebuffer), hkBindFramebuffer, &g_origBindFramebuffer);
        ok &= hookGl("glViewport", reinterpret_cast<void*>(g.Viewport), hkViewport, &g_origViewport);
        ok &= hookGl("glUseProgram", reinterpret_cast<void*>(g.UseProgram), hkUseProgram, &g_origUseProgram);
        ok &= hookGl("glBindVertexArray", reinterpret_cast<void*>(g.BindVertexArray), hkBindVertexArray, &g_origBindVertexArray);
        ok &= hookGl("glActiveTexture", reinterpret_cast<void*>(g.ActiveTexture), hkActiveTexture, &g_origActiveTexture);
        ok &= hookGl("glEnable", reinterpret_cast<void*>(g.Enable), hkEnable, &g_origEnable);
        ok &= hookGl("glDisable", reinterpret_cast<void*>(g.Disable), hkDisable, &g_origDisable);
        ok &= hookGl("glColorMask", reinterpret_cast<void*>(g.ColorMask), hkColorMask, &g_origColorMask);
        ok &= hookGl("glClear", reinterpret_cast<void*>(g.Clear), hkClear, &g_origClear);
        ok &= hookGl("glDrawElements", reinterpret_cast<void*>(g.DrawElements), hkDrawElements, &g_origDrawElements);
        ok &= hookGl("glDrawArrays", reinterpret_cast<void*>(g.DrawArrays), hkDrawArrays, &g_origDrawArrays);
        ok &= hookGl("glBlitFramebuffer", reinterpret_cast<void*>(g.BlitFramebuffer), hkBlit, &g_origBlit);
        BL_INFO("efeitos de tela: hooks GL %s", ok ? "ok" : "com falha");
    });
}

void requestGlTrace() {
    installGlHooks();
    g_traceRequested.store(true);
}

void fillGlHookStats(RenderStats& stats) {
    stats.shadowChecks = g_shadowChecks.load();
    stats.shadowMismatches = g_shadowMismatches.load();
}

} // namespace bl::graphics
