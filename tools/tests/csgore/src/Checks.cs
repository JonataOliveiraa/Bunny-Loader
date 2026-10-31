using System;
using CsGore.Content;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsGore;

public static class Checks
{
    static int pass, fail, frames, test = -1, auto = -1;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsGoreMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        test = auto = -1;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 60) Start(p);
            if (frames == 180) Check();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csgore END pass={pass} fail={fail}");
    }

    static void Start(Player p)
    {
        int testType = ModContent.GoreType<TestGore>();
        bool found = ModContent.TryFind("CsGore/AutoGore", out ModGore autoGore);
        int autoType = found ? autoGore.Type : 0;
        Report("ModGore and the Gores/ texture get types after the game's", testType >= GoreID.Count && autoType >= GoreID.Count && testType != autoType &&
            GoreLoader.GoreCount > Math.Max(testType, autoType) && TestGore.StaticCalls == 1 && autoGore is SimpleModGore,
            $"test={testType} auto={autoType} count={GoreLoader.GoreCount} static={TestGore.StaticCalls}");
        Report("their textures are in TextureAssets.Gore", TextureAssets.Gore[testType].Value.Width > 0 && TextureAssets.Gore[autoType].Value.Width > 0,
            $"test={TextureAssets.Gore[testType].Value.Width} auto={TextureAssets.Gore[autoType].Value.Width}");

        test = Gore.NewGore(p.GetSource_FromThis(), p.Center + new Vector2(-30f, -60f), Vector2.Zero, testType);
        auto = Gore.NewGore(p.GetSource_FromThis(), p.Center + new Vector2(30f, -60f), Vector2.Zero, autoType);
        Gore.NewGore(p.Center + new Vector2(0f, -90f), Vector2.Zero, testType);
        Report("OnSpawn gets the spawn source, or none without one", TestGore.Spawned == 2 && TestGore.SawSource && TestGore.SawEmpty &&
            (uint)test < (uint)Main.gore.Length, $"spawned={TestGore.Spawned} source={TestGore.SawSource} empty={TestGore.SawEmpty} index={test}");
    }

    static void Check()
    {
        Report("Update and GetAlpha run while the gore lives", TestGore.Updates > 10 && TestGore.Alphas > 0 && Main.gore[test].active,
            $"updates={TestGore.Updates} alphas={TestGore.Alphas} active={Main.gore[test].active}");
        Report("the autoloaded gore lives as a game gore", (uint)auto < (uint)Main.gore.Length && Main.gore[auto].type == ModContent.Find<ModGore>("CsGore/AutoGore").Type,
            $"index={auto} type={(auto >= 0 ? Main.gore[auto].type : -1)}");
        done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csgore {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
