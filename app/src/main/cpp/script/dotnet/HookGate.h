#pragma once
#include "il2cpp/Api.h"

#include <cstdint>

namespace bl::dotnet::gate {

void** installHook(const MethodInfo* method, void* replacement, int32_t arg, int32_t pointerOffset, int32_t fieldOffset, int32_t fieldSize);

} // namespace bl::dotnet::gate
