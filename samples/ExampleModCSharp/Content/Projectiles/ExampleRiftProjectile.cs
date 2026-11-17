using BunnyLoader;
using ExampleMod.Content.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

// The projectile shot by ExampleRiftStaff. It slows down while dragging an ExampleRiftEffect along with it, and bursts with a few ScreenPresets when it dies.
public class ExampleRiftProjectile : ModProjectile
{
    // The screen effect that follows this projectile. Kept so it can be moved every tick and closed when the projectile dies.
    ExampleRiftEffect? rift;

    // The projectile itself isn't drawn (see PreDraw), but every ModProjectile needs a texture.
    public override string Texture => "ExampleMod/Content/Projectiles/SparklingBall";

    public override void SetDefaults()
    {
        Projectile.width = 40;
        Projectile.height = 40;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1; // Infinite penetration, so it hits every enemy it touches until it dies.
        Projectile.timeLeft = 120;
        Projectile.ignoreWater = true;
        // Each rift can hit the same enemy every 15 ticks, independently from other projectiles.
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 15;
    }

    public override void AI()
    {
        Projectile.velocity *= 0.96f; // Slows down a little every tick.
        Lighting.AddLight(Projectile.Center, 0.6f, 0.3f, 0.9f);
        if (Main.rand.NextBool(2))
        {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0, 0, 100, Color.White, 1.2f);
            dust.noGravity = true;
        }
        // Screen effects are only visual, so a dedicated server has nothing to draw.
        if (Main.netMode == NetmodeID.Server)
            return;

        // The effect is spawned on the first tick, then moved to follow the projectile.
        rift ??= ScreenEffects.Spawn<ExampleRiftEffect>(Projectile.Center);
        rift.Center = Projectile.Center;
    }

    // Returning false skips drawing the projectile, since ExampleRiftEffect is what the player sees.
    public override bool PreDraw(Player player, ref Color lightColor) => false;

    public override void OnKill(int timeLeft)
    {
        SoundEngine.PlaySound(SoundID.Item14, Projectile.position);
        if (Main.netMode == NetmodeID.Server)
            return;
        if (rift is { Active: true })
            rift.Close();

        // A burst of three ready-made effects: a shockwave that ripples outwards, a quick purple flash of the whole screen,
        // and a short chromatic aberration that splits the colors around the center. Duration and FadeOut are in ticks.
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
