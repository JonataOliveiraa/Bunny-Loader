using Terraria;
using Terraria.ModLoader;

namespace CsUse.Content.Items;

public class BlockedItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;
        Item.useStyle = 1;
        Item.useTime = 20;
        Item.useAnimation = 20;
    }

    public override bool CanUseItem(Player player)
    {
        Counts.Add("Blocked.CanUseItem");
        return false;
    }
}
