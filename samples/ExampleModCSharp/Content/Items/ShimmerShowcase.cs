using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    public class ShimmerShowcaseConditions : ModItem
    {
        public override string Texture => "ExampleMod/Content/Items/ExampleItem";

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddIngredient(ItemID.Cactus)
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .AddDecraftCondition(Condition.InDesert)
                .Register();

            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddIngredient(ItemID.RottenChunk)
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .AddDecraftCondition(Condition.CorruptWorld)
                .Register();

            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddIngredient(ItemID.Vertebrae)
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .AddDecraftCondition(Condition.CrimsonWorld)
                .Register();
        }
    }

    public class ShimmerShowcaseCustomShimmerResult : ModItem
    {
        public override string Texture => "ExampleMod/Content/Items/ExampleItem";

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddIngredient(ItemID.PadThai)
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .DisableDecraft()
                .Register();

            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .AddCustomShimmerResult(ItemID.RottenEgg)
                .AddCustomShimmerResult(ItemID.Chain, 3)
                .Register();
        }
    }
}
