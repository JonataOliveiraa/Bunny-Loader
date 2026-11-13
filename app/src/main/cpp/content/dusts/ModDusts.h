#pragma once
#include <cstdint>
#include <string>

#include "il2cpp/Types.h"

namespace bl::runtime {

constexpr int kVanillaDustCount = 332;

enum DustHook : unsigned {
    kDustOnSpawn = 1u << 0,
    kDustUpdate = 1u << 1,
    kDustMidUpdate = 1u << 2,
    kDustGetAlpha = 1u << 3,
    kDustPreDraw = 1u << 4,
};

struct ModDustDef {
    std::string mod;
    std::string name;
    std::string texture;
    unsigned hooks = 0;
};

int registerModDust(ModDustDef def);

void setModDustUpdateType(int type, int updateType);

Il2CppObject* modDustTextureAsset(int type);

struct DustNativeCallbacks {
    void (*spawn)(int32_t type, void* dust) = nullptr;
    void (*update)(void** dusts, int32_t count, uint8_t* flags) = nullptr;
    void (*midUpdate)(void** dusts, int32_t count, uint8_t* flags) = nullptr;
    void (*preDraw)(void** dusts, int32_t count, uint8_t* flags) = nullptr;
    int32_t (*getAlpha)(void* dust, uint32_t* color) = nullptr;
};
void setDustNativeCallbacks(const DustNativeCallbacks& callbacks);

using DustsInstalledHook = void (*)(int first, int last);
void addDustsInstalledHook(DustsInstalledHook hook);

void tickModDusts();
bool modDustsSettled();

}
