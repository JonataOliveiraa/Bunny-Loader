#pragma once
#include "graphics/GlApi.h"
#include <cstddef>

namespace bl::graphics {

/**
 * O estado GL da Unity que os efeitos mexem, espelhado pelos hooks GL
 * (GlHooks.cpp) em vez de perguntado ao driver: os glGet* de cada quadro
 * custavam meio milissegundo de CPU na thread de render do celular.
 *
 * Os hooks entram depois de a Unity ja ter ligado coisas, entao o espelho e
 * sincronizado com glGet* uma vez e conferido nos primeiros desenhos; uma
 * diferenca que seja volta para o glGet* de vez (GlHooks.cpp, syncShadow).
 */
struct GlShadow {
    GLuint program = 0, vertexArray = 0, drawFbo = 0, readFbo = 0;
    GLenum activeTexture = GL_TEXTURE0;
    GLint viewport[4] = {0, 0, 0, 0};
    bool blend = false, depth = false, stencil = false, cull = false, scissor = false;
    GLboolean colorMask[4] = {GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE};
};

// O estado de verdade, pelos glGet* do driver.
GlShadow queryGlState();

// Quantos campos diferem; os nomes deles em `diff`.
int compareGlState(const GlShadow& a, const GlShadow& b, char* diff, size_t size);

} // namespace bl::graphics
