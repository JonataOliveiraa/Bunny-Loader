#pragma once

#include <string>

namespace bl::runtime {

int reserveBossHead(const std::string& texturePath, const std::string& assetName, int npcType);

int bossHeadSlotOf(const std::string& texturePath);

void installBossHeads();

void watchBossHeads();

}
