#pragma once

#include <cstdint>

namespace bl::runtime {

using DrawAdjustFn = void (*)(int32_t kind, float* position, int32_t* effects);
using DrawTintFn = void (*)(uint32_t* colors, int32_t count);

bool beginProjectileDrawSplit(void* ownTexture, bool extras, bool sprite);
void endProjectileDrawSplit();
bool beginDrawAdjust(void* texture, int32_t kind, DrawAdjustFn fn);
void endDrawAdjust();
bool beginDrawTint(void* texture, DrawTintFn fn);
void endDrawTint();

}
