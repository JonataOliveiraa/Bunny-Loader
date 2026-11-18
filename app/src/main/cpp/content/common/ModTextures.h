#pragma once
#include <string>

#include "il2cpp/Types.h"

namespace bl::runtime::content {

void prefetchModTexture(const std::string& file, const std::string& assetName);
Il2CppObject* modTexture(const std::string& file, const std::string& assetName);
Il2CppObject* loadedModTexture(const std::string& assetName);
void pumpModTextures(int budgetMicros);

}
