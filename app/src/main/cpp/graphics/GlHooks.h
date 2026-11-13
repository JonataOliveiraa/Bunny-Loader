#pragma once
#include "graphics/RenderHook.h"
#include <string>

namespace bl::graphics {

void installGlHooks();

void requestGlTrace();

void fillGlHookStats(RenderStats& stats);

}
