#include "hook/HookManager.h"
#include "core/Log.h"
#include "shadowhook.h"
#include <mutex>
#include <vector>

namespace bl::hook {

namespace {

struct Chain {
    void* address = nullptr;
    void* stub = nullptr;
    std::vector<void**> originals;
};

std::mutex g_mutex;

std::vector<Chain>& chains() {
    static std::vector<Chain> list;
    return list;
}

}

bool install(void* address, void* replacement, void** original) {
    if (!address || !replacement || !original) return false;
    std::lock_guard<std::mutex> guard(g_mutex);

    for (Chain& c : chains()) {
        if (c.address != address) continue;
        void** last = c.originals.back();
        __atomic_store_n(original, __atomic_load_n(last, __ATOMIC_ACQUIRE), __ATOMIC_RELEASE);
        __atomic_store_n(last, replacement, __ATOMIC_RELEASE);
        c.originals.push_back(original);
        BL_DEBUG("chained hook at %p (%zu on the same method)", address, c.originals.size());
        return true;
    }

    void* stub = shadowhook_hook_func_addr(address, replacement, original);
    if (!stub) {
        BL_ERROR("Hook failed at %p: %s", address,
                 shadowhook_to_errmsg(shadowhook_get_errno()));
        return false;
    }
    chains().push_back({address, stub, {original}});
    return true;
}

bool install(const MethodInfo* method, void* replacement, void** original) {
    if (!method) return false;
    return install(il2cpp::methodPointer(method), replacement, original);
}

}
