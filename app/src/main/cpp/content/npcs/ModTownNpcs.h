#pragma once

#include <string>

namespace bl::runtime {

int setModNpcHead(int type, int variant, const std::string& texturePath, const std::string& assetName);

int modNpcHeadSlot(int type, int variant = 0);

void prepareTownNpcs(int totalTypes);

void installTownNpcs(int totalTypes);

void watchTownNpcs();

}
