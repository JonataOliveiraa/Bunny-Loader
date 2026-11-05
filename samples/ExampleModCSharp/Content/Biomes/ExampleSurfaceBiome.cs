using Microsoft.Xna.Framework.Graphics;
using ExampleMod.Backgrounds;
using ExampleMod.Common.Systems;
using ExampleMod.Content.Items.Placeable;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Biomes
{
    public class ExampleSurfaceBiome : ModBiome
    {
        public override ModWaterStyle WaterStyle => ModContent.GetInstance<ExampleWaterStyle>();
        public override ModSurfaceBackgroundStyle SurfaceBackgroundStyle => ModContent.GetInstance<ExampleSurfaceBackgroundStyle>();
        public override CaptureBiome.TileColorStyle TileColorStyle => CaptureBiome.TileColorStyle.Crimson;

        public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Music/MysteriousMystery");

        public override int BiomeTorchItemType => ModContent.ItemType<ExampleTorch>();
        public override int BiomeCampfireItemType => ModContent.ItemType<ExampleCampfire>();

        public override string BestiaryIcon => base.BestiaryIcon;
        public override string BackgroundPath => base.BackgroundPath;
        public override Color? BackgroundColor => base.BackgroundColor;
        public override string MapBackground => BackgroundPath;

        public override bool IsBiomeActive(Player player) {
            bool b1 = ModContent.GetInstance<ExampleBiomeTileCount>().exampleBlockCount >= 40;

            bool b2 = Math.Abs(player.position.ToTileCoordinates().X - Main.maxTilesX / 2) < Main.maxTilesX / 6;

            bool b3 = player.ZoneSkyHeight || player.ZoneOverworldHeight;
            return b1 && b2 && b3;
        }

        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeLow;
    }
}
