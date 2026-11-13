#pragma once
#include <string>
#include "il2cpp/Types.h"

namespace bl::runtime {

void installModItemSave();
std::string modChestItemData(Il2CppObject* chest, int slot);
bool setModChestItemData(Il2CppObject* chest, int slot, const std::string& data);
Il2CppObject* expandChestItem(int chestIndex, int slot);
bool storeChestItemData(int chestIndex, int slot, Il2CppObject* item);

}
