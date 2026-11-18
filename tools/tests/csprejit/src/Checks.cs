using System;
using System.Runtime;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace CsPreJit;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    public static bool Drawing;

    static ILog Log => ModContent.GetInstance<CsPreJitMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = Drawing = false;
    }

    public static void Frame()
    {
        if (done || ++frames != 60) return;
        done = true;
        try
        {
            Compiles();
            Textures();
        }
        catch (Exception e) { Report("run", false, e.ToString()); }
        Drawing = true;
        Log.Info($"csprejit END pass={pass} fail={fail}");
    }

    static void Compiles()
    {
        JitInfo.GetCompiledMethodCount();
        long a = JitInfo.GetCompiledMethodCount();
        int compiled = Targets.Compiled();
        long b = JitInfo.GetCompiledMethodCount();
        int big = Targets.Big;
        long c = JitInfo.GetCompiledMethodCount();
        int box = new Box(2).Value;
        long d = JitInfo.GetCompiledMethodCount();
        float loader = Targets.CallsLoader(0.25f);
        long e = JitInfo.GetCompiledMethodCount();
        int skipped = Targets.Skipped();
        long f = JitInfo.GetCompiledMethodCount();
        int weak = Targets.Weak();
        long g = JitInfo.GetCompiledMethodCount();
        int run = SkippedClass.Run();
        long h = JitInfo.GetCompiledMethodCount();
        Report("a method of the mod is compiled before its first call", b == a, $"compiles={b - a} value={compiled}");
        Report("a property getter is compiled before its first call", c == b, $"compiles={c - b} value={big}");
        Report("a constructor is compiled before its first call", d == c, $"compiles={d - c} value={box}");
        Report("the game methods a mod method calls are compiled too", e == d, $"compiles={e - d} value={loader}");
        Report("[NoJIT] leaves a method for its first call", f > e, $"compiles={f - e} value={skipped}");
        Report("[JITWhenModsEnabled] with a missing mod leaves a method for its first call", g > f, $"compiles={g - f} value={weak}");
        Report("[NoJIT] on a class covers its methods", h > g, $"compiles={h - g} value={run}");
    }

    static void Textures()
    {
        Mod mod = ModContent.GetInstance<CsPreJitMod>();
        Report("an AsyncLoad texture arrives without asking for its value", Images.Async.IsLoaded, Images.Async.Name);
        bool waited = !Images.Lazy.IsLoaded;
        Texture2D lazy = Images.Lazy.Value;
        Report("a DoNotLoad texture waits for its value", waited && Images.Lazy.IsLoaded && lazy.Width == 19 && lazy.Height == 11,
            $"waited={waited} loaded={Images.Lazy.IsLoaded} size={lazy.Width}x{lazy.Height}");
        Asset<Texture2D> now = mod.Assets.Request<Texture2D>("Images/Palette", AssetRequestMode.ImmediateLoad);
        Report("ImmediateLoad on the game thread loads at once", now.IsLoaded, now.Name);
        int right = 0;
        string sizes = "";
        foreach ((string name, int width, int height) in Images.All)
        {
            Texture2D texture = mod.Assets.Request<Texture2D>("Images/" + name, AssetRequestMode.ImmediateLoad).Value;
            if (texture.Width == width && texture.Height == height) right++;
            else sizes += $" {name}={texture.Width}x{texture.Height}";
        }
        Report("every PNG format comes out with its size", right == Images.All.Length, $"right={right}/{Images.All.Length}{sizes}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csprejit {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
