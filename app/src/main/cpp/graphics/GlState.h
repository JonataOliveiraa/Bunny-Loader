#pragma once
#include "graphics/GlApi.h"
#include "graphics/GlShadow.h"

namespace bl::graphics {

/**
 * Devolve, na saida, o estado da Unity dado pelo espelho (GlShadow.h): sem
 * glGet*, e so o que um efeito muda. As texturas e samplers que os efeitos
 * ligam ficam nas unidades do fim (kScreenUnit...), que a Unity nao usa.
 */
class GlRestore {
public:
    explicit GlRestore(const GlShadow& state) : s_(state) {}

    ~GlRestore() {
        auto& g = gl();
        if (s_.drawFbo == s_.readFbo) {
            g.BindFramebuffer(GL_FRAMEBUFFER, s_.drawFbo);
        } else {
            g.BindFramebuffer(GL_DRAW_FRAMEBUFFER, s_.drawFbo);
            g.BindFramebuffer(GL_READ_FRAMEBUFFER, s_.readFbo);
        }
        g.Viewport(s_.viewport[0], s_.viewport[1], s_.viewport[2], s_.viewport[3]);
        g.UseProgram(s_.program);
        g.BindVertexArray(s_.vertexArray);
        g.ActiveTexture(s_.activeTexture);
        if (!(s_.colorMask[0] && s_.colorMask[1] && s_.colorMask[2] && s_.colorMask[3])) {
            g.ColorMask(s_.colorMask[0], s_.colorMask[1], s_.colorMask[2], s_.colorMask[3]);
        }
        if (s_.blend) g.Enable(GL_BLEND);
        if (s_.depth) g.Enable(GL_DEPTH_TEST);
        if (s_.stencil) g.Enable(GL_STENCIL_TEST);
        if (s_.cull) g.Enable(GL_CULL_FACE);
        if (s_.scissor) g.Enable(GL_SCISSOR_TEST);
    }

    GlRestore(const GlRestore&) = delete;
    GlRestore& operator=(const GlRestore&) = delete;

private:
    GlShadow s_;
};

// As unidades de textura dos efeitos, do fim para o comeco (a Unity usa as
// primeiras): 15 a copia da tela (uScreen), 14 o uSource, 13 a 8 os
// sampler2D do mod. Nao voltam ao estado da Unity (GlRestore).
constexpr GLenum kScreenUnit = GL_TEXTURE0 + 15;
constexpr GLenum kSourceUnit = GL_TEXTURE0 + 14;
constexpr GLenum kFirstModUnit = GL_TEXTURE0 + 13;
constexpr int kModUnits = 6;

/**
 * O estado GL que a Unity deixou, perguntado ao driver na entrada e devolvido
 * na saida. So nos caminhos raros (montar programa, VAO e textura): no de todo
 * quadro vai o GlRestore.
 *
 * A Unity guarda em cache o que ja mandou ao driver (programa, texturas,
 * framebuffer, viewport...) e nao repete o que acha que ja esta ligado. Um
 * efeito que deixasse algo diferente faria o resto do quadro sair com o
 * estado errado. So o que os efeitos mexem esta aqui.
 */
class GlStateGuard {
public:
    GlStateGuard() {
        auto& g = gl();
        g.GetIntegerv(GL_CURRENT_PROGRAM, &program_);
        g.GetIntegerv(GL_ACTIVE_TEXTURE, &activeTexture_);
        g.ActiveTexture(GL_TEXTURE0);
        g.GetIntegerv(GL_TEXTURE_BINDING_2D, &texture0_);
        g.GetIntegerv(GL_SAMPLER_BINDING, &sampler0_);
        g.GetIntegerv(GL_ARRAY_BUFFER_BINDING, &arrayBuffer_);
        g.GetIntegerv(GL_PIXEL_UNPACK_BUFFER_BINDING, &unpackBuffer_);
        g.GetIntegerv(GL_VERTEX_ARRAY_BINDING, &vertexArray_);
        g.GetIntegerv(GL_DRAW_FRAMEBUFFER_BINDING, &drawFbo);
        g.GetIntegerv(GL_READ_FRAMEBUFFER_BINDING, &readFbo);
        g.GetIntegerv(GL_VIEWPORT, viewport);
        g.GetBooleanv(GL_COLOR_WRITEMASK, colorMask_);
        blend_ = g.IsEnabled(GL_BLEND);
        depth_ = g.IsEnabled(GL_DEPTH_TEST);
        stencil_ = g.IsEnabled(GL_STENCIL_TEST);
        cull_ = g.IsEnabled(GL_CULL_FACE);
        scissor_ = g.IsEnabled(GL_SCISSOR_TEST);
    }

    ~GlStateGuard() {
        auto& g = gl();
        g.BindFramebuffer(GL_DRAW_FRAMEBUFFER, drawFbo);
        g.BindFramebuffer(GL_READ_FRAMEBUFFER, readFbo);
        g.Viewport(viewport[0], viewport[1], viewport[2], viewport[3]);
        g.UseProgram(program_);
        g.BindVertexArray(vertexArray_);
        g.BindBuffer(GL_ARRAY_BUFFER, arrayBuffer_);
        g.BindBuffer(GL_PIXEL_UNPACK_BUFFER, unpackBuffer_);
        g.ActiveTexture(GL_TEXTURE0);
        g.BindTexture(GL_TEXTURE_2D, texture0_);
        g.BindSampler(0, sampler0_);
        g.ActiveTexture(activeTexture_);
        g.ColorMask(colorMask_[0], colorMask_[1], colorMask_[2], colorMask_[3]);
        set(GL_BLEND, blend_);
        set(GL_DEPTH_TEST, depth_);
        set(GL_STENCIL_TEST, stencil_);
        set(GL_CULL_FACE, cull_);
        set(GL_SCISSOR_TEST, scissor_);
    }

    GlStateGuard(const GlStateGuard&) = delete;
    GlStateGuard& operator=(const GlStateGuard&) = delete;

    GLint drawFbo = 0;
    GLint readFbo = 0;
    GLint viewport[4] = {0, 0, 0, 0};

private:
    static void set(GLenum cap, GLboolean on) {
        if (on) gl().Enable(cap);
        else gl().Disable(cap);
    }

    GLint program_ = 0, activeTexture_ = GL_TEXTURE0, texture0_ = 0, sampler0_ = 0;
    GLint arrayBuffer_ = 0, unpackBuffer_ = 0, vertexArray_ = 0;
    GLboolean colorMask_[4] = {GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE};
    GLboolean blend_ = GL_FALSE, depth_ = GL_FALSE, stencil_ = GL_FALSE, cull_ = GL_FALSE, scissor_ = GL_FALSE;
};

} // namespace bl::graphics
