using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable;

public sealed class ExampleBlockItem : ModItem
{
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 100;

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.ExampleBlock>());
        Item.width = 12;
        Item.height = 12;
    }

    public override void AddRecipes()
    {
        CreateRecipe(10).AddIngredient<ExampleItem>().AddTile<Tiles.Furniture.ExampleWorkbench>().Register();
    }
}
