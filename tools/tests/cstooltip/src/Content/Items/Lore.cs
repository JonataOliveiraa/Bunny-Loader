using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CsTooltip.Content.Items;

public class Lore : ModItem
{
    public static string Seen = "";

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
        tooltips.Add(new TooltipLine(Mod, "Extra", "Added by the mod") { IsModifier = true });
    }
}
