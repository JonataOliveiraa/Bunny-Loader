#pragma once
#include <string>

#include "content/common/ContentAssets.h"

namespace bl::runtime {

Il2CppObject* modLanguageText(const std::string& key, const content::CultureNames* names, bool replace);
void tickModLanguage();

}
