#pragma once

#include <cstdint>

namespace bl::runtime {

using DrawAdjustFn = void (*)(int32_t kind, float* position, int32_t* effects);
using DrawTintFn = void (*)(uint32_t* colors, int32_t count);

struct DrawCapture {
    void* texture;
    float x, y;
    int32_t hasSource, sx, sy, sw, sh;
    uint32_t color;
    float rotation, ox, oy, scaleX, scaleY;
    int32_t effects;
    float depth;
};

using DrawCaptureFn = int32_t (*)(DrawCapture* draw);

bool beginProjectileDrawSplit(void* ownTexture, bool extras, bool sprite);
void endProjectileDrawSplit();
bool beginDrawAdjust(void* texture, int32_t kind, DrawAdjustFn fn);
void endDrawAdjust();
bool beginDrawTint(void* texture, DrawTintFn fn);
void endDrawTint();
bool beginDrawCapture(void* texture, DrawCaptureFn fn);
void endDrawCapture();

}
