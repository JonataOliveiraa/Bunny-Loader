#pragma once
#include <string>

namespace bl::runtime {

int vanillaPrefixCount();

int registerModPrefix(const std::string& mod, const std::string& name);

int modPrefixCount();

std::string modPrefixKey(int id);

int modPrefixByKey(const std::string& key);

}
