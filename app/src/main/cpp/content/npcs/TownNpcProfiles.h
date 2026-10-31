#pragma once
#include <cstdint>

namespace bl::runtime {
using TownProfileCallback = int32_t (*)(int32_t kind, int32_t type, void* npc, intptr_t* result);
bool installTownProfiles(TownProfileCallback callback);
bool setTownProfileCarrier(int32_t type, bool onlyExisting);
}
