#pragma once
#include "core/Config.h"

namespace bl::runtime {

void prepareTexturePacks(const std::string& root, const std::vector<ModSpec>& enabled);
void configureTexturePacks(const std::string& root, const std::vector<ModSpec>& enabled);
void tickTexturePacks();
bool texturePacksBusy();

}
