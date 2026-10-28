using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CsTooltip.Content.Items;

public class Lore : ModItem
{
    public static string Seen = "";

    public static int PreLines, PostLines, PostTooltips;

    public static string Drawn = "";

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.maxStack = 99;
        Item.material = true;
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        Seen = string.Join(",", tooltips.Select(l => l.FullName));
        TooltipLine? first = tooltips.Find(l => l.Mod == "Terraria" && l.Name == "Tooltip0");
        if (first != null) first.Text = "Changed first line";
        tooltips.Find(l => l.Name == "Tooltip1")?.Hide();
        tooltips.Add(new TooltipLine(Mod, "Gold", "Gold line") { OverrideColor = new Color(255, 215, 0) });
        tooltips.Add(new TooltipLine(Mod, "Tagged", "Plain and [c/FF4040:red] text"));
        tooltips.Add(new TooltipLine(Mod, "Extra", "Added by the mod") { IsModifier = true });
    }

    public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
    {
        PreLines++;
        if (line.Name == "Gold") line.X += 8;
        return true;
    }

    public override void PostDrawTooltipLine(DrawableTooltipLine line) => PostLines++;

    public override void PostDrawTooltip(ReadOnlyCollection<DrawableTooltipLine> lines)
    {
        PostTooltips++;
        Drawn = string.Join(",", lines.Select(l => l.Name));
    }
}
