using System;
using log4net;
using Terraria;
using Terraria.ModLoader;

namespace CsResource;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsResourceMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 180) return;
        done = true;
        try { Run(p); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        Log.Info($"csresource END pass={pass} fail={fail}");
    }

    static void Run(Player p)
    {
        Report("PreDrawResource and PostDrawResource run for each heart and star", TestOverlay.Pre >= 100 && TestOverlay.Post == TestOverlay.Pre,
            $"pre={TestOverlay.Pre} post={TestOverlay.Post}");
        Report("resourceNumber counts the hearts", TestOverlay.MaxNumber >= 4, $"max={TestOverlay.MaxNumber} hearts={TestOverlay.Hearts}");
        Report("the context's texture is the game's asset", TestOverlay.NamedTextures > 0, $"named={TestOverlay.NamedTextures} textures={string.Join(",", TestOverlay.Textures)}");
        Report("the display hooks run for life and mana", TestOverlay.LifeDisplays >= 50 && TestOverlay.ManaDisplays >= 50 && TestOverlay.PostDisplays == TestOverlay.LifeDisplays + TestOverlay.ManaDisplays,
            $"life={TestOverlay.LifeDisplays} mana={TestOverlay.ManaDisplays} post={TestOverlay.PostDisplays}");
        Report("a texture compares equal to Main.Assets.Request of the same image", TestOverlay.Matched > 0, $"matched={TestOverlay.Matched}");
        Report("the snapshot has the player's life", TestOverlay.LifeMax == p.statLifeMax2 && TestOverlay.Hearts > 0, $"lifeMax={TestOverlay.LifeMax}/{p.statLifeMax2} hearts={TestOverlay.Hearts}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csresource {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
