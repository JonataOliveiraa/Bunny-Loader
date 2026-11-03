using ExampleMod.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items;

public class ExampleGolfBall : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToGolfBall(ModContent.ProjectileType<ExampleGolfBallProjectile>());
    }

    public override void ModifyResearchSorting(ref ContentSamples.CreativeHelper.ItemGroup itemGroup)
    {
        itemGroup = ContentSamples.CreativeHelper.ItemGroup.Golf;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.GolfBall)
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.DyeVat)
            .Register();
    }
}
