#include "content/npcs/NpcLootHooks.h"
#include "core/Log.h"
#include "hook/Arm64Frame.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include <array>
#include <atomic>
#include <cstring>
#include <mutex>
#include <vector>

extern "C" {
__attribute__((visibility("hidden"))) void* g_blNpcLootPreOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blNpcLootKillOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blNpcLootBossOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blNpcLootHeartsOriginal = nullptr;
}

namespace {

using namespace bl;
std::atomic<runtime::NpcLootCallback> g_callback{nullptr};
std::mutex g_gate;
uint32_t g_installed = 0;
void* g_return = nullptr;
void* g_hearts = nullptr;
void* g_heartsLoop = nullptr;
thread_local std::vector<int32_t> t_hearts;

bool stage(Il2CppClass* npc, const char* signature, size_t offset, std::array<uint32_t, 4> expected, void* replacement, void** original) {
    const MethodInfo* method = il2cpp::findMethodBySignature(npc, il2cpp::parseSignature(signature));
    auto* code = method ? static_cast<uint8_t*>(il2cpp::methodPointer(method)) : nullptr;
    if (!code || std::memcmp(code + offset, expected.data(), 16) != 0 || !hook::install(code + offset, replacement, original)) {
        BL_ERROR("NPC loot: cannot hook %s at +0x%zx", signature, offset);
        return false;
    }
    return true;
}

bool target(Il2CppClass* npc, const char* signature, size_t offset, std::array<uint32_t, 4> expected, void** address) {
    const MethodInfo* method = il2cpp::findMethodBySignature(npc, il2cpp::parseSignature(signature));
    auto* code = method ? static_cast<uint8_t*>(il2cpp::methodPointer(method)) : nullptr;
    if (!code || std::memcmp(code + offset, expected.data(), 16) != 0) return false;
    *address = code + offset;
    return true;
}

}

extern "C" __attribute__((visibility("hidden"))) void blNpcLootPreStage(RegisterFrame* frame) {
    auto callback = g_callback.load(std::memory_order_acquire);
    void* npc = reinterpret_cast<void*>(frame->general[19]);
    frame->general[16] = reinterpret_cast<uintptr_t>(callback && !callback(0, npc, nullptr) ? g_return : g_blNpcLootPreOriginal);
}

extern "C" __attribute__((visibility("hidden"))) void blNpcLootKillStage(RegisterFrame* frame) {
    auto callback = g_callback.load(std::memory_order_acquire);
    if (callback) callback(1, reinterpret_cast<void*>(frame->general[19]), nullptr);
}

extern "C" __attribute__((visibility("hidden"))) void blNpcLootBossStage(RegisterFrame* frame) {
    auto callback = g_callback.load(std::memory_order_acquire);
    int32_t args[]{static_cast<int32_t>(frame->general[21]), static_cast<int32_t>(frame->general[20]), -1};
    const bool modified = callback && callback(2, reinterpret_cast<void*>(frame->general[19]), args);
    t_hearts.push_back(modified ? args[2] : -1);
    if (modified) {
        frame->general[21] = static_cast<uint32_t>(args[0]);
        frame->general[20] = static_cast<uint32_t>(args[1]);
    }
    frame->general[16] = reinterpret_cast<uintptr_t>(modified && (args[0] <= 0 || args[1] <= 0) ? g_hearts : g_blNpcLootBossOriginal);
}

extern "C" __attribute__((visibility("hidden"))) void blNpcLootHeartsStage(RegisterFrame* frame) {
    int32_t hearts = t_hearts.empty() ? -1 : t_hearts.back();
    if (!t_hearts.empty()) t_hearts.pop_back();
    if (hearts >= 0) frame->general[23] = static_cast<uint32_t>(hearts);
    frame->general[16] = reinterpret_cast<uintptr_t>(hearts >= 0 ? g_heartsLoop : g_blNpcLootHeartsOriginal);
}

__attribute__((naked)) void blNpcLootPreBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blNpcLootPreStage\n" BL_ARM64_RESTORE_FRAME "br x16\n");
}

__attribute__((naked)) void blNpcLootKillBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blNpcLootKillStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blNpcLootKillOriginal\n" "ldr x16, [x16, :lo12:g_blNpcLootKillOriginal]\n" "br x16\n");
}

__attribute__((naked)) void blNpcLootBossBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blNpcLootBossStage\n" BL_ARM64_RESTORE_FRAME "br x16\n");
}

__attribute__((naked)) void blNpcLootHeartsBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blNpcLootHeartsStage\n" BL_ARM64_RESTORE_FRAME "br x16\n");
}

namespace bl::runtime {

bool installNpcLootHooks(uint32_t flags, NpcLootCallback callback) {
    if (!callback || (flags & ~7u)) return false;
    std::lock_guard<std::mutex> lock(g_gate);
    auto* npc = il2cpp::findClass({"Terraria", "NPC", ""});
    const uint32_t missing = flags & ~g_installed;
    if (!npc) return false;
    if ((missing & 1) && (!target(npc, "void NPCLoot()", 0x4a4, {0xa9457bf3, 0xa94453f5, 0xa9435bf7, 0xa94263f9}, &g_return) ||
        !stage(npc, "void NPCLoot()", 0x2f8, {0xf94002e0, 0xb940e008, 0x35000068, 0x97d10865}, reinterpret_cast<void*>(blNpcLootPreBridge), &g_blNpcLootPreOriginal))) return false;
    if ((missing & 2) && !stage(npc, "void NPCLoot()", 0x368, {0xaa1303e0, 0xaa1403e1, 0x94000861, 0xaa1f03e0},
        reinterpret_cast<void*>(blNpcLootKillBridge), &g_blNpcLootKillOriginal)) return false;
    if (missing & 4) {
        if (!target(npc, "void DoDeathEvents_DropBossPotionsAndHearts()", 0x1b0, {0x710006ff, 0x5400044b, 0x52aff018, 0x52b0001a}, &g_heartsLoop) ||
            !target(npc, "void DoDeathEvents_DropBossPotionsAndHearts()", 0x180, {0xf9400340, 0xb940e008, 0x35000068, 0x97d0f2fc}, &g_hearts) ||
            !stage(npc, "void DoDeathEvents_DropBossPotionsAndHearts()", 0x108, {0xaa1303e0, 0x97ffa92c, 0xf9400328, 0x2d42a269}, reinterpret_cast<void*>(blNpcLootBossBridge), &g_blNpcLootBossOriginal) ||
            !stage(npc, "void DoDeathEvents_DropBossPotionsAndHearts()", 0x180, {0xf9400340, 0xb940e008, 0x35000068, 0x97d0f2fc}, reinterpret_cast<void*>(blNpcLootHeartsBridge), &g_blNpcLootHeartsOriginal)) return false;
    }
    g_installed |= missing;
    g_callback.store(callback, std::memory_order_release);
    return true;
}

}
