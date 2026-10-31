using Terraria.ID;
using Terraria.ModLoader;

namespace CsGrapple.Content;

public sealed class OtherHook : ModProjectile
{
    public override void SetDefaults() => Projectile.CloneDefaults(ProjectileID.GemHookAmethyst);
}
