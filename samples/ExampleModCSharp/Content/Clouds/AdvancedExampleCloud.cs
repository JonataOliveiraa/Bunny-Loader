using System.Collections.Generic;
using ExampleMod.Content.Biomes;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ExampleMod.Content.Clouds;

public class AdvancedExampleCloud : ModCloud
{
    public override bool RareCloud => true;

    public override float SpawnChance()
    {
        if (!Main.gameMenu && Main.LocalPlayer.InModBiome<ExampleSurfaceBiome>())
        {
            return 10f;
        }
        return 1f;
    }

    public override void OnSpawn(Cloud cloud)
    {
        cloud.spriteDir = SpriteEffects.None;
    }

    public override bool Draw(List<DrawData> drawDataCache, Cloud cloud, int cloudIndex, ref DrawData drawData)
    {
        if (!Main.gameMenu && Main.LocalPlayer.InModBiome<ExampleSurfaceBiome>())
        {
            var drawDataCopy = drawData;
            drawDataCopy.color *= 0.5f;
            drawDataCopy.position += Utils.NextVector2Circular(Main.rand, 5, 5);
            drawDataCache.Add(drawDataCopy);
        }
        return true;
    }
}
