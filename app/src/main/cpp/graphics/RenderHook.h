#pragma once
#include "graphics/EffectTypes.h"
#include <cstdint>

namespace bl::graphics {

void installRenderHook();

constexpr int kMarkerWidth = 17;
constexpr int kMarkerHeight = 3;

void submitEffects(EffectFrame&& frame);

bool takeWorldEffects(EffectFrame& out);
bool takeScreenEffects(EffectFrame& out);
void noteMarkerHit();

struct RenderStats {
    int64_t worldPostRender = 0, uiPostRender = 0, otherPostRender = 0;
    int64_t draws = 0, frames = 0, markersIssued = 0, markerHits = 0, screenFrames = 0;
    int64_t renderCpuNs = 0, markerCpuNs = 0, gpuNs = 0, gpuSamples = 0;
    int64_t gpuFrameNs = 0, gpuFrameSamples = 0;
    int64_t shadowChecks = 0, shadowMismatches = 0;
    int64_t boxPixels = 0;
    int32_t drawFbo = -1, readFbo = -1, viewport[4] = {0, 0, 0, 0};
    int32_t samples = -1, blitError = 0, copyFallbacks = 0;
    bool hookInstalled = false, hasContext = false, markerReady = false, gpuTimer = false;
};

RenderStats renderStats();

}
