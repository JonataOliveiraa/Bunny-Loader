#pragma once
#include "il2cpp/Api.h"

namespace bl::hook {

bool install(void* address, void* replacement, void** original);
bool install(const MethodInfo* method, void* replacement, void** original);
void removeAll();

// IMPORTANTE: todo método hookado do IL2CPP recebe um `const MethodInfo*` como
// ÚLTIMO argumento. A assinatura da função de substituição precisa refletir isso.
template <typename Fn>
bool install(const MethodInfo* method, Fn replacement, Fn* original) {
    return install(method, reinterpret_cast<void*>(replacement),
                   reinterpret_cast<void**>(original));
}

} // namespace bl::hook
