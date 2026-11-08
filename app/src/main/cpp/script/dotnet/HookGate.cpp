#include "script/dotnet/HookGate.h"
#include "script/dotnet/HookProfile.h"
#include "core/Log.h"
#include "hook/HookManager.h"

#include <mutex>

namespace bl::dotnet::gate {

struct Gate {
    void* target;
    void** original;
    int32_t pointerOffset;
    int32_t fieldOffset;
    int32_t arg;
    int32_t fieldSize;
};

static_assert(sizeof(Gate) == 32, "HookGate.S reads 32-byte gate entries");

constexpr int kGates = 64;
constexpr int kThunkBytes = 8;

} // namespace bl::dotnet::gate

extern "C" {
__attribute__((visibility("hidden"), aligned(16))) bl::dotnet::gate::Gate bl_gate_data[bl::dotnet::gate::kGates];
extern char bl_gate_thunks[];
}

namespace bl::dotnet::gate {

namespace {

std::mutex g_mutex;
int g_used = 0;

void** ungated(const MethodInfo* method, void* replacement) {
    if (profile::enabled()) return profile::installHook(method, replacement);
    auto** slot = new void*(nullptr);
    if (hook::install(method, replacement, slot)) return slot;
    delete slot;
    return nullptr;
}

} // namespace

void** installHook(const MethodInfo* method, void* replacement, int32_t arg, int32_t pointerOffset, int32_t fieldOffset, int32_t fieldSize) {
    if (!method || !replacement) return nullptr;
#if defined(__aarch64__)
    if (arg < 0 || arg > 7 || fieldOffset < 0 || (fieldSize != 1 && fieldSize != 4)) return ungated(method, replacement);
    std::lock_guard<std::mutex> guard(g_mutex);
    if (g_used >= kGates) {
        BL_WARN("hook gate: all %d gates in use; hooking without a gate", kGates);
        return ungated(method, replacement);
    }
    const int slot = g_used;
    auto** real = new void*(nullptr);
    void** callerSlot = nullptr;
    void* target = profile::wrapHook(method, replacement, real, &callerSlot);
    Gate& g = bl_gate_data[slot];
    g.target = target;
    g.original = real;
    g.pointerOffset = pointerOffset;
    g.fieldOffset = fieldOffset;
    g.arg = arg;
    g.fieldSize = fieldSize;
    __atomic_thread_fence(__ATOMIC_RELEASE);
    if (!hook::install(method, bl_gate_thunks + slot * kThunkBytes, real)) {
        BL_ERROR("hook gate: install failed");
        return nullptr;
    }
    ++g_used;
    return callerSlot;
#else
    (void)arg; (void)pointerOffset; (void)fieldOffset; (void)fieldSize;
    return ungated(method, replacement);
#endif
}

} // namespace bl::dotnet::gate
