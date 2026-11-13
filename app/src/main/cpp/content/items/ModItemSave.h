#pragma once
#include <string>
#include "il2cpp/Types.h"

namespace bl::runtime {

void installModItemSave();
Il2CppObject* expandChestItem(int chestIndex, int slot);
bool storeChestItemData(int chestIndex, int slot, Il2CppObject* item);

}
