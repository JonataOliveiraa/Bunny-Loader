#include "hook/HookManager.h"
#include "core/Log.h"
#include "shadowhook.h"
#include <vector>

namespace bl::hook {

namespace {
std::vector<void*>& stubs() {
    static std::vector<void*> list;
    return list;
}
}

bool install(void* address, void* replacement, void** original) {
    void* stub = shadowhook_hook_func_addr(address, replacement, original);
    if (!stub) {
        BL_ERROR("Hook falhou em %p: %s", address,
                 shadowhook_to_errmsg(shadowhook_get_errno()));
        return false;
    }
    stubs().push_back(stub);
    return true;
}

bool install(const MethodInfo* method, void* replacement, void** original) {
    if (!method) return false;
    return install(il2cpp::methodPointer(method), replacement, original);
}

void removeAll() {
    for (void* stub : stubs()) shadowhook_unhook(stub);
    stubs().clear();
}

} // namespace bl::hook
