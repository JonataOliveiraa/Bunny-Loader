using Terraria;
using Terraria.ModLoader;

namespace CsEquip.Content.Items;

public class TestCharm : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.accessory = true;
    }

    public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) =>
        equippedItem.type != ModContent.ItemType<TestShield>() && incomingItem.type != ModContent.ItemType<TestShield>();
}
