using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsTooltip.Common;

public class SwordTip : GlobalItem
{
    public static string Seen = "";

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (item.type != ItemID.CopperShortsword) return;
        Seen = string.Join(",", tooltips.Select(l => l.Name));
        TooltipLine? damage = tooltips.Find(l => l.Name == "Damage");
        if (damage != null) damage.Text += " (global)";
    }
}
