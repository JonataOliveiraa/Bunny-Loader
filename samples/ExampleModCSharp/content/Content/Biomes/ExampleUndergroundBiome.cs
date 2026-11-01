using System;
using ExampleMod.Common.Systems;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Biomes;

public class ExampleUndergroundBiome : ModBiome
{
    public override SceneEffectPriority Priority => SceneEffectPriority.BiomeLow;
    public override string BestiaryIcon => base.BestiaryIcon;
    public override string BackgroundPath => base.BackgroundPath;
    public override Color? BackgroundColor => base.BackgroundColor;

    public override bool IsBiomeActive(Player player)
    {
        return (player.ZoneRockLayerHeight || player.ZoneDirtLayerHeight) &&
            ModContent.GetInstance<ExampleBiomeTileCount>().exampleBlockCount >= 40 &&
            Math.Abs(player.position.ToTileCoordinates().X - Main.maxTilesX / 2) < Main.maxTilesX / 6;
    }
}
