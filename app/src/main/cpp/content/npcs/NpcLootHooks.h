#pragma once
#include <cstdint>

namespace bl::runtime {

using NpcLootCallback = int32_t (*)(int32_t kind, void* npc, int32_t* args);
bool installNpcLootHooks(uint32_t flags, NpcLootCallback callback);

}
