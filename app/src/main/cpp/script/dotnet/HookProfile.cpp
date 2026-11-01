#include "script/dotnet/HookProfile.h"
#include "boot/Boot.h"
#include "core/Log.h"
#include "hook/HookManager.h"

#include <sys/system_properties.h>
#include <time.h>
#include <unistd.h>

#include <atomic>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <unordered_map>

extern "C" char bl_hp_enter_thunks[];
extern "C" char bl_hp_orig_thunks[];

namespace bl::dotnet::profile {

namespace {

constexpr int kSlots = 2048;
constexpr int kThunkBytes = 8;
constexpr int kDepth = 256;
constexpr uint64_t kNoToken = ~0ull;
constexpr int kCalibrationCalls = 20000;

struct Stat {
    uint64_t calls = 0;
    uint64_t selfNs = 0;
    uint64_t totalNs = 0;
    std::string name;
};

struct Open {
    int64_t t0;
    int64_t child;
    uintptr_t sp;
    int32_t slot;
    bool managed;
};

Stat g_stats[kSlots];
void* g_target[kSlots];
void** g_real[kSlots];
std::atomic<int> g_used{0};
std::mutex g_mutex;
std::unordered_map<void*, void*> g_wrapped;

Open g_open[kDepth];
int g_depth = 0;

int64_t g_biasPs = -1;
int64_t g_clockPs = 0;
int g_calibrationSlot = -1;

inline int64_t nowNs() {
    timespec t;
    clock_gettime(CLOCK_MONOTONIC, &t);
    return static_cast<int64_t>(t.tv_sec) * 1000000000 + t.tv_nsec;
}

inline bool onGameThread() {
    const int game = runtime::gameThreadId();
    return game != 0 && static_cast<int>(gettid()) == game;
}

void* enterThunk(int slot) { return bl_hp_enter_thunks + slot * kThunkBytes; }
void* origThunk(int slot) { return bl_hp_orig_thunks + slot * kThunkBytes; }

int slotOf(const char* base, uintptr_t thunk) {
    return static_cast<int>((thunk - reinterpret_cast<uintptr_t>(base)) / kThunkBytes);
}

int reserve(const std::string& name) {
    const int slot = g_used.load(std::memory_order_relaxed);
    if (slot >= kSlots) return -1;
    g_stats[slot].name = name;
    g_used.store(slot + 1, std::memory_order_release);
    return slot;
}

std::string methodName(const MethodInfo* method) {
    auto& a = il2cpp::api();
    Il2CppClass* owner = a.method_get_class ? a.method_get_class(method) : nullptr;
    std::string name = owner && a.class_get_name ? a.class_get_name(owner) : "?";
    name += '.';
    name += a.method_get_name ? a.method_get_name(method) : "?";
    if (a.method_get_param_count) name += "/" + std::to_string(a.method_get_param_count(method));
    return name;
}

void* open(int slot, bool managed, uint64_t* token, uintptr_t sp, void* target) {
    while (g_depth > 0 && g_open[g_depth - 1].sp <= sp) --g_depth;
    if (g_depth >= kDepth) {
        *token = kNoToken;
        return target;
    }
    Open& o = g_open[g_depth];
    o.sp = sp;
    o.slot = slot;
    o.managed = managed;
    o.child = 0;
    *token = static_cast<uint64_t>(g_depth++);
    o.t0 = nowNs();
    return target;
}

void close(uint64_t token) {
    const int64_t t = nowNs();
    if (token == kNoToken) return;
    const int depth = static_cast<int>(token);
    if (depth >= g_depth) return;
    g_depth = depth;
    const Open& o = g_open[depth];
    const int64_t elapsed = t - o.t0;
    if (depth > 0) g_open[depth - 1].child += elapsed;
    if (!o.managed) return;
    Stat& s = g_stats[o.slot];
    ++s.calls;
    s.totalNs += static_cast<uint64_t>(elapsed);
    if (elapsed > o.child) s.selfNs += static_cast<uint64_t>(elapsed - o.child);
}

int32_t calibrationNoop() { return 0; }

int32_t calibrationReplacement() {
    return reinterpret_cast<int32_t (*)()>(origThunk(g_calibrationSlot))();
}

void calibrate() {
    const int64_t c0 = nowNs();
    int64_t sink = 0;
    for (int k = 0; k < kCalibrationCalls; ++k) sink += nowNs();
    g_clockPs = (nowNs() - c0) * 1000 / kCalibrationCalls + (sink == 0 ? 1 : 0);

    {
        std::lock_guard<std::mutex> guard(g_mutex);
        g_calibrationSlot = reserve("(profiler calibration)");
    }
    if (g_calibrationSlot < 0) {
        g_biasPs = 0;
        return;
    }
    g_target[g_calibrationSlot] = reinterpret_cast<void*>(&calibrationReplacement);
    static void* real = reinterpret_cast<void*>(&calibrationNoop);
    g_real[g_calibrationSlot] = &real;
    const int depth = g_depth;
    const int64_t parentChild = depth > 0 ? g_open[depth - 1].child : 0;
    auto fn = reinterpret_cast<int32_t (*)()>(enterThunk(g_calibrationSlot));
    for (int k = 0; k < kCalibrationCalls; ++k) sink += fn();
    g_depth = depth;
    if (depth > 0) g_open[depth - 1].child = parentChild;
    const Stat& s = g_stats[g_calibrationSlot];
    g_biasPs = s.calls ? static_cast<int64_t>(s.selfNs * 1000 / s.calls) : 0;
}

} // namespace

extern "C" void* bl_hp_enter_pre(uintptr_t thunk, uint64_t* token, uintptr_t sp) {
    const int slot = slotOf(bl_hp_enter_thunks, thunk);
    void* target = g_target[slot];
    if (!onGameThread()) {
        *token = kNoToken;
        return target;
    }
    return open(slot, true, token, sp, target);
}

extern "C" void* bl_hp_orig_pre(uintptr_t thunk, uint64_t* token, uintptr_t sp) {
    const int slot = slotOf(bl_hp_orig_thunks, thunk);
    void* target = __atomic_load_n(g_real[slot], __ATOMIC_ACQUIRE);
    if (!onGameThread()) {
        *token = kNoToken;
        return target;
    }
    return open(slot, false, token, sp, target);
}

extern "C" void bl_hp_post(uint64_t token) {
    if (token != kNoToken) close(token);
}

bool enabled() {
#if defined(__aarch64__)
    static const bool on = [] {
        char value[PROP_VALUE_MAX] = {};
        __system_property_get("debug.bl.cshookprofile", value);
        const bool yes = std::strcmp(value, "1") == 0;
        if (yes) BL_INFO("C# hook profiler: on (debug.bl.cshookprofile=1)");
        return yes;
    }();
    return on;
#else
    return false;
#endif
}

void** installHook(const MethodInfo* method, void* replacement) {
    int slot;
    {
        std::lock_guard<std::mutex> guard(g_mutex);
        slot = reserve(methodName(method));
    }
    if (slot < 0) {
        auto** plain = new void*(nullptr);
        if (hook::install(method, replacement, plain)) return plain;
        delete plain;
        return nullptr;
    }
    g_target[slot] = replacement;
    auto** real = new void*(nullptr);
    g_real[slot] = real;
    if (!hook::install(method, enterThunk(slot), real)) {
        BL_ERROR("C# hook profiler: install failed for %s", g_stats[slot].name.c_str());
        return nullptr;
    }
    return new void*(origThunk(slot));
}

void* wrapCallback(void* callback, const char* name) {
    std::lock_guard<std::mutex> guard(g_mutex);
    auto found = g_wrapped.find(callback);
    if (found != g_wrapped.end()) return found->second;
    const int slot = reserve(std::string("(callback) ") + (name ? name : "?"));
    if (slot < 0) return callback;
    g_target[slot] = callback;
    void* thunk = enterThunk(slot);
    g_wrapped[callback] = thunk;
    return thunk;
}

int32_t read(int32_t index, uint64_t* values, const char** name) {
    const int used = g_used.load(std::memory_order_acquire);
    if (index == -1) {
        if (enabled() && g_biasPs < 0 && onGameThread()) calibrate();
        if (values) {
            values[0] = enabled() ? 1 : 0;
            values[1] = static_cast<uint64_t>(g_biasPs < 0 ? 0 : g_biasPs);
            values[2] = static_cast<uint64_t>(g_clockPs);
        }
        return g_used.load(std::memory_order_acquire);
    }
    if (index < 0 || index >= used) return used;
    const Stat& s = g_stats[index];
    if (values) {
        values[0] = s.calls;
        values[1] = s.selfNs;
        values[2] = s.totalNs;
    }
    if (name) *name = s.name.c_str();
    return used;
}

} // namespace bl::dotnet::profile
