#include "content/projectiles/ProjectileStrikes.h"

#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <mutex>

namespace bl::runtime {

namespace {

using DamagePveFn = void (*)(void*, void*, float, const MethodInfo*);

DamagePveFn g_damagePve = nullptr;
thread_local void* t_projectile = nullptr;
std::mutex g_mx;
int g_state = 0;

void hkDamagePve(void* self, void* rectangle, float multiplier, const MethodInfo* method) {
    void* outer = t_projectile;
    t_projectile = self;
    g_damagePve(self, rectangle, multiplier, method);
    t_projectile = outer;
}

bool install() {
    using namespace bl::il2cpp;
    auto* cls = findClass({"Terraria", "Projectile", ""});
    auto* method = cls ? findMethodBySignature(cls, parseSignature("void Damage_PVE(ref Rectangle projRectangle, float projectileSpecificDamageMultiplier)")) : nullptr;
    if (!method) {
        BL_ERROR("projectile strikes: Projectile.Damage_PVE was not found");
        return false;
    }
    if (!hook::install(method, hkDamagePve, &g_damagePve)) {
        BL_ERROR("projectile strikes: the Projectile.Damage_PVE hook was refused");
        return false;
    }
    return true;
}

}

bool watchProjectileDamage() {
    std::lock_guard<std::mutex> lock(g_mx);
    if (g_state == 0) g_state = install() ? 1 : -1;
    return g_state == 1;
}

void* projectileDamaging() { return t_projectile; }

}
