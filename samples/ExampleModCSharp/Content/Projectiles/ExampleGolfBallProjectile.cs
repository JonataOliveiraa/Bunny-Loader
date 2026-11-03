using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

public class ExampleGolfBallProjectile : ModProjectile
{
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.IsAGolfBall[Type] = true;
        ProjectileID.Sets.TrailingMode[Type] = 0;
        ProjectileID.Sets.TrailCacheLength[Type] = 20;
    }

    public override void SetDefaults()
    {
        Projectile.netImportant = true;
        Projectile.width = 7;
        Projectile.height = 7;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.aiStyle = ProjAIStyleID.GolfBall;
        Projectile.tileCollide = false;
    }
}
