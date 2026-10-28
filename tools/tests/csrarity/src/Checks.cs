using System;
using CsRarity.Content.Items;
using CsRarity.Content.Rarities;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CsRarity;

public static class Checks
{
    const int Legendary = 81, Broken = 39;

    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsRarityMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 120) return;
        done = true;
        try { Run(); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        Log.Info($"csrarity END pass={pass} fail={fail}");
    }

    static void Run()
    {
        int type = ModContent.RarityType<Teal>();
        Report("the rarity gets a type after the game's", type >= RarityLoader.VanillaCount && RarityLoader.GetRarity(type) is Teal && type < RarityLoader.RarityCount,
            $"type={type} count={RarityLoader.RarityCount}");
        Report("SetStaticDefaults ran", Teal.SetUp, "");

        Color popup = Item.GetPopupRarityColor(type);
        Report("the pickup text uses RarityColor", popup.R == Teal.Shade.R && popup.G == Teal.Shade.G && popup.B == Teal.Shade.B, $"{popup}");

        Item blade = Item.New();
        blade.SetDefaults(ModContent.ItemType<TealBlade>(), default);
        Report("the item keeps the mod rarity", blade.rare == type, $"rare={blade.rare}");

        bool good = blade.Prefix(Legendary, out _);
        Report("a good prefix keeps the rarity (the game would clamp it to 11)", good && blade.rare == type, $"prefixed={good} prefix={blade.prefix} rare={blade.rare}");

        blade.SetDefaults(ModContent.ItemType<TealBlade>(), default);
        bool bad = blade.Prefix(Broken, out _);
        Report("a bad prefix asks GetPrefixedRarity (offset < 0 gives 1)", bad && blade.rare == 1, $"prefixed={bad} prefix={blade.prefix} rare={blade.rare}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csrarity {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
