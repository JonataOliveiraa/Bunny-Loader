using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsProjectile.Content;

public static class Counts
{
    public static readonly Dictionary<string, int> Calls = new();

    public static void Add(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public static int Of(string name) => Calls.GetValueOrDefault(name);
}

public class TestBolt : ModProjectile
{
    public static IEntitySource SpawnSource;
    public static int StaticDefaults, Bounces, TrailLength = -1;
    public static bool KilledByPenetrate;

    public override void SetStaticDefaults()
    {
        StaticDefaults++;
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
        Projectile.penetrate = 3;
        Projectile.timeLeft = 600;
        Projectile.light = 0.5f;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = true;
        AIType = ProjectileID.Bullet;
    }

    public override void OnSpawn(IEntitySource source)
    {
        Counts.Add(nameof(OnSpawn));
        SpawnSource = source;
    }

    public override bool PreAI()
    {
        Counts.Add(nameof(PreAI));
        return true;
    }

    public override void AI() => Counts.Add(nameof(AI));

    public override void PostAI() => Counts.Add(nameof(PostAI));

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Counts.Add(nameof(OnTileCollide));
        Projectile.penetrate--;
        if (Projectile.penetrate <= 0)
        {
            KilledByPenetrate = true;
            Projectile.Kill();
        }
        else
        {
            Bounces++;
            if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X;
            if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = -oldVelocity.Y;
        }
        return false;
    }

    public override void OnKill(int timeLeft) => Counts.Add(nameof(OnKill));

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        Counts.Add(nameof(PreDraw));
        TrailLength = Projectile.oldPos.Length;
        DrawData data = Projectile.GetDefaultDrawData(player, lightColor);
        for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
        {
            if (Projectile.oldPos[k] == Vector2.Zero) continue;
            DrawData trail = data;
            trail.position = data.position - Projectile.position + Projectile.oldPos[k];
            trail.color = data.color * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
            Main.EntitySpriteDraw(trail);
        }
        return true;
    }

    public override void PostDraw(Player player, Color lightColor) => Counts.Add(nameof(PostDraw));
}

public class TestFuse : ModProjectile
{
    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.aiStyle = 0;
        Projectile.timeLeft = 10;
        Projectile.tileCollide = false;
    }

    public override bool PreKill(int timeLeft)
    {
        Counts.Add("FusePreKill");
        return false;
    }

    public override void OnKill(int timeLeft) => Counts.Add("FuseOnKill");
}
