#pragma once
#include "graphics/RenderHook.h"
#include <GLES3/gl3.h>

namespace bl::graphics {

void installGlHooks();

void fillGlHookStats(RenderStats& stats);

GLenum shadowActiveTexture();

GLuint shadowTexture2D(GLuint unit);

GLuint shadowSampler(GLuint unit);

struct BlendShadow {
    bool enabled = false;
    GLint srcRgb = GL_ONE, dstRgb = GL_ZERO, srcAlpha = GL_ONE, dstAlpha = GL_ZERO;
    GLint equationRgb = GL_FUNC_ADD, equationAlpha = GL_FUNC_ADD;
};

BlendShadow shadowBlend();

}
