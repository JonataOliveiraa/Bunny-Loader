#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace bl {

struct ModSpec {
    std::string id;
    std::string entry;
};

ModSpec parseModSpec(const std::string& texto);

struct Config {
    std::string gameLibDir;
    std::string modsDir;
    std::vector<ModSpec> enabledMods;
    std::string logPath;
    std::string cmdPath;
    bool showErrors = true;
    bool verboseLog = false;
    int64_t gameVersion = 0;
    std::string dotnetDir;
    bool fastBoot = false;
    std::string quickPlayer;
    std::string quickWorld;
    std::string joinAddress;
    int32_t joinPort = 0;
    std::string joinPassword;
    std::string joinPlayer;
    std::string gameTexts;
};

inline Config& config() {
    static Config instance;
    return instance;
}

bool loadConfigFromFile(const char* path);

constexpr const char* kBunnyBaseDir =
    "/sdcard/Android/data/com.and.games505.TerrariaPaid/files/bunny";
constexpr const char* kDefaultConfigPath =
    "/sdcard/Android/data/com.and.games505.TerrariaPaid/files/bunny/config";

}
