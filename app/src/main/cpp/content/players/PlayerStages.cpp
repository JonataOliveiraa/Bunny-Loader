#include "content/players/PlayerStages.h"

#if defined(__aarch64__)
#include "core/Log.h"
#include "hook/Arm64Frame.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "il2cpp/Types.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstring>
#include <mutex>

extern "C" {
__attribute__((visibility("hidden"))) void* g_blNaturalRegenStageOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blConsumeBaitStageOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blDyeRewardStageOriginal = nullptr;
}

namespace {

std::atomic<bl::runtime::NaturalRegenCallback> g_naturalRegen{nullptr};
std::atomic<bl::runtime::ConsumeBaitCallback> g_consumeBait{nullptr};
std::atomic<bl::runtime::DyeRewardCallback> g_dyeReward{nullptr};
std::mutex g_mx;

struct Stage {
    const char* name;
    const char* signature;
    size_t offset;
    uint32_t expected[4];
    void (*bridge)();
    void** original;
    int state = 0;
};

constexpr int32_t kDyePoolCapacity = 512;

}

extern "C" __attribute__((visibility("hidden"))) void blNaturalRegenStage(RegisterFrame* frame) {
    bl::runtime::NaturalRegenCallback callback = g_naturalRegen.load(std::memory_order_acquire);
    if (!callback) return;
    float value;
    std::memcpy(&value, frame->vectors[9], sizeof(value));
    const float changed = callback(reinterpret_cast<void*>(frame->general[19]), value);
    if (std::isfinite(changed)) std::memcpy(frame->vectors[9], &changed, sizeof(changed));
}

extern "C" __attribute__((visibility("hidden"))) void blConsumeBaitStage(RegisterFrame* frame) {
    bl::runtime::ConsumeBaitCallback callback = g_consumeBait.load(std::memory_order_acquire);
    if (!callback) return;
    const int32_t decision = callback(reinterpret_cast<void*>(frame->general[19]), reinterpret_cast<void*>(frame->general[21]));
    if (decision < 0) return;
    frame->general[27] = decision ? 1 : 0;
    if (!decision) frame->general[8] = 0;
}

extern "C" __attribute__((visibility("hidden"))) void blDyeRewardStage(RegisterFrame* frame) {
    bl::runtime::DyeRewardCallback callback = g_dyeReward.load(std::memory_order_acquire);
    auto* list = reinterpret_cast<Il2CppObject*>(frame->general[21]);
    if (!callback || !list) return;
    auto& a = bl::il2cpp::api();
    Il2CppClass* cls = a.object_get_class(list);
    const int32_t offItems = bl::il2cpp::fieldOffset(cls, "_items");
    const int32_t offSize = bl::il2cpp::fieldOffset(cls, "_size");
    const MethodInfo* clear = a.class_get_method_from_name(cls, "Clear", 0);
    const MethodInfo* add = a.class_get_method_from_name(cls, "Add", 1);
    if (offItems < 0 || offSize < 0 || !clear || !add) {
        BL_ERROR("GetDyeTraderReward: the reward pool is not a List<int>");
        return;
    }
    auto* items = *reinterpret_cast<Il2CppArray**>(reinterpret_cast<uint8_t*>(list) + offItems);
    const int32_t size = *reinterpret_cast<int32_t*>(reinterpret_cast<uint8_t*>(list) + offSize);
    int32_t pool[kDyePoolCapacity];
    const int32_t count = items ? std::min(size, kDyePoolCapacity) : 0;
    if (count > 0) std::memcpy(pool, arrayData(items), sizeof(int32_t) * count);
    int32_t changed = callback(reinterpret_cast<void*>(frame->general[19]), pool, count, kDyePoolCapacity);
    if (changed < 0) return;
    changed = std::min(changed, kDyePoolCapacity);
    Il2CppObject* exception = nullptr;
    a.runtime_invoke(clear, list, nullptr, &exception);
    int32_t added = 0;
    for (int32_t i = 0; i < changed && !exception; ++i) {
        if (pool[i] <= 0) continue;
        void* args[1] = {&pool[i]};
        a.runtime_invoke(add, list, args, &exception);
        ++added;
    }
    if (!added && !exception) {
        int32_t none = 0;
        void* args[1] = {&none};
        a.runtime_invoke(add, list, args, &exception);
    }
    if (exception) BL_ERROR("GetDyeTraderReward: the game refused the reward pool");
}

__attribute__((naked)) void blNaturalRegenBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME
        "mov x0, sp\n" "bl blNaturalRegenStage\n"
        BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blNaturalRegenStageOriginal\n" "ldr x16, [x16, :lo12:g_blNaturalRegenStageOriginal]\n" "br x16\n");
}

__attribute__((naked)) void blConsumeBaitBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME
        "mov x0, sp\n" "bl blConsumeBaitStage\n"
        BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blConsumeBaitStageOriginal\n" "ldr x16, [x16, :lo12:g_blConsumeBaitStageOriginal]\n" "br x16\n");
}

__attribute__((naked)) void blDyeRewardBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME
        "mov x0, sp\n" "bl blDyeRewardStage\n"
        BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blDyeRewardStageOriginal\n" "ldr x16, [x16, :lo12:g_blDyeRewardStageOriginal]\n" "br x16\n");
}

namespace {

Stage g_stages[] = {
    {"NaturalLifeRegen", "void UpdateLifeRegen()", 0x828,
        {0xbd481e60, 0x9000a269, 0xf9400300, 0x9000a26a}, blNaturalRegenBridge, &g_blNaturalRegenStageOriginal},
    {"CanConsumeBait", "bool ItemCheck_CheckFishingBobber_ConsumeBait(Projectile bobber, out int baitTypeUsed)", 0x2bc,
        {0x3700007b, 0x7129c51f, 0x54000421, 0xb9404ea8}, blConsumeBaitBridge, &g_blConsumeBaitStageOriginal},
    {"GetDyeTraderReward", "void GetDyeTraderReward(NPC dyeTrader)", 0xef0,
        {0xb9401aa1, 0xd000e2b6, 0xb000e238, 0xf94202d6}, blDyeRewardBridge, &g_blDyeRewardStageOriginal},
};

bool install(Stage& stage) {
    std::lock_guard<std::mutex> lock(g_mx);
    if (stage.state == 0) {
        using namespace bl::il2cpp;
        auto* player = findClass({"Terraria", "Player", ""});
        const MethodInfo* method = player ? findMethodBySignature(player, parseSignature(stage.signature)) : nullptr;
        auto* entry = method ? static_cast<uint8_t*>(methodPointer(method)) : nullptr;
        if (!entry || std::memcmp(entry + stage.offset, stage.expected, sizeof(stage.expected)) != 0) {
            BL_ERROR("%s: the game's method body is not the expected one", stage.name);
            stage.state = -1;
        } else if (!bl::hook::install(entry + stage.offset, reinterpret_cast<void*>(stage.bridge), stage.original)) {
            BL_ERROR("%s: the native hook was refused", stage.name);
            stage.state = -1;
        } else {
            stage.state = 1;
        }
    }
    return stage.state == 1;
}

}

namespace bl::runtime {

bool setNaturalRegenCallback(NaturalRegenCallback callback) {
    if (!install(g_stages[0])) return false;
    g_naturalRegen.store(callback, std::memory_order_release);
    return true;
}

bool setConsumeBaitCallback(ConsumeBaitCallback callback) {
    if (!install(g_stages[1])) return false;
    g_consumeBait.store(callback, std::memory_order_release);
    return true;
}

bool setDyeRewardCallback(DyeRewardCallback callback) {
    if (!install(g_stages[2])) return false;
    g_dyeReward.store(callback, std::memory_order_release);
    return true;
}

}

#else

namespace bl::runtime {
bool setNaturalRegenCallback(NaturalRegenCallback) { return false; }
bool setConsumeBaitCallback(ConsumeBaitCallback) { return false; }
bool setDyeRewardCallback(DyeRewardCallback) { return false; }
}

#endif
