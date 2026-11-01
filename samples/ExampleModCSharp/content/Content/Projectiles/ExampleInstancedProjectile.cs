using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

public class ExampleInstancedProjectile : ModProjectile
{
    private Color trailColor;

    public override void SetDefaults()
    {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.aiStyle = ProjAIStyleID.Arrow;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = true;
        AIType = ProjectileID.Bullet;
    }

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        Dust.NewDustPerfect(Projectile.Center, DustID.TintableDust, newColor: trailColor);
        return false;
    }

    public override void OnSpawn(IEntitySource data)
    {
        trailColor = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.7f);
    }
}
