#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

constexpr int kVanillaBuffCount = 401;

struct ModBuffDef {
    std::string mod;
    std::string name;
    std::string texture;
    std::vector<std::pair<std::string, std::string>> names;
    std::vector<std::pair<std::string, std::string>> descriptions;
};

int registerModBuff(ModBuffDef def);

void setModBuffTexts(int type,
                     std::vector<std::pair<std::string, std::string>> names,
                     std::vector<std::pair<std::string, std::string>> descriptions);

bool isModBuff(int type);

int buffTypeCount();

void tickModBuffs();

bool modBuffsSettled();

using BuffsInstalledHook = void (*)(int first, int last);
void setBuffsInstalledHook(BuffsInstalledHook hook);

int modBuffTypeByName(const std::string& mod, const std::string& name);

std::string modBuffKey(int type);

struct ModBuffInfo {
    int type;
    std::string mod, name, texture;
};

std::vector<ModBuffInfo> modBuffs();

int modBuffTypeByKey(const std::string& key);

struct SavedBuff {
    int slot = 0;
    int time = 0;
    std::string key;
};

std::vector<SavedBuff> collectModBuffs(Il2CppObject* player);

std::vector<SavedBuff> restoreModBuffs(Il2CppObject* player, const std::vector<SavedBuff>& saved);

}
