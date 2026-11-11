using System;
using System.Collections.Generic;
using log4net;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CsWldMake;

public sealed class ArenaMaker : ModSubworld
{
    public const int Left = 130, Right = 270, WallHeight = 30;

    public override int Width => 400;

    public override int Height => 250;

    public override bool Persistent => true;

    public override List<GenPass> GenerationTasks => new()
    {
        new PassLegacy("Arena", Build, 1),
    };

    static void Build(GenerationProgress progress, GameConfiguration configuration)
    {
        ushort block = ModContent.TryFind("ExampleMod", "ExampleBlock", out ModTile tile) ? tile.Type : TileID.GrayBrick;
        ushort wall = ModContent.TryFind("ExampleMod", "ExampleWall", out ModWall modWall) ? modWall.Type : (ushort)WallID.GrayBrick;
        int ground = (int)Main.worldSurface + 10;
        for (int x = 0; x < Main.maxTilesX; x++)
        {
            Main.tile[x, ground].ResetToType(TileID.Grass);
            for (int y = ground + 1; y < Main.maxTilesY; y++)
                Main.tile[x, y].ResetToType(y < ground + 10 ? TileID.Dirt : TileID.Stone);
        }

        for (int x = Left; x <= Right; x++)
        {
            for (int y = ground; y < ground + 3; y++)
                Main.tile[x, y].ResetToType(block);
            for (int y = ground - WallHeight; y < ground; y++)
                Main.tile[x, y].WallType = wall;
        }

        for (int y = ground - WallHeight - 2; y < ground; y++)
        {
            for (int x = Left - 2; x < Left; x++)
                Main.tile[x, y].ResetToType(block);
            for (int x = Right + 1; x <= Right + 2; x++)
                Main.tile[x, y].ResetToType(block);
        }

        for (int row = 1; row <= 2; row++)
        {
            int y = ground - row * 10;
            for (int x = Left + 12; x <= Right - 12; x++)
                if ((x - Left) % 40 < 28) WorldGen.PlaceTile(x, y, TileID.Platforms, true, true);
        }

        for (int x = Left + 6; x <= Right - 6; x += 16)
            WorldGen.PlaceTile(x, ground - 1, TileID.Torches, true, true);

        Main.spawnTileX = (Left + Right) / 2;
        Main.spawnTileY = ground;
    }
}

public sealed class CsWldMakeMod : Mod
{
    const string Test = "cswldmake";

    static int frames, phase;
    static nint seen;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsWldMakeMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done) return;
        if (Main.ActiveWorldFileData.Pointer != seen)
        {
            seen = Main.ActiveWorldFileData.Pointer;
            frames = 0;
        }

        frames++;
        try
        {
            switch (phase)
            {
                case 0 when frames == 120:
                    Log.Info($"{Test} example mod={ModContent.TryFind("ExampleMod", "ExampleBlock", out ModTile _)}");
                    SubworldLoader.Enter<ArenaMaker>();
                    phase = 1;
                    break;
                case 1 when frames == 240 && SubworldLoader.IsActive<ArenaMaker>():
                    Log.Info($"{Test} path={Main.ActiveWorldFileData.Path}");
                    Log.Info($"{Test} SHOT arena");
                    break;
                case 1 when frames == 480 && SubworldLoader.IsActive<ArenaMaker>():
                    SubworldLoader.Exit();
                    phase = 2;
                    break;
                case 2 when frames == 120 && !SubworldLoader.InSubworld:
                    done = true;
                    Log.Info($"{Test} END pass=1 fail=0");
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Info($"{Test} run: FAIL {e}");
            done = true;
            Log.Info($"{Test} END pass=0 fail=1");
        }
    }
}
