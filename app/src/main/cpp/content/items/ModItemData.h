#pragma once
#include <cstdint>
#include <string>

#include "il2cpp/Types.h"

namespace bl::runtime {

using ManagedItemSave = int32_t (*)(void* item, const char* current, void* sink);
using ManagedItemLoad = int32_t (*)(void* item, const char* data);

void setManagedItemData(ManagedItemSave save, ManagedItemLoad load);
void markManagedItem(int type);
bool saveModItemData(Il2CppObject* item, std::string& data);
bool loadModItemData(Il2CppObject* item, const std::string& data);
void copyUnloadedItemData(Il2CppObject* from, Il2CppObject* to);

}
