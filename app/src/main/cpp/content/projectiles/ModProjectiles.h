#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

constexpr int kVanillaProjectileCount = 1136;

struct ModProjectileDef {
    std::string mod;
    std::string name;
    std::string texture;
    int frames = 1;
    std::vector<std::pair<std::string, std::string>> names;
};

int registerModProjectile(ModProjectileDef def);

bool isModProjectile(int type);

int projectileTypeCount();

void tickModProjectiles();

bool modProjectilesSettled();

void finishModProjectile(Il2CppObject* projectile, int type);

using ProjectilesInstalledHook = void (*)(int first, int last);
void setProjectilesInstalledHook(ProjectilesInstalledHook hook);

void setModProjectileFrames(int type, int frames);

int modProjectileTypeByName(const std::string& mod, const std::string& name);

}
