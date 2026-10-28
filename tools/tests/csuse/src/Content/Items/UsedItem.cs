using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CsUse.Content.Items;

public class UsedItem : ModItem
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
        Counts.Add("Used.CanUseItem");
        return true;
    }

    public override bool? UseItem(Player player)
    {
        Counts.Add("Used.UseItem");
        return true;
    }

    public override void UseAnimation(Player player)
    {
        Counts.Add("Used.UseAnimation");
        Item.useAnimation = 33;
    }

    public override void UseStyle(Player player, Rectangle heldItemFrame) => Counts.Add("Used.UseStyle");

    public override void HoldStyle(Player player, Rectangle heldItemFrame) => Counts.Add("Used.HoldStyle");

    public override void HoldItem(Player player) => Counts.Add("Used.HoldItem");

    public override Vector2? HoldoutOffset() => new Vector2(10f, 5f);

    public override void UpdateInventory(Player player) => Counts.Add("Used.UpdateInventory");
}
