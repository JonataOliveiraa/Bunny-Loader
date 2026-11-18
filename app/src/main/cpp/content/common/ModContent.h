#pragma once
#include <cstdint>

namespace bl::runtime {

using ContentReadyHook = int32_t (*)();
void addContentReadyHook(ContentReadyHook hook);

using FrameHook = int32_t (*)(int32_t budgetMicros);
void addFrameHook(FrameHook hook);
void tickContentReady();

bool contentSettled();
bool contentFinished();

}
