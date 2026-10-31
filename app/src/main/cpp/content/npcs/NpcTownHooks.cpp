#include "content/npcs/NpcTownHooks.h"
#include "core/Log.h"
#include "hook/Arm64Frame.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstring>
#include <mutex>

extern "C" {
__attribute__((visibility("hidden"))) void* g_blTownThrowOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blTownShootOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blTownMagicOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blTownSwingOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blTownSwingRectOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blTownGunDrawOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blTownSwingDrawOriginal = nullptr;
}

namespace {
using namespace bl;
struct AttackArgs {
    int32_t damage; float knockback;
    int32_t cooldown, extra, projectile, delay;
    float speed, gravity, random, aura;
    int32_t width, height, inBetween;
};
struct Float2 { float x, y; };
struct Rectangle { int32_t x, y, width, height; };
struct DrawArgs { void* texture; Rectangle frame; float scale; int32_t size; Float2 offset; };
struct AttackScope { void* npc; int32_t mode = -1; AttackArgs args{}; AttackScope* outer; };
thread_local AttackScope* t_scope = nullptr;
std::array<std::atomic<uint32_t>, 65536> g_types{};
std::atomic<uint32_t> g_all{0};
std::atomic<runtime::NpcTownCallback> g_callback{nullptr};
std::mutex g_gate;
Il2CppClass* g_npc = nullptr;
int32_t g_type = -1;
uint32_t g_installed = 0;
bool g_failed = false;
using AiFn = void (*)(void*, const MethodInfo*);
using ChatFn = bool (*)(void*, const MethodInfo*);
using RandomFn = Float2 (*)(void*, float, float, const MethodInfo*);
using LightFn = void (*)(Float2, float, float, float, const MethodInfo*);
using SwingFn = void* (*)(void*, int32_t, int32_t, int32_t, int32_t, int32_t, const MethodInfo*);
AiFn g_ai = nullptr;
ChatFn g_talk = nullptr, g_talkedTo = nullptr;
RandomFn g_random = nullptr;
LightFn g_light = nullptr;
SwingFn g_swing = nullptr;
void* g_aiCode = nullptr;

bool wants(void* npc, uint32_t flag) {
    if (!npc || g_type < 0) return false;
    uint32_t type = *reinterpret_cast<uint32_t*>(static_cast<uint8_t*>(npc) + g_type);
    return ((g_all.load(std::memory_order_relaxed) |
        (type < g_types.size() ? g_types[type].load(std::memory_order_relaxed) : 0)) & flag) != 0;
}

float scalar(RegisterFrame* frame, int index) { float value; std::memcpy(&value, frame->vectors[index], 4); return value; }
void scalar(RegisterFrame* frame, int index, float value) { std::memcpy(frame->vectors[index], &value, 4); }
float& stackFloat(RegisterFrame* frame, size_t offset) { return *reinterpret_cast<float*>(reinterpret_cast<uint8_t*>(frame + 1) + offset); }
int32_t& stackInt(RegisterFrame* frame, size_t offset) { return *reinterpret_cast<int32_t*>(reinterpret_cast<uint8_t*>(frame + 1) + offset); }

bool canChat(void* npc, bool original) {
    auto callback = g_callback.load(std::memory_order_acquire);
    if (!callback || !wants(npc, 4)) return original;
    int32_t result = original ? 1 : 0;
    callback(6, npc, &result);
    return result != 0;
}
bool talk(void* npc, const MethodInfo* method) { return canChat(npc, g_talk(npc, method)); }
bool talkedTo(void* npc, const MethodInfo* method) { return canChat(npc, g_talkedTo(npc, method)); }

void ai(void* npc, const MethodInfo* method) {
    if (!wants(npc, 1)) { g_ai(npc, method); return; }
    AttackScope scope{npc, -1, {}, t_scope};
    struct Restore { AttackScope* outer; ~Restore() { t_scope = outer; } } restore{t_scope};
    t_scope = &scope;
    g_ai(npc, method);
}

Float2 randomVector(void* random, float min, float max, const MethodInfo* method) {
    auto caller = reinterpret_cast<uintptr_t>(__builtin_return_address(0));
    auto code = reinterpret_cast<uintptr_t>(g_aiCode);
    if (t_scope && (caller == code + 0x6d94 || caller == code + 0xaf38 || caller == code + 0xb868)) { max = t_scope->args.random; min = -max; }
    return g_random(random, min, max, method);
}

void light(Float2 position, float r, float g, float b, const MethodInfo* method) {
    if (t_scope && t_scope->mode == 2 && __builtin_return_address(0) == static_cast<uint8_t*>(g_aiCode) + 0xcb54) { float scale = t_scope->args.aura; r *= scale; g *= scale; b *= scale; }
    g_light(position, r, g, b, method);
}

void* swing(void* npc, int32_t max, int32_t current, int32_t direction, int32_t width, int32_t height, const MethodInfo* method) {
    if (t_scope && t_scope->npc == npc && t_scope->mode == 3 && __builtin_return_address(0) == static_cast<uint8_t*>(g_aiCode) + 0xbbec) { width = t_scope->args.width; height = t_scope->args.height; }
    return g_swing(npc, max, current, direction, width, height, method);
}

void attack(RegisterFrame* frame, int mode) {
    void* npc = reinterpret_cast<void*>(frame->general[19]);
    auto callback = g_callback.load(std::memory_order_acquire);
    if (!callback || !t_scope || t_scope->npc != npc || !wants(npc, 1)) return;
    AttackArgs args{};
    args.damage = static_cast<int32_t>(frame->general[8]);
    args.aura = 1.f;
    if (mode == 0) {
        args.knockback = scalar(frame, 8); args.speed = scalar(frame, 9);
        args.cooldown = frame->general[21]; args.extra = frame->general[23];
        args.projectile = frame->general[20]; args.delay = static_cast<int32_t>(scalar(frame, 11));
        args.gravity = stackFloat(frame, 0x30);
    } else if (mode == 1) {
        args.knockback = scalar(frame, 8); args.speed = stackFloat(frame, 0x2c);
        args.cooldown = frame->general[22]; args.extra = frame->general[23];
        args.projectile = stackInt(frame, 0xa4); args.delay = frame->general[20];
        args.gravity = -scalar(frame, 11); args.random = scalar(frame, 9);
        args.inBetween = frame->general[24] != 0;
    } else if (mode == 2) {
        args.knockback = stackFloat(frame, 0xa4); args.speed = scalar(frame, 9);
        args.cooldown = stackInt(frame, 0x9c); args.extra = stackInt(frame, 0x8c);
        args.projectile = frame->general[27]; args.delay = frame->general[23];
        args.gravity = scalar(frame, 13); args.random = scalar(frame, 8);
    } else {
        args.knockback = scalar(frame, 11);
        args.cooldown = stackInt(frame, 0xa4); args.extra = stackInt(frame, 0x9c);
        args.width = args.height = frame->general[27];
    }
    callback(mode, npc, &args);
    args.extra = std::max(1, args.extra);
    args.width = std::max(1, args.width); args.height = std::max(1, args.height);
    if (!std::isfinite(args.random)) args.random = 0;
    if (!std::isfinite(args.aura)) args.aura = 1;
    t_scope->mode = mode; t_scope->args = args;
    frame->general[8] = static_cast<uint32_t>(args.damage);
    if (mode == 0) {
        scalar(frame, 8, args.knockback); scalar(frame, 9, args.speed); scalar(frame, 11, static_cast<float>(args.delay));
        frame->general[21] = args.cooldown; frame->general[23] = args.extra; frame->general[20] = args.projectile;
        stackFloat(frame, 0x30) = args.gravity;
    } else if (mode == 1) {
        scalar(frame, 8, args.knockback); stackFloat(frame, 0x2c) = args.speed;
        frame->general[22] = args.cooldown; frame->general[23] = args.extra; frame->general[20] = args.delay;
        stackInt(frame, 0xa4) = args.projectile; scalar(frame, 11, -args.gravity); scalar(frame, 9, args.random);
        frame->general[24] = args.inBetween != 0;
    } else if (mode == 2) {
        stackFloat(frame, 0xa4) = args.knockback; scalar(frame, 9, args.speed);
        stackInt(frame, 0x9c) = args.cooldown; stackInt(frame, 0x8c) = args.extra;
        frame->general[27] = args.projectile; frame->general[23] = args.delay;
        scalar(frame, 13, args.gravity); scalar(frame, 8, args.random);
    } else {
        scalar(frame, 11, args.knockback); stackInt(frame, 0xa4) = args.cooldown; stackInt(frame, 0x9c) = args.extra;
        frame->general[27] = args.width;
    }
}

void draw(RegisterFrame* frame, int mode) {
    void* npc = reinterpret_cast<void*>(frame->general[20]);
    auto callback = g_callback.load(std::memory_order_acquire);
    if (!callback || !wants(npc, 2)) return;
    auto* stack = reinterpret_cast<uint8_t*>(frame + 1);
    size_t tex = mode == 4 ? 0x1e8 : 0x1c8, rect = mode == 4 ? 0x1d8 : 0x1b8;
    DrawArgs args{*reinterpret_cast<void**>(stack + tex), *reinterpret_cast<Rectangle*>(stack + rect),
        scalar(frame, mode == 4 ? 13 : 10), static_cast<int32_t>(frame->general[mode == 4 ? 19 : 23]), {scalar(frame, 8), scalar(frame, 9)}};
    callback(mode, npc, &args);
    *reinterpret_cast<void**>(stack + tex) = args.texture;
    *reinterpret_cast<Rectangle*>(stack + rect) = args.frame;
    scalar(frame, mode == 4 ? 13 : 10, args.scale);
    frame->general[mode == 4 ? 19 : 23] = args.size;
    if (mode == 5) { scalar(frame, 8, args.offset.x); scalar(frame, 9, args.offset.y); }
}

bool entry(Il2CppClass* cls, const char* signature, void* replacement, void** original) {
    auto* method = il2cpp::findMethodBySignature(cls, il2cpp::parseSignature(signature));
    if (method && hook::install(il2cpp::methodPointer(method), replacement, original)) return true;
    BL_ERROR("Town NPC hooks: cannot hook %s", signature);
    return false;
}

bool stage(Il2CppClass* cls, const char* signature, size_t offset, std::array<uint32_t, 4> expected, void* bridge, void** original) {
    auto* method = il2cpp::findMethodBySignature(cls, il2cpp::parseSignature(signature));
    auto* code = method ? static_cast<uint8_t*>(il2cpp::methodPointer(method)) : nullptr;
    if (!code || std::memcmp(code + offset, expected.data(), 16) != 0 || !hook::install(code + offset, bridge, original)) {
        BL_ERROR("Town NPC hooks: cannot hook %s at +0x%zx", signature, offset);
        return false;
    }
    return true;
}
}

extern "C" __attribute__((visibility("hidden"))) void blTownThrowStage(RegisterFrame* frame) { attack(frame, 0); }
__attribute__((naked)) void blTownThrowBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blTownThrowStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blTownThrowOriginal\n" "ldr x16, [x16, :lo12:g_blTownThrowOriginal]\n" "br x16\n");
}

extern "C" __attribute__((visibility("hidden"))) void blTownShootStage(RegisterFrame* frame) { attack(frame, 1); }
__attribute__((naked)) void blTownShootBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blTownShootStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blTownShootOriginal\n" "ldr x16, [x16, :lo12:g_blTownShootOriginal]\n" "br x16\n");
}

extern "C" __attribute__((visibility("hidden"))) void blTownMagicStage(RegisterFrame* frame) { attack(frame, 2); }
__attribute__((naked)) void blTownMagicBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blTownMagicStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blTownMagicOriginal\n" "ldr x16, [x16, :lo12:g_blTownMagicOriginal]\n" "br x16\n");
}

extern "C" __attribute__((visibility("hidden"))) void blTownSwingStage(RegisterFrame* frame) { attack(frame, 3); }
__attribute__((naked)) void blTownSwingBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blTownSwingStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blTownSwingOriginal\n" "ldr x16, [x16, :lo12:g_blTownSwingOriginal]\n" "br x16\n");
}

extern "C" __attribute__((visibility("hidden"))) void blTownSwingRectStage(RegisterFrame* frame) { if (t_scope && t_scope->mode == 3) { stackInt(frame, 0x13c) -= t_scope->args.height - t_scope->args.width; stackInt(frame, 0x144) = t_scope->args.height; } }
__attribute__((naked)) void blTownSwingRectBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blTownSwingRectStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blTownSwingRectOriginal\n" "ldr x16, [x16, :lo12:g_blTownSwingRectOriginal]\n" "br x16\n");
}

extern "C" __attribute__((visibility("hidden"))) void blTownGunDrawStage(RegisterFrame* frame) { draw(frame, 4); }
__attribute__((naked)) void blTownGunDrawBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blTownGunDrawStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blTownGunDrawOriginal\n" "ldr x16, [x16, :lo12:g_blTownGunDrawOriginal]\n" "br x16\n");
}

extern "C" __attribute__((visibility("hidden"))) void blTownSwingDrawStage(RegisterFrame* frame) { draw(frame, 5); }
__attribute__((naked)) void blTownSwingDrawBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blTownSwingDrawStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blTownSwingDrawOriginal\n" "ldr x16, [x16, :lo12:g_blTownSwingDrawOriginal]\n" "br x16\n");
}

namespace bl::runtime {
bool installNpcTownHooks(int32_t type, uint32_t flags, NpcTownCallback callback) {
    if (!callback || type < -1 || type >= static_cast<int32_t>(g_types.size())) return false;
    std::lock_guard<std::mutex> lock(g_gate);
    if (g_failed) return false;
    if (!g_npc) {
        g_npc = il2cpp::findClass({"Terraria", "NPC", ""});
        g_type = g_npc ? il2cpp::fieldOffset(g_npc, "type") : -1;
        if (g_type < 0) return false;
    }
    uint32_t missing = flags & ~g_installed;
    if (missing & 1) {
        constexpr const char* sig = "void AI_007_TownEntities()";
        auto* method = il2cpp::findMethodBySignature(g_npc, il2cpp::parseSignature(sig));
        g_aiCode = method ? il2cpp::methodPointer(method) : nullptr;
        if (!g_aiCode) return false;
        {
            constexpr std::array<uint32_t, 4> expected{0x1e270100, 0xaa1903e0, 0xaa1f03e1, 0x9416062b};
            if (std::memcmp(static_cast<uint8_t*>(g_aiCode) + 0x6d84, expected.data(), 16) != 0) { BL_ERROR("Town NPC hooks: unexpected throw random caller"); return false; }
        }
        {
            constexpr std::array<uint32_t, 4> expected{0xaa1903e0, 0x4ea91d21, 0xaa1f03e1, 0x9415f5c2};
            if (std::memcmp(static_cast<uint8_t*>(g_aiCode) + 0xaf28, expected.data(), 16) != 0) { BL_ERROR("Town NPC hooks: unexpected shoot random caller"); return false; }
        }
        {
            constexpr std::array<uint32_t, 4> expected{0xaa1703e0, 0x4ea81d01, 0xaa1f03e1, 0x9415f376};
            if (std::memcmp(static_cast<uint8_t*>(g_aiCode) + 0xb858, expected.data(), 16) != 0) { BL_ERROR("Town NPC hooks: unexpected magic random caller"); return false; }
        }
        {
            constexpr std::array<uint32_t, 4> expected{0x4ea91d23, 0x4eaa1d44, 0xaa1f03e0, 0x97ff214e};
            if (std::memcmp(static_cast<uint8_t*>(g_aiCode) + 0xcb44, expected.data(), 16) != 0) { BL_ERROR("Town NPC hooks: unexpected aura light caller"); return false; }
        }
        {
            constexpr std::array<uint32_t, 4> expected{0x2a1b03e4, 0x2a1b03e5, 0xaa1f03e6, 0x9404feff};
            if (std::memcmp(static_cast<uint8_t*>(g_aiCode) + 0xbbdc, expected.data(), 16) != 0) { BL_ERROR("Town NPC hooks: unexpected swing stats caller"); return false; }
        }
        std::array<uint32_t, 4> damage{0x1e220100, 0x1e200980, 0xaa1303e0, 0xaa1f03e1};
        g_failed = true;
        if (!stage(g_npc, sig, 0x69e4, damage, reinterpret_cast<void*>(blTownThrowBridge), &g_blTownThrowOriginal)) return false;
        if (!stage(g_npc, sig, 0xac6c, damage, reinterpret_cast<void*>(blTownShootBridge), &g_blTownShootOriginal)) return false;
        if (!stage(g_npc, sig, 0xb4dc, damage, reinterpret_cast<void*>(blTownMagicBridge), &g_blTownMagicOriginal)) return false;
        if (!stage(g_npc, sig, 0xbb0c, damage, reinterpret_cast<void*>(blTownSwingBridge), &g_blTownSwingOriginal)) return false;
        if (!stage(g_npc, sig, 0xbc48, {0xb9421a68, 0x3100051f, 0x54000081, 0xb9413be8}, reinterpret_cast<void*>(blTownSwingRectBridge), &g_blTownSwingRectOriginal)) return false;
        if (!entry(g_npc, "Tuple<Vector2, float> GetSwingStats(int swingMax, int swingCurrent, int aimDir, int itemWidth, int itemHeight)", reinterpret_cast<void*>(swing), reinterpret_cast<void**>(&g_swing))) return false;
        auto* utils = il2cpp::findClass({"Terraria", "Utils", ""});
        auto* lighting = il2cpp::findClass({"Terraria", "Lighting", ""});
        if (!entry(utils, "Vector2 RandomVector2(UnifiedRandom random, float min, float max)", reinterpret_cast<void*>(randomVector), reinterpret_cast<void**>(&g_random))) return false;
        if (!entry(lighting, "void AddLight(Vector2 position, float r, float g, float b)", reinterpret_cast<void*>(light), reinterpret_cast<void**>(&g_light))) return false;
        if (!entry(g_npc, sig, reinterpret_cast<void*>(ai), reinterpret_cast<void**>(&g_ai))) return false;
        g_failed = false;
    }
    if (missing & 2) {
        auto* main = il2cpp::findClass({"Terraria", "Main", ""});
        constexpr const char* sig = "void DrawNPCExtras(NPC n, bool beforeDraw, float addHeight, float addY, Color npcColor, Vector2 halfSize, SpriteEffects npcSpriteEffect, Vector2 screenPosition)";
        g_failed = true;
        if (!stage(main, sig, 0xf94, {0x4b1303e8, 0x710002bf, 0x1e220100, 0x1a95a6a8}, reinterpret_cast<void*>(blTownGunDrawBridge), &g_blTownGunDrawOriginal)) return false;
        if (!stage(main, sig, 0x1ccc, {0xf9400360, 0xb940e008, 0x35000068, 0x97da0109}, reinterpret_cast<void*>(blTownSwingDrawBridge), &g_blTownSwingDrawOriginal)) return false;
        g_failed = false;
    }
    if (missing & 4) {
        g_failed = true;
        if (!entry(g_npc, "bool get_CanTalk()", reinterpret_cast<void*>(talk), reinterpret_cast<void**>(&g_talk))) return false;
        if (!entry(g_npc, "bool get_CanBeTalkedTo()", reinterpret_cast<void*>(talkedTo), reinterpret_cast<void**>(&g_talkedTo))) return false;
        g_failed = false;
    }
    g_installed |= missing;
    g_callback.store(callback, std::memory_order_release);
    if (type < 0) g_all.fetch_or(flags, std::memory_order_relaxed);
    else g_types[type].fetch_or(flags, std::memory_order_relaxed);
    return true;
}
}
