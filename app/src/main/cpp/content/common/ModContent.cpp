#include "content/common/ModContent.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "content/items/ModItems.h"
#include "content/npcs/ModNpcs.h"
#include "content/buffs/ModBuffs.h"
#include "content/tiles/ModTiles.h"
#include "content/projectiles/ModProjectiles.h"

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

bool contentSettled() {
    if (!modItemsSettled() || !modProjectilesSettled() || !modNpcsSettled() || !modBuffsSettled() ||
        !modTilesSettled()) return false;
    const Refs& r = refs();
    // Sem as refs nao ha como saber (o erro ja foi logado): quem espera por
    // isto nao fica preso para sempre.
    if (!r.ok) return true;
    if (!readStatic<Il2CppObject*>(r.itemDrops) || !readStatic<Il2CppObject*>(r.bestiary)) return false;
    return readStatic<int32_t>(r.numRecipes) > 0;
}

void tickContentReady() {
    if (g_done) return;
    ContentReadyHook hook = g_hook.load(std::memory_order_acquire);
    if (!hook) return;
    if (!contentSettled()) return;
    g_done = true;
    const Refs& r = refs();
    if (!r.ok) return;
    BL_INFO("conteudo de mod: pronto (receitas do jogo: %d)", readStatic<int32_t>(r.numRecipes));
    hook();
}

} // namespace bl::runtime
