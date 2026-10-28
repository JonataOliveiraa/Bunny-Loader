using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsUse.Common;

public class UseGlobal : GlobalItem
{
    public override bool CanUseItem(Item item, Player player)
    {
        if (item.type != ItemID.WoodenSword) return true;
        Counts.Add("Global.CanUseItem");
        return false;
    }

    public override bool? UseItem(Item item, Player player)
    {
        Counts.Add("Global.UseItem");
        return null;
    }

    public override void UseStyle(Item item, Player player, Rectangle heldItemFrame) => Counts.Add("Global.UseStyle");

    public override void UpdateInventory(Item item, Player player) => Counts.Add("Global.UpdateInventory");
}
