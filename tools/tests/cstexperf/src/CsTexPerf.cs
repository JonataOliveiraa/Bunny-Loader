using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsTexPerf;

public sealed class CsTexPerfMod : Mod
{
    const int End = 3000;
    static int frame;
    static int spawnX, spawnFloor;
    static long last, windowStart;
    static int frames;
    static double worst, sum, drawSum;
    static string phase = "load";

    static ILog Log => ModContent.GetInstance<CsTexPerfMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.DoDraw += DoDraw;
    }

    static void DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        long start = Stopwatch.GetTimestamp();
        if (!Main.gameMenu && frame >= 2450 && frame < 2950)
        {
            Player me = Main.player[Main.myPlayer];
            me.statLife = me.statLifeMax2 * 47 / 100;
        }
        orig(self, gameTime);
        long end = Stopwatch.GetTimestamp();
        if (Main.gameMenu) return;
        if (last != 0)
        {
            double ms = (start - last) * 1000.0 / Stopwatch.Frequency;
            sum += ms;
            worst = Math.Max(worst, ms);
            drawSum += (end - start) * 1000.0 / Stopwatch.Frequency;
            frames++;
        }
        else windowStart = start;
        last = start;
        if (start - windowStart >= Stopwatch.Frequency * 2 && frames > 0)
        {
            Log.Info($"cstexperf {phase} fps={frames / ((start - windowStart) / (double)Stopwatch.Frequency):0.0} frame={sum / frames:0.0}ms worst={worst:0.0}ms draw={drawSum / frames:0.0}ms rss={Rss()}MB");
            frames = 0;
            sum = worst = drawSum = 0;
            windowStart = start;
        }
    }

    static string Rss()
    {
        try
        {
            string? line = File.ReadLines("/proc/self/status").FirstOrDefault(l => l.StartsWith("VmRSS:"));
            return line == null ? "?" : (long.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]) / 1024).ToString();
        }
        catch (Exception) { return "?"; }
    }

    static int Floor(int x)
    {
        for (int y = 50; y < Main.maxTilesY - 10; y++)
            if (Main.tile[x, y].HasTile) return y;
        return (int)(Main.worldSurface);
    }

    static void Fill(int center, ushort type)
    {
        int floor = Floor(center);
        for (int x = center - 70; x <= center + 70; x++)
        {
            for (int y = floor; y < floor + 25; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile) tile.TileType = type;
                else WorldGen.PlaceTile(x, y, type, true, true);
            }
        }
        WorldGen.RangeFrame(center - 72, floor - 2, center + 72, floor + 27);
    }

    static void Go(Player p, int x, string name)
    {
        int floor = Floor(x);
        p.position = new Vector2(x * 16, (floor - 4) * 16);
        p.velocity = Vector2.Zero;
        phase = name;
        Log.Info($"cstexperf go {name} x={x}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
        frame++;
        try { Step(self); }
        catch (Exception e) { Log.Info($"cstexperf frame {frame}: FAIL {e}"); }
        if (frame == End) Log.Info("cstexperf END");
    }

    static string Zones(Player p) => $"desert={p.ZoneDesert} snow={p.ZoneSnow} jungle={p.ZoneJungle} bgStyle={Main.bgStyle}";

    static void Step(Player p)
    {
        switch (frame)
        {
            case 30:
                Main.dayTime = true;
                Main.time = 20000;
                spawnX = (int)(p.Center.X / 16);
                spawnFloor = Floor(spawnX);
                phase = "forest";
                Log.Info($"cstexperf start x={spawnX} floor={spawnFloor} {Zones(p)}");
                break;
            case 400:
                Fill(spawnX + 300, TileID.Sand);
                Fill(spawnX - 300, TileID.SnowBlock);
                Fill(spawnX + 600, TileID.JungleGrass);
                Go(p, spawnX + 300, "desert");
                break;
            case 520:
                Log.Info($"cstexperf SHOT desert {Zones(p)}");
                break;
            case 800:
                Go(p, spawnX - 300, "snow");
                break;
            case 920:
                Log.Info($"cstexperf SHOT snow {Zones(p)}");
                break;
            case 1200:
                Go(p, spawnX + 600, "jungle");
                break;
            case 1320:
                Log.Info($"cstexperf SHOT jungle {Zones(p)}");
                break;
            case 1600:
                Go(p, spawnX, "forest2");
                break;
            case 1720:
                Log.Info($"cstexperf SHOT forest {Zones(p)}");
                break;
            case 1800:
                phase = "lowres";
                TextureAtlasDB.UITexturesHigh = false;
                TextureAtlasDB.BackgroundTexturesHigh = false;
                TextureAtlasDB.EnvironmentTexturesHigh = false;
                Log.Info($"cstexperf lowres ui={TextureAtlasDB.UITexturesHigh} bg={TextureAtlasDB.BackgroundTexturesHigh} env={TextureAtlasDB.EnvironmentTexturesHigh}");
                break;
            case 1920:
                Log.Info("cstexperf SHOT lowres");
                break;
            case 2100:
                Go(p, spawnX + 300, "lowres-desert");
                break;
            case 2220:
                Log.Info($"cstexperf SHOT lowresdesert {Zones(p)}");
                break;
            case 2400:
                TextureAtlasDB.UITexturesHigh = true;
                TextureAtlasDB.BackgroundTexturesHigh = true;
                TextureAtlasDB.EnvironmentTexturesHigh = true;
                Go(p, spawnX, "highres");
                break;
            case 2600:
            {
                int cx = spawnX - 150;
                int top = Floor(cx) - 2;
                int bottom = (int)Main.worldSurface + 60;
                for (int x = cx - 40; x <= cx + 40; x++)
                    for (int y = top; y <= bottom; y++)
                    {
                        if (Main.tile[x, y].HasTile) WorldGen.KillTile(x, y, false, false, true);
                        Tile tile = Main.tile[x, y];
                        tile.WallType = 0;
                    }
                for (int x = cx - 40; x <= cx + 40; x++) WorldGen.PlaceTile(x, bottom + 1, TileID.Stone, true, true);
                WorldGen.RangeFrame(cx - 42, top - 2, cx + 42, bottom + 3);
                p.position = new Vector2(cx * 16, (bottom - 3) * 16);
                p.velocity = Vector2.Zero;
                phase = "underground";
                Log.Info($"cstexperf go underground x={cx} y={bottom} surface={Main.worldSurface:0}");
                break;
            }
            case 2720:
                Log.Info("cstexperf SHOT underground");
                break;
            case 2480:
                Log.Info($"cstexperf SHOT hearts life={p.statLife}/{p.statLifeMax2}");
                break;
        }
    }
}
