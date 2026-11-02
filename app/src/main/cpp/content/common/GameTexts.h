#pragma once

#include <string>

namespace bl::runtime {

void registerGameTexts();

std::string gameText(const std::string& key);

}
