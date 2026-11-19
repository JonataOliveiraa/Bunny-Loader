#include "content/common/ModContent.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "content/items/ModItems.h"
#include "content/npcs/ModNpcs.h"
#include "content/buffs/ModBuffs.h"
#include "content/dusts/ModDusts.h"
#include "content/tiles/ModTiles.h"
#include "content/walls/ModWalls.h"
#include "content/projectiles/ModProjectiles.h"
#include "content/common/ModTextures.h"
#include "menu/Cheats.h"

#include <atomic>

namespace bl::runtime {

namespace {

constexpr int kMaxReadyHooks = 4;
std::atomic<ContentReadyHook> g_hooks[kMaxReadyHooks]{};
constexpr int kMaxFrameHooks = 4;
std::atomic<FrameHook> g_frameHooks[kMaxFrameHooks]{};
bool g_done = false;
bool g_finished = false;

constexpr int kLoadingBudgetMicros = 10000;
constexpr int kMenuBudgetMicros = 8000;
constexpr int kStatusBudgetMicros = 50000;
constexpr int kWorldBudgetMicros = 1000;
constexpr int kStatusMenuMode = 10;

int currentMenuMode() {
    static const MethodInfo* getter = [] {
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        return main ? il2cpp::api().class_get_method_from_name(main, "get_menuMode", 0) : nullptr;
    }();
    if (!getter) return -1;
    Il2CppObject* exc = nullptr;
    Il2CppObject* boxed = il2cpp::api().runtime_invoke(getter, nullptr, nullptr, &exc);
    if (exc || !boxed) return -1;
    return *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject));
}

int frameBudgetMicros() {
    if (!g_finished) return kLoadingBudgetMicros;
    if (inWorld()) return kWorldBudgetMicros;
    return currentMenuMode() == kStatusMenuMode ? kStatusBudgetMicros : kMenuBudgetMicros;
}

void runFrameHooks(int budget) {
    for (auto& slot : g_frameHooks) {
        FrameHook hook = slot.load(std::memory_order_acquire);
        if (hook && hook(budget) == 0) slot.store(nullptr, std::memory_order_release);
    }
}

struct Refs {
    FieldInfo *itemDrops = nullptr, *bestiary = nullptr, *numRecipes = nullptr;
    bool ok = false;
};

const Refs& refs() {
    static Refs r = [] {
        Refs x;
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        Il2CppClass* recipe = il2cpp::findClass({"Terraria", "Recipe", {}});
        x.itemDrops = main ? il2cpp::findField(main, "ItemDropsDB") : nullptr;
        x.bestiary = main ? il2cpp::findField(main, "BestiaryDB") : nullptr;
        x.numRecipes = recipe ? il2cpp::findField(recipe, "numRecipes") : nullptr;
        x.ok = x.itemDrops && x.bestiary && x.numRecipes;
        if (!x.ok) BL_ERROR("mod content: ItemDropsDB/BestiaryDB/numRecipes references missing");
        return x;
    }();
    return r;
}

template <typename T>
T readStatic(FieldInfo* f) {
    T v{};
    il2cpp::api().field_static_get_value(f, &v);
    return v;
}

}

void addContentReadyHook(ContentReadyHook hook) {
    if (!hook) return;
    for (auto& slot : g_hooks) {
        ContentReadyHook expected = nullptr;
        if (slot.compare_exchange_strong(expected, hook, std::memory_order_acq_rel) || expected == hook) return;
    }
    BL_ERROR("mod content: no room for another content-ready hook");
}

void addFrameHook(FrameHook hook) {
    if (!hook) return;
    for (auto& slot : g_frameHooks) {
        FrameHook expected = nullptr;
        if (slot.compare_exchange_strong(expected, hook, std::memory_order_acq_rel) || expected == hook) return;
    }
    BL_ERROR("mod content: no room for another frame hook");
}

bool contentSettled() {
    if (!modItemsSettled() || !modProjectilesSettled() || !modNpcsSettled() || !modBuffsSettled() || !modDustsSettled() ||
        !modTilesSettled() || !modWallsSettled()) return false;
    const Refs& r = refs();
    if (!r.ok) return true;
    if (!readStatic<Il2CppObject*>(r.itemDrops) || !readStatic<Il2CppObject*>(r.bestiary)) return false;
    return readStatic<int32_t>(r.numRecipes) > 0;
}

void tickContentReady() {
    const int budget = frameBudgetMicros();
    content::pumpModTextures(budget);
    if (g_finished) {
        runFrameHooks(budget);
        return;
    }
    if (!g_hooks[0].load(std::memory_order_acquire)) return;
    if (!g_done) {
        if (!contentSettled()) return;
        g_done = true;
        const Refs& r = refs();
        if (!r.ok) {
            g_finished = true;
            return;
        }
        BL_INFO("mod content: ready (game recipes: %d)", readStatic<int32_t>(r.numRecipes));
    }
    bool more = false;
    for (const auto& slot : g_hooks) {
        if (ContentReadyHook hook = slot.load(std::memory_order_acquire)) more |= hook() != 0;
    }
    g_finished = !more;
}

bool contentFinished() {
    return g_finished || !g_hooks[0].load(std::memory_order_acquire);
}

}
