#pragma once
#include <cstdint>

namespace bl::graphics {

int32_t screenShader(const char* name, const char* source);
int32_t screenShaderStatus(int32_t id, char* error, int32_t capacity);
int32_t screenSubmit(const uint8_t* data, int32_t length);
int32_t screenTexture(const char* path, int32_t linear, int32_t repeat, int32_t* width, int32_t* height);
int32_t screenSaveNeutralLut(const char* path, int32_t size);
int32_t screenStats(int64_t* out, int32_t capacity);

}
