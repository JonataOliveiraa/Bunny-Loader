#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

constexpr int kVanillaItemCount = 6196;

struct ModItemDef {
    std::string mod;
    std::string name;
    std::string texture;
    const unsigned char* textureData = nullptr;
    size_t textureSize = 0;
    std::string flameTexture;
    std::vector<std::pair<std::string, std::string>> names;
    std::vector<std::pair<std::string, std::string>> tooltips;
};

int registerModItem(ModItemDef def);

bool isModItem(int type);

void setModItemTooltip(int type, std::vector<std::pair<std::string, std::string>> tooltips);

std::string modItemKey(int type);

int modItemTypeByKey(const std::string& key);

constexpr int kUnloadedPoolSize = 64;

void registerUnloadedPool();

bool isUnloadedType(int type);

int unloadedTypeFor(const std::string& key);

void setupUnloadedItem(Il2CppObject* item, int type);

int itemTypeCount();

void tickModItems();

bool modItemsSettled();

using ItemsInstalledHook = void (*)(int first, int last);
void addItemsInstalledHook(ItemsInstalledHook hook);

void prepareModItem(Il2CppObject* item, int type);

void finishModItem(Il2CppObject* item, int type);

struct ModItemInfo {
    int type;
    std::string mod, name, texture;
};

std::vector<ModItemInfo> modItems();

}
