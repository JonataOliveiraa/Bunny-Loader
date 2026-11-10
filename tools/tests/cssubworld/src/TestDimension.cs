using System.Collections.Generic;
using System.Threading;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace CsSubworld;

public sealed class TestDimension : ModSubworld
{
    public const int MarkerX = 100;

    public static int Generated, Entered, Exited, Ready, Score;

    public static int FloorY => (int)Main.worldSurface + 1;

    public override int Width => 1000;

    public override int Height => 500;

    public override bool Persistent => true;

    public override List<GenPass> GenerationTasks => new()
    {
        new PassLegacy("Floor", (progress, configuration) =>
        {
            Generated++;
            for (int x = 0; x < Main.maxTilesX; x++)
                for (int y = FloorY; y < FloorY + 20; y++)
                    WorldGen.PlaceTile(x, y, TileID.Stone, true, true);
            WorldGen.PlaceTile(MarkerX, FloorY - 1, TileID.GrayBrick, true, true);
        }, 1),
        new PassLegacy("Slow first time", (progress, configuration) =>
        {
            if (Generated != 1) return;
            CsSubworldMod.Log.Info("cssubworld SHOT loading");
            Thread.Sleep(5000);
        }, 1),
    };

    public override void OnEnter()
    {
        Entered++;
        Score = 0;
    }

    public override void OnExit()
    {
        Exited++;
        Score = 0;
    }

    public override void PostWorldReady() => Ready++;

    public override void SaveCarriedData(TagCompound tag) => tag["Score"] = Score;

    public override void LoadCarriedData(TagCompound tag) => Score = tag.GetInt("Score");
}
