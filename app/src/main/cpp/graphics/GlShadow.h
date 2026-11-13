#pragma once
#include "graphics/GlApi.h"
#include <cstddef>

namespace bl::graphics {

struct GlShadow {
    GLuint program = 0, vertexArray = 0, drawFbo = 0, readFbo = 0;
    GLenum activeTexture = GL_TEXTURE0;
    GLint viewport[4] = {0, 0, 0, 0};
    bool blend = false, depth = false, stencil = false, cull = false, scissor = false;
    GLboolean colorMask[4] = {GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE};
};

GlShadow queryGlState();

int compareGlState(const GlShadow& a, const GlShadow& b, char* diff, size_t size);

}
