#pragma once

#include <cstdint>

namespace bl::runtime {

using NaturalRegenCallback = float (*)(void* player, float regen);
using ConsumeBaitCallback = int32_t (*)(void* player, void* bait);
using DyeRewardCallback = int32_t (*)(void* player, int32_t* pool, int32_t count, int32_t capacity);

bool setNaturalRegenCallback(NaturalRegenCallback callback);
bool setConsumeBaitCallback(ConsumeBaitCallback callback);
bool setDyeRewardCallback(DyeRewardCallback callback);

}
