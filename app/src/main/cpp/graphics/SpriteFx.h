#pragma once
#include <GLES3/gl3.h>
#include <cstdint>

namespace bl::graphics {

void* spriteFxMaterial(void* source);

int32_t spriteFxRegister(void* pass, int32_t fxc, int32_t technique, int32_t passIndex, char* error, int32_t capacity);

int32_t spriteFxHasProperty(void* material, const char* name);

int32_t spriteFxStats(int64_t* out, int32_t count);

int32_t spriteFxSetTexture(void* effect, int32_t reg, void* texture);

bool spriteFxActive();

void spriteFxNoteSource(GLuint shader, GLsizei count, const GLchar* const* strings, const GLint* lengths);

void spriteFxNoteAttach(GLuint program, GLuint shader);

bool spriteFxDraw(GLuint program, GLenum mode, GLsizei count, GLenum type, const void* indices);

}
