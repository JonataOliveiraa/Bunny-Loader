#pragma once
#include "il2cpp/Api.h"

#include <chrono>

namespace bl::runtime {

int revealMapColumns(Il2CppObject* map, const MethodInfo* updateLighting, int x, int maxX,
                     int maxY, std::chrono::steady_clock::time_point until, bool* threw);

}
