using System;
using CsLoc.Content.Items;
using log4net;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsLoc;

public static class Checks
{
    static int pass, fail;

    static ILog Log => ModContent.GetInstance<CsLocMod>().Logger;

    public static void OnGameThread()
    {
        Text("quoteless", "Mods.CsLoc.Plain", "plain text");
        Text("quoted with escapes", "Mods.CsLoc.Quoted", "tab\tand \"quotes\"");
        Text("single quotes", "Mods.CsLoc.Single", "it's");
        Text("number", "Mods.CsLoc.Number", "42");
        Text("spaces around the colon", "Mods.CsLoc.Spaced", "trailing spaces kept inside");
        Text("multiline", "Mods.CsLoc.Multi", "line 1\nline 2");
        Text("nested on one line", "Mods.CsLoc.Nested.Deep.Value", "deep");
        Text("en-US.hjson with the full path", "Mods.CsLoc.OnlyEnglish", "english only");
        string both = Language.GetTextValue("Mods.CsLoc.Both");
        Report("a culture of its own or English", both == "portugues" || both == "english", both);
        Report("GetTextValue with arguments", Language.GetTextValue("Mods.CsLoc.Greeting", "Bunny") == "Hello, Bunny!",
            Language.GetTextValue("Mods.CsLoc.Greeting", "Bunny"));
        Report("LocalizedText.Format", ModContent.GetInstance<CsLocMod>().GetLocalization("Greeting").Format("C#") == "Hello, C#!", "");

        LocGem gem = ModContent.GetInstance<LocGem>();
        string name = gem.DisplayName.Value ?? "";
        Report("DisplayName from hjson", name == "Localized Gem" || name == "Gema Localizada", name);
        Report("item name in the game", Lang.GetItemNameValue(gem.Type) == name, $"{Lang.GetItemNameValue(gem.Type)}");
        Report("GetLocalizationKey of an item", gem.GetLocalizationKey("Custom") == "Mods.CsLoc.Items.LocGem.Custom", gem.GetLocalizationKey("Custom"));
        Report("GetLocalizedValue", gem.GetLocalizedValue("Custom") == "from the item", gem.GetLocalizedValue("Custom"));
        Report("Tooltip without a translation is empty", (gem.Tooltip.Value ?? "") == "", $"'{gem.Tooltip.Value}'");

        LocalizedText registered = Language.GetOrRegister("Mods.CsLoc.Registered", () => "made here");
        Report("GetOrRegister with a default", registered.Value == "made here" && Language.GetTextValue("Mods.CsLoc.Registered") == "made here",
            $"{registered.Value} / {Language.GetTextValue("Mods.CsLoc.Registered")}");
        Report("GetOrRegister keeps what exists", Language.GetOrRegister("Mods.CsLoc.Plain", () => "other").Value == "plain text", "");
        Report("GetOrRegister of a game key", Language.GetOrRegister("ItemName.IronPickaxe").Value == Lang.GetItemNameValue(1),
            $"{Language.GetOrRegister("ItemName.IronPickaxe").Value}");
        Report("a missing key gives the key", Language.GetTextValue("Mods.CsLoc.Nope") == "Mods.CsLoc.Nope", "");
        Log.Info($"csloc END pass={pass} fail={fail}");
    }

    static void Text(string name, string key, string expected)
    {
        string got = Language.GetTextValue(key);
        Report(name, got == expected, $"'{got}'");
    }

    public static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csloc {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
