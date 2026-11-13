#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

constexpr int kVanillaNpcCount = 697;

struct ModNpcDef {
    std::string mod;
    std::string name;
    std::string texture;
    int frames = 1;
    int animationType = 0;
    std::vector<std::pair<std::string, std::string>> names;
};

int registerModNpc(ModNpcDef def);

bool isModNpc(int type);

int npcTypeCount();

int registeredNpcTypeCount();

void prepareModNpcs();

using NpcsInstalledHook = void (*)(int first, int last);
void setNpcsInstalledHook(NpcsInstalledHook hook);

void tickModNpcs();

bool modNpcsSettled();

struct ModNpcInfo {
    int type;
    std::string mod, name, texture;
    int frames;
};

std::vector<ModNpcInfo> modNpcs();

void setModNpcFrames(int type, int frames);

void setModNpcAnimation(int type, int animationType);

bool ensureNpcFrameHook();
void runNpcFindFrame(Il2CppObject* npc, int borrowed);

int modNpcTypeByName(const std::string& mod, const std::string& name);

void finishModNpc(Il2CppObject* npc, int type, void* spawnParams = nullptr);

}
