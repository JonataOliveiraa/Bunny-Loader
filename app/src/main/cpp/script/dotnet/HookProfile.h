#pragma once
#include "il2cpp/Api.h"

#include <cstdint>

namespace bl::dotnet::profile {

bool enabled();

void** installHook(const MethodInfo* method, void* replacement);

void* wrapCallback(void* callback, const char* name);

template <class F>
F wrap(F callback, const char* name) {
    if (!callback || !enabled()) return callback;
    return reinterpret_cast<F>(wrapCallback(reinterpret_cast<void*>(callback), name));
}

int32_t read(int32_t index, uint64_t* values, const char** name);

} // namespace bl::dotnet::profile
