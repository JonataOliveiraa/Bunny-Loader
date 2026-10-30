using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsProjExtras.Content;

public sealed class DrawHook : ModProjectile
{
    public static bool Extras = true, Sprite = true;
    public static int ExtrasCalls, PostDrawCalls;

    public override void SetDefaults()
    {
        Projectile.CloneDefaults(ProjectileID.GemHookAmethyst);
        Projectile.timeLeft = 3600;
    }

    public override void PostAI() => Projectile.velocity = Vector2.Zero;

    public override bool? GrappleCanLatchOnTo(Player player, int x, int y) => false;

    public override bool PreDrawExtras(Player player)
    {
        ExtrasCalls++;
        return Extras;
    }

    public override bool PreDraw(Player player, ref Color lightColor) => Sprite;

    public override void PostDraw(Player player, Color lightColor) => PostDrawCalls++;
}
