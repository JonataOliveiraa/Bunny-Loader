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

// This subworld shows off the rest of what a ModSubworld can do: several generation passes, a world that is kept between trips,
// data carried over from the main world, a fixed time of day and a custom transition instead of the loading screen.
// Use ExampleAdvancedSubworldKey to visit it. See ExampleSubworld for the basics.
public class ExampleAdvancedSubworld : ModSubworld
{
    // Shown in the chat when the player arrives. See PostWorldReady.
    public static LocalizedText WelcomeText { get; private set; }

    public override int Width => 1400;

    public override int Height => 700;

    // Persistent subworlds are saved when the player leaves and loaded again on the next trip, so anything built or broken there stays.
    public override bool Persistent => true;

    // Lets grass spread and plants and trees grow, like in a normal world. Liquids, wiring and tile entities update either way.
    public override bool RandomTileUpdates => true;

    // Instead of the loading screen, the screen stays dark while the world changes, with DrawTransition drawn on top.
    public override bool ShowLoadingScreen => false;

    // How long the transition takes in the world being left, and in the world being entered.
    public override float TransitionOutSeconds => 1.5f;

    public override float TransitionInSeconds => 1.2f;

    // The passes run in this order. Each one is a method below.
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

    // The height of the surface at a given column. Two sine waves of different sizes make rolling hills with smaller bumps on them.
    static int SurfaceAt(int x) => (int)(Main.worldSurface + Math.Sin(x / 47.0) * 9 + Math.Sin(x / 13.0) * 3);

    static void Hills(GenerationProgress progress, GameConfiguration configuration)
    {
        for (int x = 0; x < Main.maxTilesX; x++)
        {
            int surface = SurfaceAt(x);
            // The dirt layer is 10 to 13 tiles deep, with stone below it.
            int dirt = surface + WorldGen.genRand.Next(10, 14);
            Main.tile[x, surface].ResetToType(TileID.Grass);
            for (int y = surface + 1; y < Main.maxTilesY; y++)
            {
                // Main.tile returns a reference, so changes to the tile are kept.
                ref Tile tile = ref Main.tile[x, y];
                tile.ResetToType(y < dirt ? TileID.Dirt : TileID.Stone);
                // Walls start a little below the surface, so the hills don't look like they have walls behind them.
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
            // A type of -1 makes TileRunner remove tiles instead of placing them, which digs a cave.
            WorldGen.TileRunner(x, y, WorldGen.genRand.Next(6, 12), WorldGen.genRand.Next(20, 60), -1);
        }
    }

    static void Ore(GenerationProgress progress, GameConfiguration configuration)
    {
        // Splotches of ExampleOre below the rock layer, the same way ExampleOrePass places them in the main world.
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
        // The spawn point defaults to the middle of the surface, which would be inside a hill here, so it is moved on top of it.
        Main.spawnTileX = Main.maxTilesX / 2;
        Main.spawnTileY = SurfaceAt(Main.spawnTileX);
    }

    // Update runs once per world update while this subworld is active. Here it keeps the time frozen at midnight.
    public override void Update()
    {
        Main.dayTime = false;
        Main.time = 16200.0;
    }

    // SaveCarriedData runs in the world being left, and LoadCarriedData in the world being entered, with the same tag.
    // Each world saves its own ModSystem data, so this is how data is shared between them. Here, defeating the Minion Boss in either world counts in both.
    public override void SaveCarriedData(TagCompound tag)
    {
        tag["downedMinionBoss"] = DownedBossSystem.downedMinionBoss;
    }

    public override void LoadCarriedData(TagCompound tag)
    {
        DownedBossSystem.downedMinionBoss = tag.GetBool("downedMinionBoss");
    }

    // PostWorldReady runs once the player is in the world, after LoadCarriedData.
    public override void PostWorldReady()
    {
        Main.NewText(WelcomeText.Value, 175, 120, 255);
    }

    // DrawTransition draws over the whole screen during the trip. progress goes from 0 to 1 while leaving, stays at 1 while the world changes,
    // and goes back to 0 while arriving. The default draws black with progress as its opacity.
    public override void DrawTransition(SpriteBatch spriteBatch, float progress)
    {
        // Smoothstep easing, so the fade starts and ends slowly. Calling base keeps the black fade.
        float eased = progress * progress * (3f - 2f * progress);
        base.DrawTransition(spriteBatch, eased);

        // Two black bars close in from the top and the bottom of the screen, like eyelids.
        int lid = (int)(Main.screenHeight / 2f * eased * eased);
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, lid), Color.Black);
        spriteBatch.Draw(pixel, new Rectangle(0, Main.screenHeight - lid, Main.screenWidth, lid + 1), Color.Black);
    }
}
