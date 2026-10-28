using System;
using System.Collections.Generic;
using CsPrefix.Content.Items;
using CsPrefix.Content.Prefixes;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using BunnyLoader.Interop;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsPrefix;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsPrefixMod>().Logger;

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
        Log.Info($"csprefix END pass={pass} fail={fail}");
    }

    static void Run(Player p)
    {
        ModPrefix shiny = ModContent.GetInstance<Shiny>(), lucky = ModContent.GetInstance<Lucky>();
        Report("the prefixes get types after the game's", shiny.Type >= PrefixLoader.VanillaCount && PrefixLoader.GetPrefix(shiny.Type) == shiny &&
            lucky.Type >= PrefixLoader.VanillaCount && lucky.Type != shiny.Type && lucky.Type < PrefixLoader.PrefixCount, $"shiny={shiny.Type} lucky={lucky.Type} count={PrefixLoader.PrefixCount}");
        string name = Lang.prefix[shiny.Type].Value ?? "";
        Report("the name goes into Lang.prefix", name == "Shiny", $"'{name}'");

        Item sword = Make(ModContent.ItemType<TestSword>());
        var rollable = new List<int>();
        var list = sword.GetRollablePrefixes();
        if (!list.IsNull) for (int i = 0; i < list.Length; i++) rollable.Add(list[i]);
        Report("a mod weapon rolls the game's melee prefixes and the mod's", rollable.Contains(shiny.Type) && rollable.Count > 1,
            string.Join(",", rollable));
        Report("AllowPrefix false takes a prefix out", !rollable.Contains(TestSword.Forbidden), "");

        bool ok = sword.TryGetPrefixStatMultipliersForItem(shiny.Type, out float dmg, out _, out _, out _, out _, out _, out int crt, out _, out _, out float value);
        Report("SetStats and ModifyValue", ok && Math.Abs(dmg - 1.2f) < 1e-4 && crt == 5 && Math.Abs(value - 1.2f * 1.1f * 1.1f) < 1e-3,
            $"ok={ok} dmg={dmg} crt={crt} value={value}");

        Shiny.Applied = 0;
        int crit = sword.crit;
        bool prefixed = sword.Prefix(shiny.Type, out _);
        Report("the prefix applies the stats", prefixed && sword.prefix == shiny.Type && sword.damage == 24 && sword.crit == crit + 5,
            $"prefixed={prefixed} prefix={sword.prefix} damage={sword.damage} crit={sword.crit}");
        Report("Apply runs after the game's stats", Shiny.Applied == 1, $"calls={Shiny.Applied}");

        List<string> lines = Lines(sword);
        Report("GetTooltipLines adds a line", lines.Contains("Shines"), string.Join(" | ", lines));
        Report("a prefixed item's tooltip starts with its name, as the game's prefixes do", lines.Count > 0 && lines[0] == sword.HoverName,
            $"first='{(lines.Count > 0 ? lines[0] : "")}' name='{sword.HoverName}'");
        Report("a mod weapon does not stack by default (tModLoader's maxStack 1)", sword.maxStack == 1, $"maxStack={sword.maxStack}");

        Item charm = Make(ModContent.ItemType<Charm>());
        bool accessory = charm.Prefix(lucky.Type, out _);
        Lucky.Effects = 0;
        p.GrantPrefixBenefits(charm);
        Report("ApplyAccessoryEffects runs with the accessory", accessory && charm.prefix == lucky.Type && Lucky.Effects == 1,
            $"prefixed={accessory} prefix={charm.prefix} calls={Lucky.Effects}");
    }

    static Item Make(int type)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        return item;
    }

    static List<string> Lines(Item item)
    {
        var texts = GameArray<GameString>.New(new GameClass("System", "String"), 30);
        var prefix = GameArray<bool>.New(new GameClass("System", "Boolean"), 30);
        var bad = GameArray<bool>.New(new GameClass("System", "Boolean"), 30);
        int yoyo = -1, research = -1, materials = -1, count = 0, setBonus = -1, shared = -1;
        Color setColor = new(255, 255, 255, 255);
        Main.MouseText_DrawItemTooltip_GetLinesInfo(item, ref yoyo, ref research, ref materials, item.knockBack, ref count, texts, prefix, bad,
            ref setBonus, ref setColor, ref shared);
        var lines = new List<string>();
        for (int i = 0; i < count; i++) lines.Add(texts[i].ToString() ?? "");
        return lines;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csprefix {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
