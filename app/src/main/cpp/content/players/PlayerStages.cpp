#include "content/players/PlayerStages.h"

#if defined(__aarch64__)
#include "core/Log.h"
#include "hook/Arm64Frame.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <atomic>
#include <cmath>
#include <cstring>
#include <mutex>

extern "C" {
__attribute__((visibility("hidden"))) void* g_blNaturalRegenStageOriginal = nullptr;
}

namespace {

std::atomic<bl::runtime::NaturalRegenCallback> g_naturalRegen{nullptr};
std::mutex g_mx;
int g_state = 0;

constexpr size_t kNaturalRegenOffset = 0x828;
constexpr uint32_t kNaturalRegenExpected[4] = {0xbd481e60, 0x9000a269, 0xf9400300, 0x9000a26a};

}

extern "C" __attribute__((visibility("hidden"))) void blNaturalRegenStage(RegisterFrame* frame) {
    bl::runtime::NaturalRegenCallback callback = g_naturalRegen.load(std::memory_order_acquire);
    if (!callback) return;
    float value;
    std::memcpy(&value, frame->vectors[9], sizeof(value));
    const float changed = callback(reinterpret_cast<void*>(frame->general[19]), value);
    if (std::isfinite(changed)) std::memcpy(frame->vectors[9], &changed, sizeof(changed));
}

__attribute__((naked)) void blNaturalRegenBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME
        "mov x0, sp\n" "bl blNaturalRegenStage\n"
        BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blNaturalRegenStageOriginal\n" "ldr x16, [x16, :lo12:g_blNaturalRegenStageOriginal]\n" "br x16\n");
}

namespace bl::runtime {

bool setNaturalRegenCallback(NaturalRegenCallback callback) {
    std::lock_guard<std::mutex> lock(g_mx);
    if (g_state == 0) {
        using namespace bl::il2cpp;
        auto* player = findClass({"Terraria", "Player", ""});
        const MethodInfo* method = player ? findMethodBySignature(player, parseSignature("void UpdateLifeRegen()")) : nullptr;
        auto* entry = method ? static_cast<uint8_t*>(methodPointer(method)) : nullptr;
        if (!entry || std::memcmp(entry + kNaturalRegenOffset, kNaturalRegenExpected, sizeof(kNaturalRegenExpected)) != 0) {
            BL_ERROR("NaturalLifeRegen: the game's UpdateLifeRegen body is not the expected one");
            g_state = -1;
        } else if (!hook::install(entry + kNaturalRegenOffset, reinterpret_cast<void*>(blNaturalRegenBridge), &g_blNaturalRegenStageOriginal)) {
            BL_ERROR("NaturalLifeRegen: the native hook was refused");
            g_state = -1;
        } else {
            g_state = 1;
        }
    }
    if (g_state != 1) return false;
    g_naturalRegen.store(callback, std::memory_order_release);
    return true;
}

}

#else

namespace bl::runtime {
bool setNaturalRegenCallback(NaturalRegenCallback) { return false; }
}

#endif
