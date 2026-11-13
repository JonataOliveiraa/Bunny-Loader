#pragma once
#include <cstddef>
#include <string>
#include <vector>

namespace bl::mods {

struct OrderEntry {
    std::string uid;
    std::string id;
    std::vector<std::string> dependencies;
    bool usable = true;
};

struct OrderResult {
    std::vector<size_t> order;
    std::vector<std::string> errors;
    std::vector<std::vector<size_t>> needs;
};

OrderResult sortByDependencies(const std::vector<OrderEntry>& mods);

}
