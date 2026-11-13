#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime::content {

Il2CppObject* loadTextureAsset(const std::string& path, const unsigned char* data, size_t size,
                               const std::string& assetName, int* width, int* height);

bool isVanillaTexturePath(const std::string& path);

Il2CppObject* missingTextureAsset(int* width, int* height);

Il2CppObject* loadGameTexture(const std::string& path, const unsigned char* data, size_t size,
                              const std::string& what, int filterMode, int* width, int* height);
Il2CppObject* loadPackTexture(const std::string& path, int* width, int* height);
void destroyGameTexture(Il2CppObject* texture);

bool textureAssetSize(Il2CppObject* asset, int* width, int* height);
std::string textureAssetName(Il2CppObject* asset);
Il2CppObject* textureAssetValue(Il2CppObject* asset);
Il2CppObject* textureAssetSource(Il2CppObject* asset);
int textureAssetState(Il2CppObject* asset);
bool replaceTextureAssetValue(Il2CppObject* asset, Il2CppObject* value, Il2CppObject* source, int state = 2);
std::vector<Il2CppObject*> textureAssets();

std::string normalizeTextureAssetName(std::string name);

Il2CppObject* vanillaTextureAsset(const std::string& name, bool* missingOnMobile);

using CultureNames = std::vector<std::pair<std::string, std::string>>;

std::string textForCulture(const CultureNames& names, const std::string& fallback);

std::string activeCultureName();
Il2CppObject* newLocalizedText(const std::string& key, const std::string& text);
void addLanguageKey(const std::string& key, Il2CppObject* text);
Il2CppObject* findLanguageText(const std::string& key);
bool setLocalizedTextValue(Il2CppObject* text, const std::string& value);
std::string localizedTextValue(Il2CppObject* text);
void invalidateItemTooltips();

bool setTableElement(FieldInfo* table, int index, Il2CppObject* value);

}
