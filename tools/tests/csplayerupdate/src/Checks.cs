using System;
using System.Collections.Generic;
using log4net;
using Terraria;
using Terraria.ModLoader;

namespace CsPlayerUpdate;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsPlayerUpdateMod>().Logger;

    static int Calls(string name) => UpdatePlayer.Calls.GetValueOrDefault(name);

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 120) return;
        done = true;
        try { Run(p); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        Log.Info($"csplayerupdate END pass={pass} fail={fail}");
    }

    static void Every(string name, int at)
    {
        int calls = Calls(name);
        Report($"{name} runs every frame", calls >= at, $"calls={calls}");
    }

    static void Run(Player p)
    {
        Every(nameof(UpdatePlayer.PostUpdateMiscEffects), 100);
        Every(nameof(UpdatePlayer.PostUpdateRunSpeeds), 100);
        Every(nameof(UpdatePlayer.ResetInfoAccessories), 100);
        Every(nameof(UpdatePlayer.ProcessTriggers), 100);
        Every(nameof(UpdatePlayer.NaturalLifeRegen), 100);
        Every(nameof(UpdatePlayer.UpdateDyes), 100);
        Every(nameof(UpdatePlayer.UpdateVisibleAccessories), 100);
        Every(nameof(UpdatePlayer.UpdateVisibleVanityAccessories), 100);
        int moves = Calls(nameof(UpdatePlayer.PreUpdateMovement));
        Report("PreUpdateMovement runs once per update", moves >= 100 && moves <= frames + 1, $"calls={moves} frames={frames}");

        Main.DoUpdate_WhilePaused();
        Report("UpdateAutopause runs while paused", Calls(nameof(UpdatePlayer.UpdateAutopause)) == 1, $"calls={Calls(nameof(UpdatePlayer.UpdateAutopause))}");

        int key = Main.ReversedUpDownArmorSetBonuses ? 1 : 0;
        p.KeyDoubleTap(key);
        p.KeyDoubleTap(1 - key);
        Report("ArmorSetBonusActivated on the set bonus key only", Calls(nameof(UpdatePlayer.ArmorSetBonusActivated)) == 1,
            $"calls={Calls(nameof(UpdatePlayer.ArmorSetBonusActivated))}");
        p.KeyHoldDown(key, 60);
        Report("ArmorSetBonusHeld gets the hold time", Calls(nameof(UpdatePlayer.ArmorSetBonusHeld)) == 1 && UpdatePlayer.LastHold == 60,
            $"calls={Calls(nameof(UpdatePlayer.ArmorSetBonusHeld))} hold={UpdatePlayer.LastHold}");

        int loadout = p.CurrentLoadoutIndex, other = loadout == 0 ? 1 : 0;
        p.TrySwitchingLoadout(other);
        int switched = p.CurrentLoadoutIndex;
        p.TrySwitchingLoadout(loadout);
        Report("OnEquipmentLoadoutSwitched gets the old and new loadout", Calls(nameof(UpdatePlayer.OnEquipmentLoadoutSwitched)) == 2 &&
            switched == other && UpdatePlayer.OldLoadout == other && UpdatePlayer.NewLoadout == loadout,
            $"calls={Calls(nameof(UpdatePlayer.OnEquipmentLoadoutSwitched))} switched={switched} last={UpdatePlayer.OldLoadout}->{UpdatePlayer.NewLoadout}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csplayerupdate {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
