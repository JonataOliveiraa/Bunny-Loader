using System;
using BunnyLoader;
using Terraria;
using Terraria.ModLoader;

namespace CsPerf;

public sealed class CsPerfMod : Mod
{
    const string Isolated = "Main.DoUpdate_AnimateCursorColors/0";
    const string After = "Main.DoUpdate_AnimateVisualPlayerAura/0";
    const string Burst = "WorldItem.UpdateItem/1";

    static int frames;
    static int stacked;

    static readonly On_Main.hook_DoUpdate_AnimateCursorColors[] cursor =
    {
        orig => orig(), orig => orig(), orig => orig(),
    };
    static readonly On_Main.hook_DoUpdate_AnimateVisualPlayerAura[] aura =
    {
        orig => orig(), orig => orig(), orig => orig(),
    };
    static readonly On_WorldItem.hook_UpdateItem[] items =
    {
        (orig, self, i) => orig(self, i), (orig, self, i) => orig(self, i), (orig, self, i) => orig(self, i),
    };

    public override void Load()
    {
        Measure.Install(Logger, "csperf");
        On_Player.Update += Update;
    }

    static void Stack(int total)
    {
        while (stacked < total)
        {
            On_Main.DoUpdate_AnimateCursorColors += cursor[stacked];
            On_Main.DoUpdate_AnimateVisualPlayerAura += aura[stacked];
            On_WorldItem.UpdateItem += items[stacked];
            stacked++;
        }
        while (stacked > total)
        {
            stacked--;
            On_Main.DoUpdate_AnimateCursorColors -= cursor[stacked];
            On_Main.DoUpdate_AnimateVisualPlayerAura -= aura[stacked];
            On_WorldItem.UpdateItem -= items[stacked];
        }
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer) return;
        self.creativeGodMode = true;
        try { Step(++frames); }
        catch (Exception e) { Measure.Info($"error {e}"); }
    }

    static void Step(int f)
    {
        switch (f)
        {
            case 60:
                Measure.Info($"profiler {(HookProfile.Enabled ? "on" : "OFF")}, bias {HookProfile.BiasNs:0} ns, clock {HookProfile.ClockNs:0} ns, QuickJS window first");
                break;
            case 120:
                Measure.Begin("no C# hook on the targets");
                break;
            case 420:
                Measure.End(Isolated, After, Burst);
                break;
            case 480:
                Stack(1);
                break;
            case 540:
                Measure.Begin("1 pass-through hook per target");
                break;
            case 840:
                Measure.End(Isolated, After, Burst);
                Stack(3);
                break;
            case 900:
                Measure.Begin("3 pass-through hooks per target");
                break;
            case 1200:
                Measure.End(Isolated, After, Burst);
                Stack(0);
                break;
            case 1260:
                Measure.Begin("hooks removed (native stub stays)");
                break;
            case 1560:
                Measure.End(Isolated, After, Burst);
                Measure.Info("END");
                break;
        }
    }
}
