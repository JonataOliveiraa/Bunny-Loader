#pragma once
#include <string>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

constexpr int kVanillaWallCount = 367;

struct ModWallDef {
    std::string mod;
    std::string name;
    std::string texture;
};

int registerModWall(ModWallDef def);

bool isModWall(int type);

int wallTypeCount();

void tickModWalls();

bool modWallsSettled();

using WallsInstalledHook = void (*)(int first, int last);
void setWallsInstalledHook(WallsInstalledHook hook);

int modWallTypeByName(const std::string& mod, const std::string& name);

std::string modWallKey(int type);

int modWallTypeByKey(const std::string& key);

struct ModWallInfo {
    int type;
    std::string mod, name, texture;
};

std::vector<ModWallInfo> modWalls();

} // namespace bl::runtime
