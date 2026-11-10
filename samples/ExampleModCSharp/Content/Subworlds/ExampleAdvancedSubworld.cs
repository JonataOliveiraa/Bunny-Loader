using System;
using System.Collections.Generic;
using ExampleMod.Common.Systems;
using ExampleMod.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace ExampleMod.Content.Subworlds;

public class ExampleAdvancedSubworld : ModSubworld
{
    public static LocalizedText WelcomeText { get; private set; }

    public override int Width => 1400;

    public override int Height => 700;

    public override bool Persistent => true;

    public override bool RandomTileUpdates => true;

    public override bool ShowLoadingScreen => false;

    public override float TransitionOutSeconds => 1.5f;

    public override float TransitionInSeconds => 1.2f;

    public override List<GenPass> GenerationTasks => new()
    {
        new PassLegacy("Hills", Hills),
        new PassLegacy("Caves", Caves),
        new PassLegacy("Example Ore", Ore),
        new PassLegacy("Spawn", Spawn),
    };

    public override void SetStaticDefaults()
    {
        WelcomeText = this.GetLocalization("Welcome");
    }

    static int SurfaceAt(int x) => (int)(Main.worldSurface + Math.Sin(x / 47.0) * 9 + Math.Sin(x / 13.0) * 3);

    static void Hills(GenerationProgress progress, GameConfiguration configuration)
    {
        for (int x = 0; x < Main.maxTilesX; x++)
        {
            int surface = SurfaceAt(x);
            int dirt = surface + WorldGen.genRand.Next(10, 14);
            Main.tile[x, surface].ResetToType(TileID.Grass);
            for (int y = surface + 1; y < Main.maxTilesY; y++)
            {
                ref Tile tile = ref Main.tile[x, y];
                tile.ResetToType(y < dirt ? TileID.Dirt : TileID.Stone);
                if (y > surface + 2)
                    tile.WallType = y < dirt ? WallID.DirtUnsafe : WallID.Stone;
            }
        }
    }

    static void Caves(GenerationProgress progress, GameConfiguration configuration)
    {
        for (int i = 0; i < Main.maxTilesX / 12; i++)
        {
            int x = WorldGen.genRand.Next(30, Main.maxTilesX - 30);
            int y = WorldGen.genRand.Next((int)Main.worldSurface + 25, Main.maxTilesY - 20);
            WorldGen.TileRunner(x, y, WorldGen.genRand.Next(6, 12), WorldGen.genRand.Next(20, 60), -1);
        }
    }

    static void Ore(GenerationProgress progress, GameConfiguration configuration)
    {
        int ore = ModContent.TileType<ExampleOre>();
        for (int i = 0; i < Main.maxTilesX / 8; i++)
        {
            int x = WorldGen.genRand.Next(0, Main.maxTilesX);
            int y = WorldGen.genRand.Next((int)Main.rockLayer, Main.maxTilesY);
            WorldGen.TileRunner(x, y, WorldGen.genRand.Next(3, 6), WorldGen.genRand.Next(2, 6), ore);
        }
    }

    static void Spawn(GenerationProgress progress, GameConfiguration configuration)
    {
        Main.spawnTileX = Main.maxTilesX / 2;
        Main.spawnTileY = SurfaceAt(Main.spawnTileX);
    }

    public override void Update()
    {
        Main.dayTime = false;
        Main.time = 16200.0;
    }

    public override void SaveCarriedData(TagCompound tag)
    {
        tag["downedMinionBoss"] = DownedBossSystem.downedMinionBoss;
    }

    public override void LoadCarriedData(TagCompound tag)
    {
        DownedBossSystem.downedMinionBoss = tag.GetBool("downedMinionBoss");
    }

    public override void PostWorldReady()
    {
        Main.NewText(WelcomeText.Value, 175, 120, 255);
    }

    public override void DrawTransition(SpriteBatch spriteBatch, float progress)
    {
        float eased = progress * progress * (3f - 2f * progress);
        base.DrawTransition(spriteBatch, eased);
        int lid = (int)(Main.screenHeight / 2f * eased * eased);
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, lid), Color.Black);
        spriteBatch.Draw(pixel, new Rectangle(0, Main.screenHeight - lid, Main.screenWidth, lid + 1), Color.Black);
    }
}
