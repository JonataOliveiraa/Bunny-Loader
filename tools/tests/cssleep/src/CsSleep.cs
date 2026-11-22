using System;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SleepTest;

public sealed class CsSleepMod : Mod
{
    static int frames, pass, fail, bedX, bedY;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsSleepMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = pass = fail = 0;
            done = false;
            return;
        }
        if (i != Main.myPlayer || done) return;
        frames++;
        try { Step(self); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            Finish();
        }
    }

    static void Step(Player p)
    {
        switch (frames)
        {
            case 120:
                Main.dayTime = false;
                Main.time = 10000;
                bedX = (int)(p.Center.X / 16) + 3;
                bedY = (int)((p.position.Y + p.height) / 16) - 1;
                WorldGen.PlaceTile(bedX, bedY, TileID.Beds, true, true, -1, 0);
                Report("a bed is placed next to the player", Main.tile[bedX, bedY].HasTile && Main.tile[bedX, bedY].TileType == TileID.Beds,
                    $"x={bedX} y={bedY} type={Main.tile[bedX, bedY].TileType}");
                break;
            case 150:
                p.sleeping.StartSleeping(p, bedX, bedY);
                Log.Info($"cssleep started: sleeping={p.sleeping.isSleeping}");
                break;
            case 420:
                Report("the player is asleep", p.sleeping.isSleeping && p.sleeping.FullyFallenAsleep,
                    $"sleeping={p.sleeping.isSleeping} full={p.sleeping.FullyFallenAsleep} ghost={p.ghost} dead={p.dead}");
                Log.Info("cssleep SHOT asleep");
                break;
            case 600:
                p.sleeping.StopSleeping(p);
                break;
            case 660:
                Report("the player wakes up", !p.sleeping.isSleeping, $"sleeping={p.sleeping.isSleeping}");
                Log.Info("cssleep SHOT awake");
                break;
            case 720:
                Finish();
                break;
        }
    }

    static void Finish()
    {
        done = true;
        Log.Info($"cssleep END pass={pass} fail={fail}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cssleep {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
