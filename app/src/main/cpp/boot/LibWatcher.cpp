#include "boot/LibWatcher.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "boot/Boot.h"
#include "shadowhook.h"

namespace bl::loader {

namespace {

using InitFn = int (*)(const char*);
InitFn origInit = nullptr;
void* initStub = nullptr;

int hkInit(const char* domainName) {
    int result = origInit(domainName);
    BL_DEBUG("il2cpp_init concluido (domain=%s)", domainName ? domainName : "?");
    runtime::boot();
    return result;
}

}

bool installWatcher() {
    static bool installed = false;
    if (installed) return true;

    shadowhook_init(SHADOWHOOK_MODE_UNIQUE, false);
    initStub = shadowhook_hook_sym_name(
        "libil2cpp.so",
        "il2cpp_init",
        reinterpret_cast<void*>(hkInit),
        reinterpret_cast<void**>(&origInit));

    if (!initStub && shadowhook_get_errno() != SHADOWHOOK_ERRNO_PENDING) {
        BL_ERROR("Failed to register the il2cpp_init hook: %s",
                 shadowhook_to_errmsg(shadowhook_get_errno()));
        return false;
    }
    installed = true;
    BL_DEBUG("il2cpp_init watcher registered");
    return true;
}

}
