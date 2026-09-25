#include "runtime/ModContent.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "runtime/ModItems.h"
#include "runtime/ModNpcs.h"
#include "runtime/ModBuffs.h"
#include "runtime/ModProjectiles.h"

#include <atomic>

namespace bl::runtime {

namespace {

std::atomic<ContentReadyHook> g_hook{nullptr};
bool g_done = false;

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
        if (!x.ok) BL_ERROR("conteudo de mod: refs de ItemDropsDB/BestiaryDB/numRecipes faltando");
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

} // namespace

void setContentReadyHook(ContentReadyHook hook) {
    g_hook.store(hook, std::memory_order_release);
}

void tickContentReady() {
    if (g_done) return;
    ContentReadyHook hook = g_hook.load(std::memory_order_acquire);
    if (!hook) return;
    if (!modItemsSettled() || !modProjectilesSettled() || !modNpcsSettled() || !modBuffsSettled()) return;
    const Refs& r = refs();
    if (!r.ok) { g_done = true; return; }
    if (!readStatic<Il2CppObject*>(r.itemDrops) || !readStatic<Il2CppObject*>(r.bestiary)) return;
    if (readStatic<int32_t>(r.numRecipes) <= 0) return;
    g_done = true;
    BL_INFO("conteudo de mod: pronto (receitas do jogo: %d)", readStatic<int32_t>(r.numRecipes));
    hook();
}

} // namespace bl::runtime
