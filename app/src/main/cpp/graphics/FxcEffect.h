#pragma once
#include <cstdint>
#include <string>

namespace bl::graphics {

int32_t fxcParamCount(int32_t id);

int32_t fxcParamFloats(int32_t id, int32_t index);

bool fxcSpriteSource(int32_t id, int32_t technique, int32_t pass, std::string& out, std::string& error, int32_t& registers, int32_t& images);

int32_t fxcLoad(const uint8_t* data, int32_t length, char* error, int32_t capacity);
int32_t fxcInfo(int32_t id, char* out, int32_t capacity);
int32_t fxcPassSource(int32_t id, int32_t technique, int32_t pass, char* out, int32_t capacity);
int32_t fxcSetParam(int32_t id, int32_t index, const float* values, int32_t count);
int32_t fxcCommit(int32_t id, int32_t technique, int32_t pass, float* out, int32_t capacity);
int32_t fxcFree(int32_t id);

}
