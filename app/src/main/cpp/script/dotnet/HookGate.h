#pragma once
#include "il2cpp/Api.h"

#include <cstdint>

namespace bl::dotnet::gate {

constexpr int32_t FlagInvert = 1;
constexpr int32_t FlagAlways = 2;
constexpr int32_t FlagValue = 4;
constexpr int32_t ArgAbsolute = 8;

struct Spec {
    int32_t arg;
    int32_t pointerOffset;
    int32_t fieldOffset;
    int32_t fieldSize;
    const uint8_t* bitmap;
    int32_t bitmapBits;
    int32_t flags;
    const void* address;
};

void** installHook(const MethodInfo* method, void* replacement, int32_t arg, int32_t pointerOffset, int32_t fieldOffset, int32_t fieldSize);

void** installHook(const MethodInfo* method, void* replacement, const Spec& spec, int32_t* id);

bool setAlways(int32_t id, bool always);

}
