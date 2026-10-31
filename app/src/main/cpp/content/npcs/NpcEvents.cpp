#include "content/npcs/NpcEvents.h"

#include "content/npcs/ModNpcs.h"
#include "core/Log.h"
#include "hook/Arm64Frame.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <array>
#include <atomic>
#include <cstring>
#include <mutex>

extern "C" {
__attribute__((visibility("hidden"))) void* g_blNpcRegenOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blNpcRegenDamageOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blNpcScalingOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blNpcDefaultsOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blNpcActiveOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blNpcDeadOriginal = nullptr;
}

namespace {

using namespace bl;

constexpr uint32_t Ai = 1, Frame = 2, Reset = 4, Active = 8, Regen = 16, Scaling = 32, Defaults = 64, Dead = 128;
std::array<std::atomic<uint32_t>, 65536> g_types{};
std::atomic<uint32_t> g_all{0};
std::atomic<runtime::NpcEventCallback> g_callback{nullptr};
std::mutex g_gate;
Il2CppClass* g_npc = nullptr;
int32_t g_type = -1;
uint32_t g_installed = 0;
bool g_failed = false;
using NpcFn = void (*)(Il2CppObject*, const MethodInfo*);
using BuffFn = void (*)(Il2CppObject*, bool, const MethodInfo*);
NpcFn g_ai = nullptr, g_dots = nullptr;
BuffFn g_reset = nullptr;
const MethodInfo* g_aiInfo = nullptr;
void* g_activeReturn = nullptr;
void* g_deadReturn = nullptr;

struct RegenState {
    Il2CppObject* npc;
    int32_t damage = -1;
    RegenState* outer;
};

thread_local RegenState* t_regen = nullptr;

bool wants(Il2CppObject* npc, uint32_t event) {
    if (!npc || g_type < 0) return false;
    const auto type = *reinterpret_cast<uint32_t*>(reinterpret_cast<uint8_t*>(npc) + g_type);
    return ((g_all.load(std::memory_order_relaxed) |
        (type < g_types.size() ? g_types[type].load(std::memory_order_relaxed) : 0)) & event) != 0;
}

void ai(Il2CppObject* npc, const MethodInfo* method) {
    auto callback = g_callback.load(std::memory_order_acquire);
    if (callback && wants(npc, Ai)) callback(0, npc, nullptr);
    else g_ai(npc, method);
}

void reset(Il2CppObject* npc, bool lowerBuffTime, const MethodInfo* method) {
    auto callback = g_callback.load(std::memory_order_acquire);
    if (callback && wants(npc, Reset)) callback(2, npc, nullptr);
    g_reset(npc, lowerBuffTime, method);
}

void dots(Il2CppObject* npc, const MethodInfo* method) {
    if (!wants(npc, Regen)) {
        g_dots(npc, method);
        return;
    }
    RegenState state{npc, -1, t_regen};
    struct Restore { RegenState* outer; ~Restore() { t_regen = outer; } } restore{t_regen};
    t_regen = &state;
    g_dots(npc, method);
}

bool entry(const char* signature, void* replacement, void** original, const MethodInfo** info = nullptr) {
    const MethodInfo* method = il2cpp::findMethodBySignature(g_npc, il2cpp::parseSignature(signature));
    if (info) *info = method;
    if (method && hook::install(il2cpp::methodPointer(method), replacement, original)) return true;
    BL_ERROR("NPC events: cannot hook %s", signature);
    return false;
}

}

extern "C" __attribute__((visibility("hidden"))) void blNpcRegenStage(RegisterFrame* frame) {
    auto* npc = reinterpret_cast<Il2CppObject*>(frame->general[19]);
    auto callback = g_callback.load(std::memory_order_acquire);
    if (callback && t_regen && t_regen->npc == npc) {
        const int32_t expected = *reinterpret_cast<int32_t*>(reinterpret_cast<uint8_t*>(frame + 1) + 0x60);
        t_regen->damage = expected == 0 ? -1 : expected;
        callback(4, npc, &t_regen->damage);
    }
}

extern "C" __attribute__((visibility("hidden"))) void blNpcRegenDamageStage(RegisterFrame* frame) {
    if (t_regen && t_regen->npc == reinterpret_cast<Il2CppObject*>(frame->general[19]))
        frame->general[20] = static_cast<uint32_t>(t_regen->damage);
}

extern "C" __attribute__((visibility("hidden"))) void blNpcScalingStage(RegisterFrame* frame) {
    auto* npc = reinterpret_cast<Il2CppObject*>(frame->general[19]);
    auto callback = g_callback.load(std::memory_order_acquire);
    if (!callback || !wants(npc, Scaling)) return;
    struct Args { int32_t players; float balance, bossAdjustment; } args;
    args.players = static_cast<int32_t>(frame->general[20]);
    std::memcpy(&args.balance, frame->vectors[9], sizeof(float));
    args.bossAdjustment = 1.f;
    callback(5, npc, &args);
}

extern "C" __attribute__((visibility("hidden"))) void blNpcDefaultsStage(RegisterFrame* frame) {
    auto callback = g_callback.load(std::memory_order_acquire);
    if (callback) callback(6, reinterpret_cast<void*>(frame->general[19]), nullptr);
}

extern "C" __attribute__((visibility("hidden"))) void blNpcActiveStage(RegisterFrame* frame) {
    auto* npc = reinterpret_cast<Il2CppObject*>(frame->general[19]);
    auto callback = g_callback.load(std::memory_order_acquire);
    void* target = callback && wants(npc, Active) && !callback(3, npc, nullptr) ? g_activeReturn : g_blNpcActiveOriginal;
    frame->general[16] = reinterpret_cast<uintptr_t>(target);
}

extern "C" __attribute__((visibility("hidden"))) void blNpcDeadStage(RegisterFrame* frame) {
    auto* npc = reinterpret_cast<Il2CppObject*>(frame->general[19]);
    auto callback = g_callback.load(std::memory_order_acquire);
    void* target = callback && wants(npc, Dead) && !callback(7, npc, nullptr) ? g_deadReturn : g_blNpcDeadOriginal;
    frame->general[16] = reinterpret_cast<uintptr_t>(target);
}

__attribute__((naked)) void blNpcRegenBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blNpcRegenStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blNpcRegenOriginal\n" "ldr x16, [x16, :lo12:g_blNpcRegenOriginal]\n" "br x16\n");
}

__attribute__((naked)) void blNpcRegenDamageBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blNpcRegenDamageStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blNpcRegenDamageOriginal\n" "ldr x16, [x16, :lo12:g_blNpcRegenDamageOriginal]\n" "br x16\n");
}

__attribute__((naked)) void blNpcScalingBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blNpcScalingStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blNpcScalingOriginal\n" "ldr x16, [x16, :lo12:g_blNpcScalingOriginal]\n" "br x16\n");
}

__attribute__((naked)) void blNpcDefaultsBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blNpcDefaultsStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blNpcDefaultsOriginal\n" "ldr x16, [x16, :lo12:g_blNpcDefaultsOriginal]\n" "br x16\n");
}

__attribute__((naked)) void blNpcActiveBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blNpcActiveStage\n" BL_ARM64_RESTORE_FRAME "br x16\n");
}

__attribute__((naked)) void blNpcDeadBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blNpcDeadStage\n" BL_ARM64_RESTORE_FRAME "br x16\n");
}

namespace {

bool stage(const char* signature, size_t offset, std::array<uint32_t, 4> expected, void* bridge, void** original) {
    const MethodInfo* method = bl::il2cpp::findMethodBySignature(g_npc, bl::il2cpp::parseSignature(signature));
    auto* code = method ? static_cast<uint8_t*>(bl::il2cpp::methodPointer(method)) : nullptr;
    if (!code || std::memcmp(code + offset, expected.data(), 16) != 0 || !bl::hook::install(code + offset, bridge, original)) {
        BL_ERROR("NPC events: cannot hook %s at +0x%zx", signature, offset);
        return false;
    }
    return true;
}

bool cancellingStage(const char* signature, size_t offset, std::array<uint32_t, 4> expected,
    size_t returnOffset, std::array<uint32_t, 4> epilogue, void* bridge, void** original, void** returnAddress) {
    const MethodInfo* method = bl::il2cpp::findMethodBySignature(g_npc, bl::il2cpp::parseSignature(signature));
    auto* code = method ? static_cast<uint8_t*>(bl::il2cpp::methodPointer(method)) : nullptr;
    if (!code || std::memcmp(code + returnOffset, epilogue.data(), 16) != 0) {
        BL_ERROR("NPC events: unexpected return in %s", signature);
        return false;
    }
    *returnAddress = code + returnOffset;
    return stage(signature, offset, expected, bridge, original);
}

}

namespace bl::runtime {

bool setNpcEvents(int32_t type, uint32_t events, NpcEventCallback callback) {
    if (!callback || type < -1 || type >= static_cast<int32_t>(g_types.size())) return false;
    std::lock_guard<std::mutex> lock(g_gate);
    if (g_failed) return false;
    if (!g_npc) {
        g_npc = il2cpp::findClass({"Terraria", "NPC", ""});
        g_type = g_npc ? il2cpp::fieldOffset(g_npc, "type") : -1;
        if (g_type < 0) return false;
    }
    const uint32_t missing = events & ~g_installed;
    struct Failure { bool success = false; ~Failure() { if (!success) g_failed = true; } } failure;
    if ((missing & Ai) && !entry("void AI()", reinterpret_cast<void*>(ai), reinterpret_cast<void**>(&g_ai), &g_aiInfo)) return false;
    if ((missing & Frame) && !ensureNpcFrameHook()) return false;
    if ((missing & Reset) && !entry("void UpdateNPC_BuffSetFlags(bool lowerBuffTime)", reinterpret_cast<void*>(reset), reinterpret_cast<void**>(&g_reset))) return false;
    if ((missing & Active) && !cancellingStage("void CheckActive()", 0xac,
        {0xf000ecf6, 0xf9454ed6, 0xb000ecba, 0xb9416679}, 0x6f4,
        {0xa9497bfd, 0xa9484ff4, 0xa94757f6, 0xa9465ff8}, reinterpret_cast<void*>(blNpcActiveBridge), &g_blNpcActiveOriginal, &g_activeReturn)) return false;
    if ((missing & Regen) && (!stage("void UpdateNPC_BuffApplyDOTs()", 0x688,
        {0x394193e8, 0x360000a8, 0xb9413a68, 0x7100051f}, reinterpret_cast<void*>(blNpcRegenBridge), &g_blNpcRegenOriginal) ||
        !stage("void UpdateNPC_BuffApplyDOTs()", 0x700,
        {0x3100069f, 0xb9013e68, 0x54000121, 0x3200ebe9}, reinterpret_cast<void*>(blNpcRegenDamageBridge), &g_blNpcRegenDamageOriginal) ||
        !entry("void UpdateNPC_BuffApplyDOTs()", reinterpret_cast<void*>(dots), reinterpret_cast<void**>(&g_dots)))) return false;
    if ((missing & Scaling) && !stage("void ScaleStats_ByPlayerCount(int numPlayers)", 0x4a8,
        {0xf94002c0, 0xb941c674, 0xb940e008, 0x35000048}, reinterpret_cast<void*>(blNpcScalingBridge), &g_blNpcScalingOriginal)) return false;
    if ((missing & Defaults) && !stage("void SetDefaults(int Type, NPCSpawnParams spawnparams)", 0x4c4c,
        {0xf9400340, 0xb940e008, 0x35000068, 0x97d67e37}, reinterpret_cast<void*>(blNpcDefaultsBridge), &g_blNpcDefaultsOriginal)) return false;
    if ((missing & Dead) && !cancellingStage("void checkDead()", 0x77c,
        {0xf9405c08, 0x52800029, 0x3901c109, 0x3948f268}, 0x130,
        {0xa9487bfd, 0xa9474ff4, 0xa94657f6, 0xa9455ff8}, reinterpret_cast<void*>(blNpcDeadBridge), &g_blNpcDeadOriginal, &g_deadReturn)) return false;
    g_installed |= missing;
    g_callback.store(callback, std::memory_order_release);
    if (type < 0) g_all.fetch_or(events, std::memory_order_relaxed);
    else g_types[type].fetch_or(events, std::memory_order_relaxed);
    failure.success = true;
    return true;
}

bool npcFrameEvent(Il2CppObject* npc) {
    auto callback = g_callback.load(std::memory_order_acquire);
    if (!callback || !wants(npc, Frame)) return false;
    callback(1, npc, nullptr);
    return true;
}

void runNpcOriginal(Il2CppObject* npc, int32_t event, int32_t argument) {
    if (event == 1) runNpcFindFrame(npc, argument);
    else if (event == 0) {
        if (g_ai) g_ai(npc, g_aiInfo);
        else {
            static auto* cls = il2cpp::findClass({"Terraria", "NPC", ""});
            static const MethodInfo* method = cls ? il2cpp::api().class_get_method_from_name(cls, "AI", 0) : nullptr;
            if (method) reinterpret_cast<NpcFn>(il2cpp::methodPointer(method))(npc, method);
        }
    }
}

Il2CppObject* npcImmunityData(int32_t type) {
    auto& a = il2cpp::api();
    static Il2CppClass* sets = il2cpp::findClass({"Terraria.ID", "NPCID", "Sets"});
    static FieldInfo* field = sets ? il2cpp::findField(sets, "DebuffImmunitySets") : nullptr;
    if (!field) return nullptr;
    a.runtime_class_init(sets);
    Il2CppObject* dictionary = nullptr;
    a.field_static_get_value(field, &dictionary);
    if (!dictionary) return nullptr;
    static const MethodInfo* get = a.class_get_method_from_name(a.object_get_class(dictionary), "TryGetValue", 2);
    if (!get) return nullptr;
    Il2CppObject* data = nullptr;
    Il2CppObject* error = nullptr;
    void* args[] = {&type, &data};
    a.runtime_invoke(get, dictionary, args, &error);
    return error ? nullptr : data;
}

}
