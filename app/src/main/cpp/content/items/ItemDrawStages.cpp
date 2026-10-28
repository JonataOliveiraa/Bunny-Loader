#include "content/items/ItemDrawStages.h"

#if defined(__aarch64__)
#include "core/Log.h"
#include "hook/Arm64Frame.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <cmath>
#include <cstring>
#include <mutex>

namespace bl::runtime::content {

namespace {

constexpr int kListeners = 2;

struct Stage {
    size_t offset;
    uint32_t expected[4];
    void (*bridge)();
    void* original = nullptr;
    void* entry = nullptr;
};

struct Pair {
    const char* name;
    const char* space;
    const char* owner;
    const char* signature;
    Stage pre, post;
    bool installed = false;
    ItemDrawListener listeners[kListeners];
    std::atomic<int> count{0};
};

extern Pair pairs[2];
std::mutex g_installMx;
thread_local bool inCallback[2] = {};

float scalar(const RegisterFrame* frame, unsigned index) {
    float value;
    std::memcpy(&value, frame->vectors[index], sizeof(value));
    return value;
}

void scalar(RegisterFrame* frame, unsigned index, float value) { std::memcpy(frame->vectors[index], &value, sizeof(value)); }

bool anyActive(const Pair& pair, int count) {
    for (int i = 0; i < count; i++) {
        if (pair.listeners[i].active->load(std::memory_order_relaxed)) return true;
    }
    return false;
}

struct CallbackGuard {
    bool& active;
    explicit CallbackGuard(bool& value) : active(value) { active = true; }
    ~CallbackGuard() { active = false; }
};

}

extern "C" __attribute__((visibility("hidden"))) void* blItemDrawDispatch(unsigned index, RegisterFrame* frame) {
    Pair& pair = pairs[index / 2];
    const bool post = index & 1;
    Stage& stage = post ? pair.post : pair.pre;
    const int count = pair.count.load(std::memory_order_acquire);
    if (inCallback[index / 2] || !anyActive(pair, count)) return stage.original;
    CallbackGuard guard(inCallback[index / 2]);
    if (post) {
        for (int i = 0; i < count; i++) {
            const ItemDrawListener& l = pair.listeners[i];
            if (l.active->load(std::memory_order_relaxed)) l.post(l.user);
        }
        return stage.original;
    }
    auto* stack = reinterpret_cast<uint8_t*>(frame) + sizeof(RegisterFrame);
    bool go = true;
    if (index == 0) {
        ItemDrawWorld args{};
        std::memcpy(&args.light, &frame->general[23], 4);
        std::memcpy(&args.alpha, stack + 0x248, 4);
        args.rotation = scalar(frame, 11);
        std::memcpy(&args.scale, stack + 0x24c, 4);
        for (int i = 0; i < count; i++) {
            const ItemDrawListener& l = pair.listeners[i];
            if (l.active->load(std::memory_order_relaxed)) go = l.pre(l.user, &args) && go;
        }
        if (std::isfinite(args.rotation)) scalar(frame, 11, args.rotation);
        if (std::isfinite(args.scale)) std::memcpy(stack + 0x24c, &args.scale, 4);
    } else {
        ItemDrawInventory args{};
        args.item = reinterpret_cast<void*>(frame->general[21]);
        args.batch = reinterpret_cast<void*>(frame->general[29]);
        args.position[0] = scalar(frame, 10);
        args.position[1] = scalar(frame, 9);
        std::memcpy(args.frame, stack + 0x118, 16);
        std::memcpy(&args.light, stack + 0x114, 4);
        std::memcpy(&args.environment, &frame->general[22], 4);
        args.origin[0] = scalar(frame, 15);
        args.origin[1] = scalar(frame, 8);
        std::memcpy(&args.scale, stack + 0x110, 4);
        for (int i = 0; i < count; i++) {
            const ItemDrawListener& l = pair.listeners[i];
            if (l.active->load(std::memory_order_relaxed)) go = l.pre(l.user, &args) && go;
        }
        if (!go) {
            float scale;
            std::memcpy(&scale, stack + 0x110, 4);
            scalar(frame, 13, scale);
        }
    }
    return go ? stage.original : pair.post.entry;
}

#define BL_ITEM_BRIDGE(name, index) \
__attribute__((naked)) void name() { \
    __asm__ volatile(BL_ARM64_SAVE_FRAME \
        "mov x1, sp\n" "mov w0, #" #index "\n" "bl blItemDrawDispatch\n" \
        "str x0, [sp, #128]\n" BL_ARM64_RESTORE_FRAME "br x16\n"); \
}
BL_ITEM_BRIDGE(worldBefore, 0)
BL_ITEM_BRIDGE(worldAfter, 1)
BL_ITEM_BRIDGE(inventoryBefore, 2)
BL_ITEM_BRIDGE(inventoryAfter, 3)
#undef BL_ITEM_BRIDGE

namespace {

Pair pairs[2] = {
    {"World", "Terraria", "Main", "void DrawItem(WorldItem item, int whoami)",
        {0x418, {0x79c14108, 0xd000fdd9, 0xaa1f03e0, 0xb9001be8}, worldBefore}, {0x20a8, {0x910a03ff, 0xa9497bfd, 0xa9484ff4, 0xa94757f6}, worldAfter}},
    {"Inventory", "Terraria.UI", "ItemSlot", "float DrawItemIcon(Item item, int context, SpriteBatch spriteBatch, Vector2 screenPositionForItemCenter, float scale, float sizeLimit, Color environmentColor, float itemFade, bool flip)",
        {0x380, {0x910363e0, 0xaa1703e1, 0xaa1803e2, 0xa90dffff}, inventoryBefore}, {0x1198, {0x4ead1da0, 0xa95c7bfd, 0xa95b4ff4, 0xa95a57f6}, inventoryAfter}}
};

bool installStages(Pair& pair) {
    if (pair.installed) return true;
    using namespace bl::il2cpp;
    auto* cls = findClass({pair.space, pair.owner, ""});
    auto* method = cls ? findMethodBySignature(cls, parseSignature(pair.signature)) : nullptr;
    auto* entry = method ? static_cast<uint8_t*>(methodPointer(method)) : nullptr;
    for (Stage* stage : {&pair.pre, &pair.post}) {
        if (!entry || std::memcmp(entry + stage->offset, stage->expected, sizeof(stage->expected))) {
            BL_ERROR("ItemDraw.%s: the game's method body is not the expected one", pair.name);
            return false;
        }
        stage->entry = entry + stage->offset;
    }
    for (Stage* stage : {&pair.post, &pair.pre}) {
        if (!bl::hook::install(stage->entry, reinterpret_cast<void*>(stage->bridge), &stage->original)) {
            BL_ERROR("ItemDraw.%s: the native hook was refused", pair.name);
            return false;
        }
    }
    pair.installed = true;
    return true;
}

}

bool addItemDrawListener(ItemDrawKind kind, const ItemDrawListener& listener) {
    const int index = static_cast<int>(kind);
    if (index < 0 || index > 1 || !listener.active || !listener.pre || !listener.post) return false;
    std::lock_guard<std::mutex> lock(g_installMx);
    Pair& pair = pairs[index];
    const int count = pair.count.load(std::memory_order_relaxed);
    if (count >= kListeners || !installStages(pair)) return false;
    pair.listeners[count] = listener;
    pair.count.store(count + 1, std::memory_order_release);
    return true;
}

}

#else

namespace bl::runtime::content {
bool addItemDrawListener(ItemDrawKind, const ItemDrawListener&) { return false; }
}

#endif
