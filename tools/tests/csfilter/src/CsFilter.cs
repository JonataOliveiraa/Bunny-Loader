using System;
using log4net;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace CsFilter;

public sealed class FilterScene : ModSceneEffect
{
    internal static bool Active;
    internal static int Calls, ActiveCalls;

    public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

    public override bool IsSceneEffectActive(Player player) => Active;

    public override void SpecialVisuals(Player player, bool isActive)
    {
        Calls++;
        if (isActive) ActiveCalls++;
        player.ManageSpecialBiomeVisuals("testInvert", isActive);
    }
}

public sealed class CsFilterMod : Mod
{
    const int On = 120, Check = 300, Off = 720, End = 900;

    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsFilterMod>().Logger;

    public override void Load() => On_Main.DoUpdate += Update;

    static void Update(On_Main.orig_DoUpdate orig, Main self, Microsoft.Xna.Framework.GameTime gameTime)
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
        if (done) Log.Info($"csfilter END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        Filter filter = Filters.Scene["testInvert"];
        if (f == On)
        {
            FilterScene.Active = true;
            Log.Info($"csfilter scene on filter={(filter.IsNull ? "null" : "ok")}");
        }
        if (f == Check)
        {
            Report("SpecialVisuals runs with isActive while the scene effect is on", FilterScene.ActiveCalls > 0, $"calls={FilterScene.Calls} active={FilterScene.ActiveCalls}");
            Report("the game's testInvert filter is on", !filter.IsNull && filter.IsActive(), $"null={filter.IsNull} opacity={(filter.IsNull ? 0 : filter.Opacity)}");
        }
        if (f == Off)
        {
            FilterScene.Active = false;
            Log.Info("csfilter scene off");
        }
        if (f == End)
        {
            Report("the filter turns off with the scene effect", !filter.IsNull && !filter.IsActive(), $"opacity={(filter.IsNull ? 0 : filter.Opacity)}");
            FilterScene.Active = false;
            done = true;
        }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csfilter {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
