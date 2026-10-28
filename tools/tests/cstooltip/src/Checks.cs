using System;
using System.Collections.Generic;
using CsTooltip.Common;
using CsTooltip.Content.Items;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using BunnyLoader.Interop;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsTooltip;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsTooltipMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
        Lore.PreLines = Lore.PostLines = Lore.PostTooltips = 0;
        Lore.Drawn = "";
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        if (frames == 120)
        {
            try
            {
                Run();
                Give(p);
            }
            catch (Exception e) { Report("run", false, e.ToString()); }
            Log.Info("cstooltip: open the inventory and hold your finger on the Lore Stone to test the drawing (2 minutes)");
            return;
        }
        if (frames < 120 || frames % 60 != 0) return;
        if (Lore.PostTooltips > 0)
        {
            Report("PreDrawTooltipLine and PostDrawTooltipLine run for each line", Lore.PreLines > 0 && Lore.PostLines == Lore.PreLines,
                $"pre={Lore.PreLines} post={Lore.PostLines}");
            Report("PostDrawTooltip gets the drawn lines", Lore.Drawn.Contains("Tooltip0") && Lore.Drawn.Contains("Gold") && Lore.Drawn.Contains("Tagged"),
                Lore.Drawn);
            Finish();
        }
        else if (frames >= 120 + 60 * 120)
        {
            Report("the tooltip was drawn", false, "the Lore Stone tooltip was not shown in 2 minutes");
            Finish();
        }
    }

    static void Finish()
    {
        done = true;
        Log.Info($"cstooltip END pass={pass} fail={fail} (look at the tooltip: 'Gold line' in gold and moved right, 'red' in red)");
    }

    static void Give(Player p)
    {
        int type = ModContent.ItemType<Lore>();
        for (int k = 0; k < 50; k++)
        {
            Item item = p.inventory[k];
            if (item.type == type) return;
        }
        for (int k = 0; k < 50; k++)
        {
            Item item = p.inventory[k];
            if (item.type > 0 && item.stack > 0) continue;
            item.SetDefaults(type, default);
            return;
        }
    }

    static void Run()
    {
        List<(string Text, bool Modifier)> lore = Lines(ModContent.ItemType<Lore>());
        Report("ModifyTooltips sees the game's lines by name", Lore.Seen.StartsWith("Terraria/ItemName,", StringComparison.Ordinal) &&
            Lore.Seen.Contains("Terraria/Tooltip0") && Lore.Seen.Contains("Terraria/Tooltip1") && Lore.Seen.Contains("Terraria/Material"), Lore.Seen);
        Report("a changed line is drawn with its new text", lore.Exists(l => l.Text == "Changed first line"), Dump(lore));
        Report("a hidden line is gone", !lore.Exists(l => l.Text == "Second line"), Dump(lore));
        Report("an added line comes last, as a modifier", lore.Count > 0 && lore[^1].Text == "Added by the mod" && lore[^1].Modifier, Dump(lore));
        Report("a color tag stays in the line text", lore.Exists(l => l.Text == "Plain and [c/FF4040:red] text"), Dump(lore));

        List<(string Text, bool Modifier)> sword = Lines(ItemID.CopperShortsword);
        Report("GlobalItem.ModifyTooltips on a vanilla weapon", SwordTip.Seen.Contains("Damage") && SwordTip.Seen.Contains("Speed") &&
            SwordTip.Seen.Contains("Knockback"), SwordTip.Seen);
        Report("the Damage line changed", sword.Exists(l => l.Text.EndsWith(" (global)", StringComparison.Ordinal)), Dump(sword));
    }

    static List<(string, bool)> Lines(int type)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        var texts = GameArray<GameString>.New(new GameClass("System", "String"), 30);
        var prefix = GameArray<bool>.New(new GameClass("System", "Boolean"), 30);
        var bad = GameArray<bool>.New(new GameClass("System", "Boolean"), 30);
        int yoyo = -1, research = -1, materials = -1, count = 0, setBonus = -1, shared = -1;
        Color setColor = new(255, 255, 255, 255);
        Main.MouseText_DrawItemTooltip_GetLinesInfo(item, ref yoyo, ref research, ref materials, item.knockBack, ref count, texts, prefix, bad,
            ref setBonus, ref setColor, ref shared);
        var lines = new List<(string, bool)>();
        for (int i = 0; i < count; i++) lines.Add((texts[i].ToString() ?? "", prefix[i]));
        return lines;
    }

    static string Dump(List<(string Text, bool Modifier)> lines) => string.Join(" | ", lines.ConvertAll(l => (l.Modifier ? "+" : "") + l.Text));

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cstooltip {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
