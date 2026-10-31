using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsGrapple.Content;

public sealed class TestHook : ModProjectile
{
    public static bool? CanUse, Latch;
    public static int SwapTo, Limit = 3, CanUseCalls, LatchX = -1, LatchY = -1;
    public static float Range = 300f, Retreat = 11f, Pull = 11f, OffsetX;

    public override void SetDefaults() => Projectile.CloneDefaults(ProjectileID.GemHookAmethyst);

    public override bool? CanUseGrapple(Player player)
    {
        CanUseCalls++;
        return CanUse;
    }

    public override void UseGrapple(Player player, ref int type)
    {
        if (SwapTo > 0) type = SwapTo;
    }

    public override float GrappleRange() => Range;

    public override void NumGrappleHooks(Player player, ref int numHooks) => numHooks = Limit;

    public override void GrappleRetreatSpeed(Player player, ref float speed) => speed = Retreat;

    public override void GrapplePullSpeed(Player player, ref float speed) => speed = Pull;

    public override void GrappleTargetPoint(Player player, ref float grappleX, ref float grappleY) => grappleX += OffsetX;

    public override bool? GrappleCanLatchOnTo(Player player, int x, int y)
    {
        if (Latch == true)
        {
            LatchX = x;
            LatchY = y;
        }
        return Latch;
    }
}
