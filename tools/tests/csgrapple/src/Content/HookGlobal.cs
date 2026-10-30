using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsGrapple.Content;

public sealed class HookGlobal : GlobalProjectile
{
    public static bool BlockVanilla;
    public static int RetreatCalls;

    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.Hook;

    public override bool? CanUseGrapple(int type, Player player) => BlockVanilla ? false : null;

    public override void GrappleRetreatSpeed(Projectile projectile, Player player, ref float speed)
    {
        RetreatCalls++;
        speed = 40f;
    }
}
