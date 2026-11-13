#pragma once

#include <cstdint>

namespace bl::runtime {

void installModNpcSave();

using ManagedNpcNeeds = int32_t (*)(void* npc);
using ManagedNpcSave = int32_t (*)(void* npc, void* sink);
using ManagedNpcLoad = void (*)(void* npc, const char* data);
void setManagedNpcData(ManagedNpcNeeds needs, ManagedNpcSave save, ManagedNpcLoad load);

}
