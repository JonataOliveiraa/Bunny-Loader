#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace bl {

struct Config {
    std::string gameLibDir;
    std::string modsDir;
    std::vector<std::string> enabledMods;
    std::string logPath;
    int64_t gameVersion = 0;
};

inline Config& config() {
    static Config instance;
    return instance;
}

} // namespace bl
