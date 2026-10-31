using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsDust.Content;

public sealed class TestDust : ModDust
{
    public static int Spawned, Updated, StaticCalls;
    public static int LastFrameX = -1;

    public override void SetStaticDefaults() => StaticCalls++;

    public override void OnSpawn(Dust dust)
    {
        Spawned++;
        LastFrameX = dust.frame.X;
        dust.noGravity = true;
        dust.velocity = new Vector2(0f, -1f);
        dust.fadeIn = 0f;
    }

    public override bool Update(Dust dust)
    {
        Updated++;
        dust.position += dust.velocity;
        dust.scale -= 0.02f;
        if (dust.scale < 0.3f) dust.active = false;
        return false;
    }
}

public sealed class SheetDust : ModDust
{
    public static int MidUpdates, Alphas;

    public override string Texture => null!;

    public override void SetStaticDefaults() => UpdateType = DustID.Torch;

    public override bool MidUpdate(Dust dust)
    {
        MidUpdates++;
        return false;
    }

    public override Color? GetAlpha(Dust dust, Color lightColor)
    {
        Alphas++;
        return new Color(255, 0, 0, 255);
    }
}

public sealed class DrawDust : ModDust
{
    public static int PreDraws;

    public override bool PreDraw(Dust dust)
    {
        PreDraws++;
        return false;
    }
}
