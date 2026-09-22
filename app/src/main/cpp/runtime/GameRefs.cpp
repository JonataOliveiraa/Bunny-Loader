#include "runtime/GameRefs.h"
#include "core/Log.h"
#include "il2cpp/Resolver.h"

namespace bl::runtime {

GameRefs& game() {
    static GameRefs instance;
    return instance;
}

bool resolveGameRefs() {
    using namespace il2cpp;
    auto& g = game();
    auto& a = api();

    Il2CppClass* entity = findClass({"Terraria", "Entity", {}});
    if (!entity) return false;
    g.entity.position = fieldOffset(entity, "position");
    g.entity.velocity = fieldOffset(entity, "velocity");
    g.entity.width    = fieldOffset(entity, "width");
    g.entity.height   = fieldOffset(entity, "height");
    g.entity.active   = fieldOffset(entity, "active");
    g.entity.whoAmI   = fieldOffset(entity, "whoAmI");

    g.proj.cls = findClass({"Terraria", "Projectile", {}});
    if (!g.proj.cls) return false;
    g.proj.type     = fieldOffset(g.proj.cls, "type");
    g.proj.damage   = fieldOffset(g.proj.cls, "damage");
    g.proj.timeLeft = fieldOffset(g.proj.cls, "timeLeft");
    g.proj.rotation = fieldOffset(g.proj.cls, "rotation");
    g.proj.setDefaults = a.class_get_method_from_name(g.proj.cls, "SetDefaults", 1);
    g.proj.aiMethod    = a.class_get_method_from_name(g.proj.cls, "AI", 0);
    g.proj.kill        = a.class_get_method_from_name(g.proj.cls, "Kill", 0);

    // TODO(Fase 3): validate() — conferir que nenhum offset ficou -1 e nenhum
    // ponteiro ficou nulo, logando cada item faltante.
    BL_INFO("GameRefs: Projectile.type=%d velocity=%d AI=%p",
            g.proj.type, g.entity.velocity, (void*)g.proj.aiMethod);
    return true;
}

} // namespace bl::runtime
