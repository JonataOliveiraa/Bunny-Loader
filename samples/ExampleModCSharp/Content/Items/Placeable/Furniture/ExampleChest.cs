using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable.Furniture;

public class ExampleChest : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.Furniture.ExampleChest>());
        Item.width = 26;
        Item.height = 22;
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

public class ExampleChestKey : ModItem
{
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 3;
    }

    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.GoldenKey);
    }
}
