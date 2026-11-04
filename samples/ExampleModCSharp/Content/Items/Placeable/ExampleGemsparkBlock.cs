using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable;

public class ExampleGemsparkBlock : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(Mod.Find<ModTile>("ExampleGemsparkBlockOn").Type);
    }

    public override void AddRecipes()
    {
        CreateRecipe(20)
            .AddIngredient<ExampleGem>()
            .AddIngredient(ItemID.Glass, 20)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
