using ExampleMod.Content.Dusts;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

public class ExampleYoyoProjectile : ModProjectile
{
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.YoyosLifeTimeMultiplier[Type] = 3.5f;
        ProjectileID.Sets.YoyosMaximumRange[Type] = 300f;
        ProjectileID.Sets.YoyosTopSpeed[Type] = 13f;
    }

    public override void SetDefaults()
    {
        Projectile.width = 16;
        Projectile.height = 16;

        Projectile.aiStyle = ProjAIStyleID.Yoyo;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.MeleeNoSpeed;
        Projectile.penetrate = -1;
        Projectile.drawLayer = ProjectileDrawLayerID.HeldProj;
    }

    public override void PostAI()
    {
        if (Main.rand.NextBool(5))
        {
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, ModContent.DustType<Sparkle>());
        }
    }
}
