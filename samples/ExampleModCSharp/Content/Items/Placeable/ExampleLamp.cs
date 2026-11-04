using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable;

internal class ExampleLamp : ModItem
{
    public override LocalizedText Tooltip => LocalizedText.Empty;

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.ExampleLamp>());
        Item.width = 10;
        Item.height = 24;
        Item.value = 500;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
