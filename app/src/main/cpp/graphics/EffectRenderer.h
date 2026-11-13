#pragma once
#include "graphics/EffectTypes.h"
#include "graphics/GlShadow.h"
#include "graphics/RenderHook.h"

namespace bl::graphics {

struct DrawTarget {
    int fbo = 0;
    int viewport[4] = {0, 0, 0, 0};
};

class EffectRenderer {
public:
    static void Execute(const EffectFrame& frame, const DrawTarget& target, const GlShadow& state);
    static void FillStats(RenderStats& stats);
};

}
