using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items;

public class ExampleItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;

        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.buyPrice(silver: 1);
    }
}
