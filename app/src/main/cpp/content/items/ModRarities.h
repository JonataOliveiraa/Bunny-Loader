#pragma once
#include <cstdint>

namespace bl::runtime {

constexpr int32_t kVanillaRarityCount = 12;

int32_t allocateModRarity();
int32_t modRarityCount();

}
