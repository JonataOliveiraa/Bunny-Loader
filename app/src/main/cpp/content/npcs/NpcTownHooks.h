#pragma once
#include <cstdint>

namespace bl::runtime {
using NpcTownCallback = void (*)(int32_t, void*, void*);
bool installNpcTownHooks(int32_t type, uint32_t flags, NpcTownCallback callback);
}
