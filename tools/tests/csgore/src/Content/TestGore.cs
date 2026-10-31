using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CsGore.Content;

public sealed class TestGore : ModGore
{
    public static int StaticCalls, Spawned, Updates, Alphas;
    public static bool SawSource, SawEmpty;

    public override void SetStaticDefaults() => StaticCalls++;

    public override void OnSpawn(Gore gore, IEntitySource source)
    {
        Spawned++;
        if (source.IsNull) SawEmpty = true;
        else SawSource = true;
        gore.timeLeft = 600;
    }

    public override bool Update(Gore gore)
    {
        Updates++;
        return true;
    }

    public override Color? GetAlpha(Gore gore, Color lightColor)
    {
        Alphas++;
        return new Color(0, 255, 0, 255);
    }
}
