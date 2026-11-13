#pragma once
#include <string>
#include <vector>

namespace bl::mods {

std::string manifestString(const std::string& txt, const char* key);

std::vector<std::string> manifestStrings(const std::string& txt, const char* key);

}
