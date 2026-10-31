using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable.Furniture;

public sealed class ExampleWorkbench : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.Furniture.ExampleWorkbench>());
        Item.width = 28;
        Item.height = 14;
        Item.value = 150;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.WorkBench).AddIngredient<ExampleItem>(10).Register();
}
