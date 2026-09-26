#include "content/common/GameRefs.h"
#include "core/Log.h"
#include "il2cpp/Resolver.h"
#include <atomic>

namespace bl::runtime {

GameRefs& game() {
    static GameRefs instance;
    return instance;
}

// Idempotente: o hook (ARM) e a sonda de resolucao (emulador) podem ambos
// chamar resolveGameRefs; so resolve de fato uma vez.
namespace { std::atomic<bool> g_resolved{false}; }

namespace {

// Conta quantos itens obrigatórios ficaram sem resolver, logando cada um.
int missing = 0;

void needOffset(const char* what, int32_t value) {
    if (value < 0) { BL_ERROR("nao resolvido: %s", what); ++missing; }
}

void needPointer(const char* what, const void* value) {
    if (!value) { BL_ERROR("nao resolvido: %s", what); ++missing; }
}

} // namespace

bool resolveGameRefs() {
    if (g_resolved.load()) return true;
    using namespace il2cpp;
    auto& g = game();
    auto& a = api();
    missing = 0;

    // --- Terraria.Entity: campos compartilhados ---
    Il2CppClass* entity = findClass({"Terraria", "Entity", {}});
    needPointer("Terraria.Entity", entity);
    if (!entity) return false;

    g.entity.whoAmI   = fieldOffset(entity, "whoAmI");
    g.entity.position = fieldOffset(entity, "position");
    g.entity.velocity = fieldOffset(entity, "velocity");
    g.entity.width    = fieldOffset(entity, "width");
    g.entity.height   = fieldOffset(entity, "height");
    needOffset("Entity.whoAmI", g.entity.whoAmI);
    needOffset("Entity.position", g.entity.position);
    needOffset("Entity.velocity", g.entity.velocity);
    needOffset("Entity.width", g.entity.width);
    needOffset("Entity.height", g.entity.height);

    // --- Terraria.Projectile ---
    g.proj.cls = findClass({"Terraria", "Projectile", {}});
    needPointer("Terraria.Projectile", g.proj.cls);
    if (!g.proj.cls) return false;

    // `active` é declarado em Projectile; findField sobe a hierarquia, então
    // resolver a partir da subclasse cobre os dois casos.
    g.proj.active   = fieldOffset(g.proj.cls, "active");
    g.proj.rotation = fieldOffset(g.proj.cls, "rotation");
    g.proj.type     = fieldOffset(g.proj.cls, "type");
    g.proj.owner    = fieldOffset(g.proj.cls, "owner");
    g.proj.timeLeft = fieldOffset(g.proj.cls, "timeLeft");
    g.proj.damage   = fieldOffset(g.proj.cls, "damage");
    needOffset("Projectile.active", g.proj.active);
    needOffset("Projectile.rotation", g.proj.rotation);
    needOffset("Projectile.type", g.proj.type);
    needOffset("Projectile.owner", g.proj.owner);
    needOffset("Projectile.timeLeft", g.proj.timeLeft);
    needOffset("Projectile.damage", g.proj.damage);

    g.proj.setDefaults = a.class_get_method_from_name(g.proj.cls, "SetDefaults", 1);
    g.proj.aiMethod    = a.class_get_method_from_name(g.proj.cls, "AI", 0);
    g.proj.kill        = a.class_get_method_from_name(g.proj.cls, "Kill", 0);
    g.proj.update      = a.class_get_method_from_name(g.proj.cls, "Update", 1);
    needPointer("Projectile.SetDefaults(int)", g.proj.setDefaults);
    needPointer("Projectile.AI()", g.proj.aiMethod);
    needPointer("Projectile.Kill()", g.proj.kill);
    needPointer("Projectile.Update(int)", g.proj.update);

    if (missing > 0) {
        BL_ERROR("GameRefs: %d item(ns) nao resolvido(s) — jogo incompativel", missing);
        return false;
    }

    // Confere contra o dump (1.4.5.6.4) para provar que a resolucao bateu com
    // a realidade: velocity=0x1C, Projectile.type=0x64, active=0x49.
    BL_INFO("GameRefs ok | Entity.velocity=0x%X (esp 0x1C) Projectile.type=0x%X (esp 0x64) active=0x%X (esp 0x49)",
            g.entity.velocity, g.proj.type, g.proj.active);
    g_resolved.store(true);
    return true;
}

} // namespace bl::runtime
