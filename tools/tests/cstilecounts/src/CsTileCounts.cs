using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsTileCounts;

public sealed class CsTileCountsMod : Mod
{
    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (!Main.gameMenu && i == Main.myPlayer)
            Checks.Frame(self);
    }
}

public sealed class CountedTile : ModTile
{
    public override string Texture => "CsTileCounts/CountedTile";

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        AddMapEntry(new Color(200, 90, 40));
    }
}

public sealed class CountSystem : ModSystem
{
    public static int Calls, Resets, LastLength, LastCount = -1;

    public override void ResetNearbyTileEffects() => Resets++;

    public override void TileCountsAvailable(ReadOnlySpan<int> tileCounts)
    {
        Calls++;
        LastLength = tileCounts.Length;
        LastCount = tileCounts[ModContent.TileType<CountedTile>()];
    }
}

public static class Checks
{
    const int Width = 6, Height = 4;

    static int frames, pass, fail, x, y, placed, callsAtPlace, callsAtClear;
    static bool done;

    static void Log(string text) => ModContent.GetInstance<CsTileCountsMod>().Logger.Info(text);

    static void Check(string name, bool ok, string detail = "")
    {
        if (ok)
            pass++;
        else
            fail++;
        Log($"cstilecounts {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    public static void Frame(Player player)
    {
        if (done)
            return;
        try
        {
            switch (++frames)
            {
                case 60:
                    Lengths("at world entry");
                    Place(player);
                    break;
                case 240:
                    Counted();
                    Clear();
                    break;
                case 420:
                    Cleared();
                    End();
                    break;
            }
        }
        catch (Exception e)
        {
            Check("run", false, e.ToString());
            End();
        }
    }

    static void End()
    {
        done = true;
        Log($"cstilecounts END pass={pass} fail={fail}");
    }

    static int Length(SceneMetrics metrics) => metrics.IsNull || metrics._tileCounts.IsNull ? -1 : metrics._tileCounts.Length;

    static void Lengths(string when)
    {
        int size = TileLoader.TileCount;
        int player = Length(Main.PlayerSceneMetrics), camera = Length(Main._cameraSceneMetrics), pylon = Length(Main.PylonSystem._sceneMetrics);
        Check($"mod tiles exist {when}", size > TileID.Count && ModContent.TileType<CountedTile>() >= TileID.Count, $"TileCount={size} type={ModContent.TileType<CountedTile>()}");
        Check($"player scene metrics fit every tile {when}", player >= size, $"length={player} TileCount={size}");
        Check($"camera scene metrics fit every tile {when}", camera >= size, $"length={camera} TileCount={size}");
        Check($"pylon scene metrics fit every tile {when}", pylon >= size, $"length={pylon} TileCount={size}");
        int fresh = Length(SceneMetrics.New());
        Check($"a new SceneMetrics fits every tile {when}", fresh >= size, $"length={fresh} TileCount={size}");
    }

    static void Place(Player player)
    {
        int type = ModContent.TileType<CountedTile>();
        x = (int)player.Center.X / 16 + 4;
        y = (int)player.Center.Y / 16 - 10;
        for (int i = x; i < x + Width; i++)
            for (int j = y; j < y + Height; j++)
            {
                WorldGen.KillTile(i, j, false, false, true);
                if (WorldGen.PlaceTile(i, j, type, true, true) && Main.tile[i, j].active() && Main.tile[i, j].type == type)
                    placed++;
            }
        callsAtPlace = CountSystem.Calls;
        Check("mod tiles placed next to the player", placed == Width * Height, $"placed={placed} at {x},{y}");
    }

    static void Counted()
    {
        int type = ModContent.TileType<CountedTile>();
        Check("TileCountsAvailable ran after placing", CountSystem.Calls > callsAtPlace && CountSystem.Resets > 0, $"calls={CountSystem.Calls - callsAtPlace} resets={CountSystem.Resets}");
        Check("tileCounts has TileLoader.TileCount entries", CountSystem.LastLength == TileLoader.TileCount, $"length={CountSystem.LastLength} TileCount={TileLoader.TileCount}");
        Check("tileCounts[ModContent.TileType<T>()] counts the placed tiles", CountSystem.LastCount == placed, $"count={CountSystem.LastCount} placed={placed}");
        int scene = Main.SceneMetrics.GetTileCount((ushort)type);
        Check("SceneMetrics.GetTileCount sees the mod tile", scene == placed, $"count={scene} placed={placed}");
        Scanned("the pylon scan counts mod tiles", Main.PylonSystem._sceneMetrics, type);
        Scanned("a new SceneMetrics counts mod tiles", SceneMetrics.New(), type);
        Lengths("after scanning");
    }

    static void Scanned(string name, SceneMetrics metrics, int type)
    {
        int length = Length(metrics), count = -1;
        string error = "";
        if (length < TileLoader.TileCount)
        {
            Check(name, false, $"skipped: the scan would write past _tileCounts (length {length})");
            return;
        }
        try
        {
            metrics.Scan(new SceneMetricsScanSettings { BiomeScanCenterPositionInWorld = new Vector2((x + Width / 2) * 16, (y + Height / 2) * 16) });
            count = metrics.GetTileCount((ushort)type);
        }
        catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
        Check(name, count == placed, $"count={count} placed={placed} {error}");
    }

    static void Clear()
    {
        for (int i = x; i < x + Width; i++)
            for (int j = y; j < y + Height; j++)
                WorldGen.KillTile(i, j, false, false, true);
        callsAtClear = CountSystem.Calls;
    }

    static void Cleared()
    {
        Check("the count drops to zero after removing the tiles", CountSystem.Calls > callsAtClear && CountSystem.LastCount == 0, $"calls={CountSystem.Calls - callsAtClear} count={CountSystem.LastCount}");
    }
}
