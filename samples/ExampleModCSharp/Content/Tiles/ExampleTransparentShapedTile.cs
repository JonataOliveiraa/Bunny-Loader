using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Tiles;

[LegacyName("ExampleCustomLiquidMaskedTile")]
public sealed class ExampleTransparentShapedTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;

        Main.tileLighted[Type] = true;
        Main.tileBlockLight[Type] = false;

        TileID.Sets.TouchDamageImmediate[Type] = 30;
        TileID.Sets.TouchDamageHot[Type] = true;

        TileID.Sets.DrawsWalls[Type] = true;

        AddMapEntry(Color.Orange);
    }

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        r = 2f;
        g = 1.33f;
        b = 0.4f;
    }
}
public sealed class ExampleTransparentShapedTileItem : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<ExampleTransparentShapedTile>());
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<Items.Placeable.ExampleBlock>()
            .Register();
    }
}
