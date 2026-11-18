#include "boot/LibWatcher.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "boot/Boot.h"
#include "shadowhook.h"

#include <atomic>
#include <condition_variable>
#include <mutex>

namespace bl::loader {

namespace {

using InitFn = int (*)(const char*);
InitFn origInit = nullptr;
void* initStub = nullptr;
std::atomic<bool> g_installed{false};
std::mutex g_initMutex;
std::condition_variable g_initCv;
bool g_initDone = false;

void markInitDone() {
    {
        std::lock_guard<std::mutex> lock(g_initMutex);
        g_initDone = true;
    }
    g_initCv.notify_all();
}

int hkInit(const char* domainName) {
    int result = origInit(domainName);
    BL_DEBUG("il2cpp_init concluido (domain=%s)", domainName ? domainName : "?");
    runtime::boot();
    markInitDone();
    return result;
}

}

bool watcherInstalled() { return g_installed.load(std::memory_order_acquire); }

bool waitForIl2cppInit(std::chrono::steady_clock::time_point deadline) {
    std::unique_lock<std::mutex> lock(g_initMutex);
    return g_initCv.wait_until(lock, deadline, [] { return g_initDone; });
}

void waitForIl2cppInit() {
    std::unique_lock<std::mutex> lock(g_initMutex);
    g_initCv.wait(lock, [] { return g_initDone; });
}

bool installWatcher() {
    if (g_installed.load(std::memory_order_acquire)) return true;

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
    g_installed.store(true, std::memory_order_release);
    BL_DEBUG("il2cpp_init watcher registered");
    return true;
}

}
