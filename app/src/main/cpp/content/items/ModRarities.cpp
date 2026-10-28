#include "content/items/ModRarities.h"

#include <atomic>

namespace bl::runtime {

namespace {

std::atomic<int32_t> g_count{0};

}

int32_t allocateModRarity() { return kVanillaRarityCount + g_count.fetch_add(1, std::memory_order_acq_rel); }

int32_t modRarityCount() { return kVanillaRarityCount + g_count.load(std::memory_order_acquire); }

}
