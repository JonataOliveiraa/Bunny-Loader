#include "script/dotnet/HookGate.h"
#include "script/dotnet/HookProfile.h"
#include "core/Log.h"
#include "hook/HookManager.h"

#include <cstddef>
#include <mutex>

namespace bl::dotnet::gate {

struct Gate {
    void* target;
    void** original;
    int32_t pointerOffset;
    int32_t fieldOffset;
    int32_t arg;
    int32_t fieldSize;
    const uint8_t* bitmap;
    int32_t bitmapBits;
    int32_t flags;
    const void* address;
    int64_t reserved;
};

static_assert(sizeof(Gate) == 64, "HookGate.S reads 64-byte gate entries");
static_assert(offsetof(Gate, pointerOffset) == 16 && offsetof(Gate, fieldOffset) == 20, "HookGate.S field offsets");
static_assert(offsetof(Gate, arg) == 24 && offsetof(Gate, fieldSize) == 28, "HookGate.S field offsets");
static_assert(offsetof(Gate, bitmap) == 32 && offsetof(Gate, bitmapBits) == 40, "HookGate.S field offsets");
static_assert(offsetof(Gate, flags) == 44 && offsetof(Gate, address) == 48, "HookGate.S field offsets");

constexpr int kGates = 256;
constexpr int kThunkBytes = 8;

}

extern "C" {
__attribute__((visibility("hidden"), aligned(64))) bl::dotnet::gate::Gate bl_gate_data[bl::dotnet::gate::kGates];
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

bool valid(const Spec& spec) {
    if (spec.arg < 0 || spec.arg > ArgAbsolute) return false;
    if (spec.arg == ArgAbsolute && !spec.address) return false;
    if ((spec.flags & FlagValue) && (spec.arg == ArgAbsolute || spec.pointerOffset >= 0 || spec.fieldOffset >= 0 || (spec.fieldSize != 1 && spec.fieldSize != 4))) return false;
    if (spec.fieldOffset >= 0 && spec.fieldSize != 1 && spec.fieldSize != 4) return false;
    if (spec.bitmap && ((spec.fieldOffset < 0 && !(spec.flags & FlagValue)) || spec.bitmapBits <= 0)) return false;
    return true;
}

}

void** installHook(const MethodInfo* method, void* replacement, const Spec& spec, int32_t* id) {
    if (id) *id = -1;
    if (!method || !replacement) return nullptr;
#if defined(__aarch64__)
    if (!valid(spec)) return ungated(method, replacement);
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
    g.pointerOffset = spec.pointerOffset;
    g.fieldOffset = spec.fieldOffset;
    g.arg = spec.arg;
    g.fieldSize = spec.fieldSize;
    g.bitmap = spec.bitmap;
    g.bitmapBits = spec.bitmap ? spec.bitmapBits : 0;
    g.address = spec.address;
    __atomic_store_n(&g.flags, spec.flags, __ATOMIC_RELEASE);
    if (!hook::install(method, bl_gate_thunks + slot * kThunkBytes, real)) {
        BL_ERROR("hook gate: install failed");
        return nullptr;
    }
    ++g_used;
    if (id) *id = slot;
    return callerSlot;
#else
    (void)spec;
    return ungated(method, replacement);
#endif
}

void** installHook(const MethodInfo* method, void* replacement, int32_t arg, int32_t pointerOffset, int32_t fieldOffset, int32_t fieldSize) {
    if (arg < 0 || arg > 7 || fieldOffset < 0 || (fieldSize != 1 && fieldSize != 4)) return ungated(method, replacement);
    const Spec spec{arg, pointerOffset, fieldOffset, fieldSize, nullptr, 0, 0, nullptr};
    return installHook(method, replacement, spec, nullptr);
}

bool setAlways(int32_t id, bool always) {
    std::lock_guard<std::mutex> guard(g_mutex);
    if (id < 0 || id >= g_used) return false;
    Gate& g = bl_gate_data[id];
    const int32_t flags = __atomic_load_n(&g.flags, __ATOMIC_RELAXED);
    __atomic_store_n(&g.flags, always ? (flags | FlagAlways) : (flags & ~FlagAlways), __ATOMIC_RELEASE);
    return true;
}

}
