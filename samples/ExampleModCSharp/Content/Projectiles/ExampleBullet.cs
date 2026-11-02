using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

public class ExampleBullet : ModProjectile
{
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 5;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.aiStyle = ProjAIStyleID.Arrow;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 5;
        Projectile.timeLeft = 600;
        Projectile.alpha = 255;
        Projectile.light = 0.5f;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = true;
        Projectile.extraUpdates = 1;

        AIType = ProjectileID.Bullet;
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Projectile.penetrate--;
        if (Projectile.penetrate <= 0)
        {
            Projectile.Kill();
        }
        else
        {
            Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
            SoundEngine.PlaySound(SoundID.Item10, Projectile.position);

            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon)
            {
                Projectile.velocity.X = -oldVelocity.X;
            }

            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon)
            {
                Projectile.velocity.Y = -oldVelocity.Y;
            }
        }

        return false;
    }

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        var drawData = Projectile.GetDefaultDrawData(player, lightColor);

        for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
        {
            if (Projectile.oldPos[k] == Vector2.Zero)
            {
                continue;
            }

            DrawData adjustedDrawData = drawData with {
                position = drawData.position - Projectile.position + Projectile.oldPos[k],
                color = drawData.color * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length)
            };

            Main.EntitySpriteDraw(adjustedDrawData);
        }

        return true;
    }

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Item10, Projectile.position);
    }
}
