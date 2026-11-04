using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable;

public class ExampleLivingFire : ModItem
{
    public static Vector3 LightColor = new Vector3(0.7f, 0.8f, 0.8f);

    public override void SetStaticDefaults()
    {
        ItemID.Sets.IsLavaImmuneRegardlessOfRarity[Type] = true;
    }

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.ExampleLivingFireTile>());
        Item.width = 12;
        Item.height = 12;
    }

    public override void PostUpdate(WorldItem item)
    {
        Lighting.AddLight(item.Center, LightColor);
    }

    public override void AddRecipes()
    {
        CreateRecipe(20)
            .AddIngredient(ItemID.LivingFireBlock, 20)
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.CrystalBall)
            .Register();
    }
}
