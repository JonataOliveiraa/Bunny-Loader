using BunnyLoader;
using ExampleMod.Content.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

public class ExampleRiftProjectile : ModProjectile
{
    ExampleRiftEffect? rift;

    public override string Texture => "ExampleMod/Content/Projectiles/SparklingBall";

    public override void SetDefaults()
    {
        Projectile.width = 40;
        Projectile.height = 40;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 120;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 15;
    }

    public override void AI()
    {
        Projectile.velocity *= 0.96f;
        Lighting.AddLight(Projectile.Center, 0.6f, 0.3f, 0.9f);
        if (Main.rand.NextBool(2))
        {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0, 0, 100, Color.White, 1.2f);
            dust.noGravity = true;
        }
        if (Main.netMode == NetmodeID.Server)
            return;
        rift ??= ScreenEffects.Spawn<ExampleRiftEffect>(Projectile.Center);
        rift.Center = Projectile.Center;
    }

    public override bool PreDraw(Player player, ref Color lightColor) => false;

    public override void OnKill(int timeLeft)
    {
        SoundEngine.PlaySound(SoundID.Item14, Projectile.position);
        if (Main.netMode == NetmodeID.Server)
            return;
        if (rift is { Active: true })
            rift.Close();
        Vector2 center = Projectile.Center;
        ScreenEffects.Spawn<ScreenPresets.Shockwave>(center, fx =>
        {
            fx.Radius = 260;
            fx.Duration = 35;
        });
        ScreenEffects.Spawn<ScreenPresets.Flash>(fx =>
        {
            fx.Color = new Color(190, 120, 255);
            fx.Amount = 0.3f;
            fx.Duration = 12;
        });
        ScreenEffects.Spawn<ScreenPresets.ChromaticAberration>(center, fx =>
        {
            fx.Amount = 5;
            fx.Duration = 18;
            fx.FadeOut = 18;
        });
    }
}
