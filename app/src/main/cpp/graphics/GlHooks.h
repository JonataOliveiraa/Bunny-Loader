#pragma once
#include "graphics/RenderHook.h"

namespace bl::graphics {

void installGlHooks();

void fillGlHookStats(RenderStats& stats);

}
