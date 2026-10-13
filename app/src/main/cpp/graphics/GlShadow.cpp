#include "graphics/GlShadow.h"

#include <cstdio>
#include <cstring>

namespace bl::graphics {

GlShadow queryGlState() {
    auto& g = gl();
    GlShadow s;
    GLint v = 0;
    g.GetIntegerv(GL_CURRENT_PROGRAM, &v);
    s.program = static_cast<GLuint>(v);
    g.GetIntegerv(GL_VERTEX_ARRAY_BINDING, &v);
    s.vertexArray = static_cast<GLuint>(v);
    g.GetIntegerv(GL_DRAW_FRAMEBUFFER_BINDING, &v);
    s.drawFbo = static_cast<GLuint>(v);
    g.GetIntegerv(GL_READ_FRAMEBUFFER_BINDING, &v);
    s.readFbo = static_cast<GLuint>(v);
    g.GetIntegerv(GL_ACTIVE_TEXTURE, &v);
    s.activeTexture = static_cast<GLenum>(v);
    g.GetIntegerv(GL_VIEWPORT, s.viewport);
    s.blend = g.IsEnabled(GL_BLEND);
    s.depth = g.IsEnabled(GL_DEPTH_TEST);
    s.stencil = g.IsEnabled(GL_STENCIL_TEST);
    s.cull = g.IsEnabled(GL_CULL_FACE);
    s.scissor = g.IsEnabled(GL_SCISSOR_TEST);
    g.GetBooleanv(GL_COLOR_WRITEMASK, s.colorMask);
    return s;
}

int compareGlState(const GlShadow& a, const GlShadow& b, char* diff, size_t size) {
    int count = 0;
    size_t used = 0;
    diff[0] = 0;
    auto note = [&](bool same, const char* name) {
        if (same) return;
        count++;
        if (used < size) used += snprintf(diff + used, size - used, "%s ", name);
    };
    note(a.program == b.program, "programa");
    note(a.vertexArray == b.vertexArray, "vao");
    note(a.drawFbo == b.drawFbo, "fbo-desenho");
    note(a.readFbo == b.readFbo, "fbo-leitura");
    note(a.activeTexture == b.activeTexture, "textura-ativa");
    note(std::memcmp(a.viewport, b.viewport, sizeof a.viewport) == 0, "viewport");
    note(a.blend == b.blend, "blend");
    note(a.depth == b.depth, "depth");
    note(a.stencil == b.stencil, "stencil");
    note(a.cull == b.cull, "cull");
    note(a.scissor == b.scissor, "scissor");
    note(std::memcmp(a.colorMask, b.colorMask, sizeof a.colorMask) == 0, "colorMask");
    return count;
}

} // namespace bl::graphics
