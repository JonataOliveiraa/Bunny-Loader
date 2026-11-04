using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable;

public class ExampleWallAdvanced : ModItem
{
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 400;
    }

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableWall(ModContent.WallType<Walls.ExampleWallAdvanced>());
    }

    public override void AddRecipes()
    {
        CreateRecipe(4)
            .AddIngredient<ExampleBlock>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
