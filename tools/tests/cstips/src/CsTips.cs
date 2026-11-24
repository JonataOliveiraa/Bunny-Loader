using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using log4net;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ModLoader;

namespace CsTips;

public sealed class CsTipsMod : Mod
{
    const int Draws = 300;

    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsTipsMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cstips {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done || ++frames < 60) return;
        done = true;
        try { Run(); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        Log.Info($"cstips END pass={pass} fail={fail}");
    }

    static void Run()
    {
        GameTipsDisplay display = Main.gameTips;
        if (display.IsNull)
        {
            Report("the game has a tip display", false, "Main.gameTips is null");
            return;
        }
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        int repeats = 0, untranslated = 0;
        string last = "";
        display.ClearTips();
        for (int n = 0; n < Draws; n++)
        {
            display.AddNewTip(n * 20.0);
            var list = new GameList(display._currentTips);
            var tip = list.Get<GameTipsDisplay.GameTip>(list.Count - 1);
            string key = tip._textKey.Key ?? "";
            string text = tip._formattedText ?? "";
            if (n == 0) Log.Info($"cstips first tip {key}: {text}");
            if (text == key) untranslated++;
            if (key == last) repeats++;
            last = key;
            seen[key] = seen.TryGetValue(key, out int count) ? count + 1 : 1;
        }
        display.ClearTips();
        int mod = 0, vanilla = 0, input = 0;
        foreach ((string key, int count) in seen)
        {
            if (key.StartsWith("Mods.ExampleMod.GameTips.", StringComparison.Ordinal)) mod += count;
            else if (key.StartsWith("LoadingTips_Default.", StringComparison.Ordinal)) vanilla += count;
            else if (key.StartsWith("LoadingTips_Keyboard.", StringComparison.Ordinal) || key.StartsWith("LoadingTips_GamePad.", StringComparison.Ordinal)) input += count;
        }
        Report("mod tips show up", mod > 0, $"exampleMod={mod} of {Draws}");
        Report("vanilla tips still show up", vanilla > 0, $"vanilla={vanilla} of {Draws}");
        Report("a tip hidden by ModifyGameTipVisibility never shows", !seen.ContainsKey("Mods.ExampleMod.GameTips.DisabledExampleTip"), $"disabled={(seen.TryGetValue("Mods.ExampleMod.GameTips.DisabledExampleTip", out int d) ? d : 0)}");
        Report("hidden vanilla tips never show", !seen.ContainsKey("LoadingTips_Default.9") && !seen.ContainsKey("LoadingTips_Default.76"), $"bloodMoon={(seen.ContainsKey("LoadingTips_Default.9") ? 1 : 0)} eclipse={(seen.ContainsKey("LoadingTips_Default.76") ? 1 : 0)}");
        Report("touch shows no keyboard or gamepad tips", input == 0, $"input={input} mode={XNAUnityRunner.PrimaryInputMode}");
        Report("the same tip does not show twice in a row", repeats == 0, $"repeats={repeats}");
        Report("every tip has text", !seen.ContainsKey("") && untranslated == 0, $"distinct={seen.Count} untranslated={untranslated}");
    }
}
