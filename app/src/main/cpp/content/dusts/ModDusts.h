#pragma once
#include <string>
#include <vector>

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
    int engine = 0;
};

int registerModDust(ModDustDef def);

bool isModDust(int type);
int modDustTypeByName(const std::string& mod, const std::string& name);

void setModDustUpdateType(int type, int updateType);

Il2CppObject* modDustTextureAsset(int type);

struct DustCallbacks {
    void (*spawn)(int type, Il2CppObject* dust) = nullptr;
    void (*update)(const std::vector<Il2CppObject*>& dusts, std::vector<bool>& vanilla) = nullptr;
    void (*midUpdate)(const std::vector<Il2CppObject*>& dusts, std::vector<bool>& keepSpeed) = nullptr;
    void (*preDraw)(const std::vector<Il2CppObject*>& dusts, std::vector<bool>& draw) = nullptr;
    bool (*getAlpha)(Il2CppObject* dust, uint32_t& color) = nullptr;
};
void setDustCallbacks(const DustCallbacks& callbacks);

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

struct DustPoolStats {
    int active = 0;
    int activeCount = 0;
    int freeCount = 0;
    int repairs = 0;
    int modActive = 0;
    int64_t updateNs = 0, drawNs = 0, jsNs = 0;
    int64_t updates = 0, draws = 0;
    int64_t groupPasses = 0, groupDusts = 0;
};
DustPoolStats dustPoolStats();

bool repairDustPool();

}
