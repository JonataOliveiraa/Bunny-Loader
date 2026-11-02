#pragma once

#include <string>

namespace bl::runtime {

bool callServerSync(const char* method, const char* signature, const std::string* arg, std::string* out);

}
