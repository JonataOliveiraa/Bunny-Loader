#pragma once
#include <cstdint>

namespace bl::runtime {

using ContentReadyHook = int32_t (*)();
void addContentReadyHook(ContentReadyHook hook);
void tickContentReady();

bool contentSettled();
bool contentFinished();

}
