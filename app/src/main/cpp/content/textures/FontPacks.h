#pragma once
#include "core/Config.h"
#include "il2cpp/Types.h"

namespace bl::runtime {

void configureFontPacks(const std::string& root, const std::vector<ModSpec>& enabled);
void tickFontPacks();

Il2CppObject* loadModFont(const std::string& path);

}
