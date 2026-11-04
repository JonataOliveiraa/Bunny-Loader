using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable.Furniture;

public class ExamplePlatform : ModItem
{
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 200;
    }

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.Furniture.ExamplePlatform>());
        Item.width = 8;
        Item.height = 10;
    }

    public override void AddRecipes()
    {
        CreateRecipe(2)
            .AddIngredient<ExampleBlock>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
