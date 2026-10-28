using Terraria;
using Terraria.ID;
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

    public override void AddRecipes()
    {
        CreateRecipe(999)
            .AddIngredient(ItemID.DirtBlock, 10)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
