using System.Diagnostics;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CsFrameSkip;

public sealed class CsFrameSkipMod : Mod
{
    public static ILog Log => ModContent.GetInstance<CsFrameSkipMod>().Logger;
}

public sealed class CsFrameSkipSystem : ModSystem
{
    const int End = 22;

    static readonly Stopwatch clock = new();
    static int updates, draws, second = -1;
    static int phaseUpdates, phaseSeconds;
    static string phase = "";
    static bool done;

    static string Phase(int s) => s switch
    {
        < 3 => "baseline",
        < 10 => "slow",
        < 13 => "recover",
        < 20 => "hitch",
        _ => "tail",
    };

    static double Extra(int s, int draw) => Phase(s) switch
    {
        "slow" => 22,
        "hitch" when draw == 1 => 60,
        _ => 0,
    };

    public override void PostUpdateEverything()
    {
        if (Main.gameMenu || done) return;

        Main.LocalPlayer.creativeGodMode = true;
        updates++;
    }

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (Main.gameMenu || done) return;

        if (!clock.IsRunning)
        {
            clock.Start();
            second = 0;
            updates = 0;
            return;
        }

        draws++;
        int s = (int)(clock.ElapsedMilliseconds / 1000);

        double extra = Extra(s, draws);
        if (extra > 0)
        {
            long until = Stopwatch.GetTimestamp() + (long)(extra * Stopwatch.Frequency / 1000);
            while (Stopwatch.GetTimestamp() < until) { }
        }

        if (s == second) return;

        Report();
        second = s;

        if (s >= End)
        {
            done = true;
            CsFrameSkipMod.Log.Info("csframeskip END");
        }
    }

    static void Report()
    {
        string current = Phase(second);
        if (current != phase)
        {
            if (phase != "" && phaseSeconds > 1)
                CsFrameSkipMod.Log.Info($"csframeskip phase {phase}: {phaseUpdates / (double)phaseSeconds:0.0} updates/s");
            phase = current;
            phaseUpdates = 0;
            phaseSeconds = 0;
        }
        else
        {
            phaseUpdates += updates;
            phaseSeconds++;
        }

        Microsoft.Xna.Framework.Game game = Main.instance;
        XNAUnityRunner runner = XNAUnityRunner._instance;
        CsFrameSkipMod.Log.Info($"csframeskip t={second} {current} draws={draws} updates={updates} " +
                                $"variable={game.updateModeVariable} fpsCount={game.fpsCount} disableVariable={runner.DisableVariableUpdate}");
        draws = 0;
        updates = 0;
    }
}
