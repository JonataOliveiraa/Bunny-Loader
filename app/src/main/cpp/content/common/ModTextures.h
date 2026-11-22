#pragma once
#include <cstddef>
#include <string>

#include "il2cpp/Types.h"

namespace bl::runtime::content {

void prefetchModTexture(const std::string& file, const std::string& assetName);
Il2CppObject* modTexture(const std::string& file, const std::string& assetName);
Il2CppObject* modTextureAsset(const std::string& file, const unsigned char* data, size_t size,
                              const std::string& assetName, int* width, int* height);
Il2CppObject* loadedModTexture(const std::string& assetName);
void pumpModTextures(int budgetMicros);
void prefetchWarmTextures(const std::string& path, const std::string& key);
void finishTextureWarmList();

}
