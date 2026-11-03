using System;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CsEvents;

public sealed class CsEventsMod : Mod
{
    static int frames, pass, fail, first, second, entered, thrown, afterThrow;
    static bool done, gameTimeOk = true;
    static Player enteredPlayer;

    static ILog Log => ModContent.GetInstance<CsEventsMod>().Logger;

    static void First(GameTime time)
    {
        first++;
        if (time.IsNull) gameTimeOk = false;
    }

    static void Second(GameTime time) => second++;

    static void Throwing(GameTime time)
    {
        thrown++;
        throw new InvalidOperationException("csevents handler failure");
    }

    static void Entered(Player player)
    {
        entered++;
        enteredPlayer = player;
    }

    public override void Load()
    {
        Player.Hooks.OnEnterWorld += Entered;
        On_Main.DoUpdate += Update;
    }

    static void Update(On_Main.orig_DoUpdate orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (done) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csevents END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        switch (f)
        {
            case 10:
                Report("Player.Hooks.OnEnterWorld ran when the player entered the world", entered >= 1 && !enteredPlayer.IsNull && enteredPlayer.whoAmI == Main.myPlayer, $"calls={entered}");
                Main.OnPostDraw += First;
                Main.OnPostDraw += Second;
                break;
            case 70:
                Report("both OnPostDraw subscribers run every frame with the GameTime", first >= 15 && second >= 15 && Math.Abs(first - second) <= 1 && gameTimeOk, $"first={first} second={second} gameTime={gameTimeOk}");
                Main.OnPostDraw -= First;
                first = 0;
                second = 0;
                break;
            case 130:
                Report("removing one subscriber leaves the other", first == 0 && second >= 30, $"first={first} second={second}");
                Main.OnPostDraw += Throwing;
                break;
            case 160:
                afterThrow = second;
                break;
            case 190:
                Report("a handler that throws is reported and the others keep running", thrown >= 10 && second > afterThrow, $"thrown={thrown} second={second - afterThrow}");
                Main.OnPostDraw -= Throwing;
                Main.OnPostDraw -= Second;
                first = second = 0;
                break;
            case 230:
                Report("removing every subscriber stops the calls", first == 0 && second == 0, $"first={first} second={second}");
                break;
            case 240:
                Parameters();
                done = true;
                break;
        }
    }

    static void Parameters()
    {
        int calls = 0, lastX = -1;
        bool all = Utils.PlotLine(2, 5, 12, 5, (x, y) =>
        {
            calls++;
            lastX = x;
            return y == 5;
        });
        Report("a C# lambda passed as a game delegate is called for each point the game plots", all && calls == 10 && lastX == 11, $"calls={calls} last={lastX} result={all}");
        int stopped = 0;
        bool early = Utils.PlotLine(0, 0, 20, 0, (x, y) => ++stopped < 4);
        Report("returning false from the C# delegate stops the game's loop", !early && stopped == 4, $"calls={stopped} result={early}");
        int total = 0;
        for (int i = 0; i < 300; i++) Utils.PlotLine(0, 0, 1, 0, (x, y) => { total++; return true; });
        Report("300 short-lived game delegates in a row", total == 300, $"calls={total}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csevents {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
