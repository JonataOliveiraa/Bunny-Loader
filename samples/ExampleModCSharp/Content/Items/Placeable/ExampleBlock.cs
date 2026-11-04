using ExampleMod.Content.Items.Placeable.Furniture;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable;

public class ExampleBlock : ModItem
{
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 100;
        ItemID.Sets.ExtractinatorMode[Type] = Type;

        ItemID.Sets.CanBeExtractinated[Type] = true;
    }

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.ExampleBlock>());
        Item.width = 12;
        Item.height = 12;
    }

    public override void AddRecipes()
    {
        CreateRecipe(10)
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();

        CreateRecipe()
            .AddIngredient<ExampleWall>(4)
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();

        CreateRecipe()
            .AddIngredient<ExamplePlatform>(2)
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }

    public override void ExtractinatorUse(int extractinatorBlockType, ref int resultType, ref int resultStack)
    {
        if (Main.rand.NextBool(3))
        {
            resultType = ModContent.ItemType<ExampleOre>();
            if (Main.rand.NextBool(5))
            {
                resultStack += Main.rand.Next(2);
            }
        }
    }
}
