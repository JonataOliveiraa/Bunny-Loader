using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Biomes;

public class ExampleWaterfallStyle : ModWaterfallStyle
{
    public override void AddLight(int i, int j) =>
        Lighting.AddLight(new Vector2(i, j).ToWorldCoordinates(), Color.White.ToVector3() * 0.5f);
}
