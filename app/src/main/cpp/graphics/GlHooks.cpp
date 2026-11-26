#include "graphics/GlHooks.h"
#include "graphics/GlApi.h"
#include "graphics/GlShadow.h"
#include "graphics/GpuTimer.h"
#include "graphics/EffectRenderer.h"
#include "graphics/RenderHook.h"
#include "graphics/SpriteFx.h"
#include "core/Log.h"
#include "hook/HookManager.h"

#include <EGL/egl.h>
#include <atomic>
#include <cstring>
#include <dlfcn.h>
#include <mutex>
#include <sys/prctl.h>

namespace bl::graphics {

namespace {

using SwapFn = EGLBoolean (*)(EGLDisplay, EGLSurface);
using BindFramebufferFn = void (*)(GLenum, GLuint);
using ViewportFn = void (*)(GLint, GLint, GLsizei, GLsizei);
using DrawElementsFn = void (*)(GLenum, GLsizei, GLenum, const void*);
using DrawArraysFn = void (*)(GLenum, GLint, GLsizei);
using UintFn = void (*)(GLuint);
using EnumFn = void (*)(GLenum);
using ColorMaskFn = void (*)(GLboolean, GLboolean, GLboolean, GLboolean);
using ShaderSourceFn = void (*)(GLuint, GLsizei, const GLchar* const*, const GLint*);
using AttachShaderFn = void (*)(GLuint, GLuint);
using UniformFn = void (*)(GLint, GLsizei, const GLfloat*);
using UniformMatrixFn = void (*)(GLint, GLsizei, GLboolean, const GLfloat*);
using BindTextureFn = void (*)(GLenum, GLuint);
using BindSamplerFn = void (*)(GLuint, GLuint);

SwapFn g_origSwap = nullptr;
BindFramebufferFn g_origBindFramebuffer = nullptr;
ViewportFn g_origViewport = nullptr;
DrawElementsFn g_origDrawElements = nullptr;
DrawArraysFn g_origDrawArrays = nullptr;
UintFn g_origUseProgram = nullptr;
UintFn g_origBindVertexArray = nullptr;
EnumFn g_origActiveTexture = nullptr;
EnumFn g_origEnable = nullptr;
EnumFn g_origDisable = nullptr;
ColorMaskFn g_origColorMask = nullptr;
ShaderSourceFn g_origShaderSource = nullptr;
AttachShaderFn g_origAttachShader = nullptr;
UniformFn g_origUniform1fv = nullptr;
UniformFn g_origUniform2fv = nullptr;
UniformFn g_origUniform3fv = nullptr;
UniformFn g_origUniform4fv = nullptr;
UniformMatrixFn g_origUniformMatrix4fv = nullptr;
BindTextureFn g_origBindTexture = nullptr;
BindSamplerFn g_origBindSampler = nullptr;

constexpr GLuint kUnits = 32;
GLuint g_textures2D[kUnits] = {};
GLuint g_samplers[kUnits] = {};
bool g_texturesKnown[kUnits] = {};
bool g_samplersKnown[kUnits] = {};

thread_local bool t_inside = false;

thread_local int t_unity = -1;

bool unityThread() {
    if (t_unity < 0) {
        char name[17] = {0};
        prctl(PR_GET_NAME, name);
        t_unity = std::strncmp(name, "UnityGfxDevice", 14) == 0 || std::strcmp(name, "UnityMain") == 0;
    }
    return t_unity == 1;
}

bool tracked() {
    return !t_inside && unityThread();
}

GlShadow g_shadow;
DrawTarget g_lastDraw;
bool g_synced = false;
bool g_trustShadow = true;
std::atomic<int64_t> g_shadowChecks{0};
std::atomic<int64_t> g_shadowMismatches{0};
constexpr int64_t kShadowChecks = 120;

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
            BL_ERROR("screen effects: GL state mirror is wrong (%s); falling back to glGet*", diff);
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

void onMarker() {
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
    }
    g_origBindFramebuffer(target, fbo);
}

void hkViewport(GLint x, GLint y, GLsizei w, GLsizei h) {
    if (tracked()) {
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

void hkDrawElements(GLenum mode, GLsizei count, GLenum type, const void* indices) {
    if (tracked()) {
        noteDraw();
        if (spriteFxActive()) {
            t_inside = true;
            const bool drawn = spriteFxDraw(g_shadow.program, mode, count, type, indices);
            t_inside = false;
            if (drawn) return;
        }
    }
    g_origDrawElements(mode, count, type, indices);
}

void noteUniform(int floats, GLint location, GLsizei count, const GLfloat* values) {
    if (tracked() && spriteFxActive()) spriteFxNoteUniform(g_shadow.program, floats, location, count, values);
}

void hkUniform1fv(GLint location, GLsizei count, const GLfloat* values) {
    noteUniform(1, location, count, values);
    g_origUniform1fv(location, count, values);
}

void hkUniform2fv(GLint location, GLsizei count, const GLfloat* values) {
    noteUniform(2, location, count, values);
    g_origUniform2fv(location, count, values);
}

void hkUniform3fv(GLint location, GLsizei count, const GLfloat* values) {
    noteUniform(3, location, count, values);
    g_origUniform3fv(location, count, values);
}

void hkUniform4fv(GLint location, GLsizei count, const GLfloat* values) {
    noteUniform(4, location, count, values);
    g_origUniform4fv(location, count, values);
}

void hkUniformMatrix4fv(GLint location, GLsizei count, GLboolean transpose, const GLfloat* values) {
    if (!transpose) noteUniform(16, location, count, values);
    g_origUniformMatrix4fv(location, count, transpose, values);
}

void hkBindTexture(GLenum target, GLuint texture) {
    if (target == GL_TEXTURE_2D && tracked() && g_synced) {
        const GLuint unit = g_shadow.activeTexture - GL_TEXTURE0;
        if (unit < kUnits) {
            g_textures2D[unit] = texture;
            g_texturesKnown[unit] = true;
        }
    }
    g_origBindTexture(target, texture);
}

void hkBindSampler(GLuint unit, GLuint sampler) {
    if (unit < kUnits && tracked()) {
        g_samplers[unit] = sampler;
        g_samplersKnown[unit] = true;
    }
    g_origBindSampler(unit, sampler);
}

void syncShadow() {
    if (g_synced) return;
    g_shadow = queryGlState();
    g_synced = true;
}

void hkShaderSource(GLuint shader, GLsizei count, const GLchar* const* strings, const GLint* lengths) {
    if (!t_inside) spriteFxNoteSource(shader, count, strings, lengths);
    g_origShaderSource(shader, count, strings, lengths);
}

void hkAttachShader(GLuint program, GLuint shader) {
    if (!t_inside) spriteFxNoteAttach(program, shader);
    g_origAttachShader(program, shader);
}

void hkDrawArrays(GLenum mode, GLint first, GLsizei count) {
    if (tracked()) noteDraw();
    g_origDrawArrays(mode, first, count);
}

int g_frame = 0;

EGLBoolean hkSwap(EGLDisplay display, EGLSurface surface) {
    if (!unityThread()) return g_origSwap(display, surface);

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
    BL_ERROR("screen effects: %s not hooked", name);
    return false;
}

}

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
        ok &= hookGl("glDrawElements", reinterpret_cast<void*>(g.DrawElements), hkDrawElements, &g_origDrawElements);
        ok &= hookGl("glDrawArrays", reinterpret_cast<void*>(g.DrawArrays), hkDrawArrays, &g_origDrawArrays);
        ok &= hookGl("glShaderSource", reinterpret_cast<void*>(g.ShaderSource), hkShaderSource, &g_origShaderSource);
        ok &= hookGl("glAttachShader", reinterpret_cast<void*>(g.AttachShader), hkAttachShader, &g_origAttachShader);
        ok &= hookGl("glUniform1fv", reinterpret_cast<void*>(g.Uniform1fv), hkUniform1fv, &g_origUniform1fv);
        ok &= hookGl("glUniform2fv", reinterpret_cast<void*>(g.Uniform2fv), hkUniform2fv, &g_origUniform2fv);
        ok &= hookGl("glUniform3fv", reinterpret_cast<void*>(g.Uniform3fv), hkUniform3fv, &g_origUniform3fv);
        ok &= hookGl("glUniform4fv", reinterpret_cast<void*>(g.Uniform4fv), hkUniform4fv, &g_origUniform4fv);
        ok &= hookGl("glUniformMatrix4fv", reinterpret_cast<void*>(g.UniformMatrix4fv), hkUniformMatrix4fv, &g_origUniformMatrix4fv);
        ok &= hookGl("glBindTexture", reinterpret_cast<void*>(g.BindTexture), hkBindTexture, &g_origBindTexture);
        ok &= hookGl("glBindSampler", reinterpret_cast<void*>(g.BindSampler), hkBindSampler, &g_origBindSampler);
        BL_INFO("screen effects: GL hooks %s", ok ? "ok" : "failed");
    });
}

GLenum shadowActiveTexture() {
    syncShadow();
    return g_shadow.activeTexture;
}

GLuint shadowTexture2D(GLuint unit) {
    syncShadow();
    if (unit >= kUnits) return 0;

    if (!g_texturesKnown[unit]) {
        auto& g = gl();
        const bool inside = t_inside;
        t_inside = true;

        GLint value = 0;
        g.ActiveTexture(GL_TEXTURE0 + unit);
        g.GetIntegerv(GL_TEXTURE_BINDING_2D, &value);
        g.ActiveTexture(g_shadow.activeTexture);

        t_inside = inside;
        g_textures2D[unit] = static_cast<GLuint>(value);
        g_texturesKnown[unit] = true;
    }

    return g_textures2D[unit];
}

GLuint shadowSampler(GLuint unit) {
    syncShadow();
    if (unit >= kUnits) return 0;

    if (!g_samplersKnown[unit]) {
        auto& g = gl();
        const bool inside = t_inside;
        t_inside = true;

        GLint value = 0;
        g.ActiveTexture(GL_TEXTURE0 + unit);
        g.GetIntegerv(GL_SAMPLER_BINDING, &value);
        g.ActiveTexture(g_shadow.activeTexture);

        t_inside = inside;
        g_samplers[unit] = static_cast<GLuint>(value);
        g_samplersKnown[unit] = true;
    }

    return g_samplers[unit];
}

void fillGlHookStats(RenderStats& stats) {
    stats.shadowChecks = g_shadowChecks.load();
    stats.shadowMismatches = g_shadowMismatches.load();
}

}
