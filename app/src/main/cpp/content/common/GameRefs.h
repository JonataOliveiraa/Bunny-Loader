#pragma once
#include "il2cpp/Api.h"

namespace bl::runtime {

struct EntityRefs {
    int32_t whoAmI   = -1;
    int32_t position = -1;
    int32_t velocity = -1;
    int32_t width    = -1;
    int32_t height   = -1;
};

struct ProjectileRefs {
    Il2CppClass* cls = nullptr;
    int32_t active   = -1;
    int32_t rotation = -1;
    int32_t type     = -1;
    int32_t owner    = -1;
    int32_t timeLeft = -1;
    int32_t damage   = -1;
    const MethodInfo* setDefaults = nullptr;
    const MethodInfo* aiMethod    = nullptr;
    const MethodInfo* kill        = nullptr;
    const MethodInfo* update      = nullptr;
};

struct GameRefs {
    EntityRefs entity;
    ProjectileRefs proj;
};

GameRefs& game();

bool resolveGameRefs();

template <typename T>
inline T& field(Il2CppObject* obj, int32_t offset) {
    return *reinterpret_cast<T*>(reinterpret_cast<char*>(obj) + offset);
}

}
