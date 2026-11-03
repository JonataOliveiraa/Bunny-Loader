using ExampleMod.Content.Dusts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles.Minions;

public class ExampleSentryShot : ModProjectile
{
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.SentryShot[Type] = true;
    }

    public override void SetDefaults()
    {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Summon;
        Projectile.timeLeft = 600;
    }

    public override void AI()
    {
        if (Main.rand.NextBool(3))
        {
            Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, ModContent.DustType<Sparkle>(), Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f, newColor: Color.SkyBlue);
        }
    }

    public override void OnKill(int timeLeft)
    {
        for (int k = 0; k < 5; k++)
        {
            Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, ModContent.DustType<Sparkle>(), Projectile.oldVelocity.X * 0.5f, Projectile.oldVelocity.Y * 0.5f, newColor: Color.SkyBlue);
        }

        SoundEngine.PlaySound(SoundID.Item25, Projectile.position);
    }
}
