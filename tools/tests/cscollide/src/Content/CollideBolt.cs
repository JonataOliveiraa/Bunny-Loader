using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CsCollide.Content;

public sealed class CollideBolt : ModProjectile
{
    public static bool SeenFallThrough;

    public float CollidedY = float.NaN;

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.aiStyle = 0;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.damage = 0;
        Projectile.penetrate = -1;
        Projectile.tileCollide = true;
        Projectile.timeLeft = 600;
    }

    public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac)
    {
        switch ((int)Projectile.ai[0])
        {
            case 1:
                width = 40;
                height = 40;
                hitboxCenterFrac = new Vector2(0.5f, 0.5f);
                return true;
            case 2:
                return false;
            case 3:
                fallThrough = false;
                return true;
            case 4:
                SeenFallThrough = fallThrough;
                return true;
            default:
                return true;
        }
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (float.IsNaN(CollidedY)) CollidedY = Projectile.Center.Y;
        return false;
    }
}
