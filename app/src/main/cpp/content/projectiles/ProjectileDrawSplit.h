#pragma once

#include <cstdint>

namespace bl::runtime {

using DrawAdjustFn = void (*)(int32_t kind, float* position, int32_t* effects);

bool beginProjectileDrawSplit(void* ownTexture, bool extras, bool sprite);
void endProjectileDrawSplit();
bool beginDrawAdjust(void* texture, int32_t kind, DrawAdjustFn fn);
void endDrawAdjust();

}
