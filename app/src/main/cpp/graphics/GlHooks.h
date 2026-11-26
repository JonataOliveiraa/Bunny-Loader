#pragma once
#include "graphics/RenderHook.h"
#include <GLES3/gl3.h>

namespace bl::graphics {

void installGlHooks();

void fillGlHookStats(RenderStats& stats);

GLenum shadowActiveTexture();

GLuint shadowTexture2D(GLuint unit);

GLuint shadowSampler(GLuint unit);

}
