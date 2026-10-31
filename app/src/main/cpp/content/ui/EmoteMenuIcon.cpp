#include "content/ui/EmoteMenuIcon.h"
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
__attribute__((visibility("hidden"))) void* g_blEmoteMenuIconOriginal = nullptr;
}

namespace {

using namespace bl;
std::atomic<void*> g_icon{nullptr};
std::mutex g_gate;
bool g_installed = false;

constexpr const char* kSignature = "void EmoteDraw(ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)";
constexpr size_t kOffset = 0x668;
constexpr std::array<uint32_t, 4> kExpected{0xd0013b48, 0x910163e0, 0xf9405d29, 0xaa1503e1};

}

extern "C" __attribute__((visibility("hidden"))) void blEmoteMenuIconStage(RegisterFrame* frame) {
    void* icon = g_icon.load(std::memory_order_acquire);
    if (icon) frame->general[23] = reinterpret_cast<uintptr_t>(icon);
}

__attribute__((naked)) void blEmoteMenuIconBridge() {
    __asm__ volatile(BL_ARM64_SAVE_FRAME "mov x0, sp\n" "bl blEmoteMenuIconStage\n" BL_ARM64_RESTORE_FRAME
        "adrp x16, g_blEmoteMenuIconOriginal\n" "ldr x16, [x16, :lo12:g_blEmoteMenuIconOriginal]\n" "br x16\n");
}

namespace bl::runtime {

bool installEmoteMenuIcon() {
    std::lock_guard<std::mutex> lock(g_gate);
    if (g_installed) return true;
    auto* window = il2cpp::findClass({"", "GUIEmotesWindow", ""});
    const MethodInfo* method = window ? il2cpp::findMethodBySignature(window, il2cpp::parseSignature(kSignature)) : nullptr;
    auto* code = method ? static_cast<uint8_t*>(il2cpp::methodPointer(method)) : nullptr;
    if (!code || std::memcmp(code + kOffset, kExpected.data(), sizeof(kExpected)) != 0 ||
        !hook::install(code + kOffset, reinterpret_cast<void*>(blEmoteMenuIconBridge), &g_blEmoteMenuIconOriginal)) {
        BL_ERROR("emote menu: cannot hook GUIEmotesWindow.EmoteDraw at +0x%zx", kOffset);
        return false;
    }
    g_installed = true;
    return true;
}

void setEmoteMenuIcon(void* texture) { g_icon.store(texture, std::memory_order_release); }

}
