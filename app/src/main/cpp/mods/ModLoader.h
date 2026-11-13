#pragma once
#include <cstdint>
#include <string>
#include <vector>

#include "core/Config.h"

namespace bl::mods {

struct LoadedMod {
    std::string id;
    std::string internalName;
    std::string displayName;
    std::string version;
    std::vector<std::string> dependencies;
    std::string dir;
    std::string entry;
    uint32_t errorCount = 0;
    bool enabled = true;
    bool loaded = false;
};

void loadAll(const std::string& modsDir, const std::vector<ModSpec>& enabled);

size_t loadedCount();

LoadedMod* get(uint16_t index);

const std::string& currentDir();

const std::string& dirOf(const std::string& id);

const std::string& currentId();

std::string rootOf(const std::string& id);

std::string displayName(const std::string& id);

const LoadedMod* find(const std::string& id);

constexpr uint32_t MaxErrors = 20;

}
