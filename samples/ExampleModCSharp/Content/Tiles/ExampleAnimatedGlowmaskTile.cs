using ExampleMod.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

public class ExampleAnimatedGlowmaskTile : ModTile
{
    private Asset<Texture2D> glowTexture;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);

        TileObjectData.newTile.Height = 3;

        TileObjectData.newTile.Origin = new Point16(1, 2);

        TileObjectData.newTile.CoordinateHeights = [16, 16, 18];

        TileObjectData.addTile(Type);

        AddMapEntry(new Color(75, 139, 166));

        AnimationFrameHeight = 56;

        glowTexture = ModContent.Request<Texture2D>(Texture + "_Glow");
    }

    public override void AnimateTile(ref int frame, ref int frameCounter)
    {
        frame = Main.tileFrame[TileID.LunarMonolith];
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];

        Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);

        int height = tile.TileFrameY % AnimationFrameHeight == 36 ? 18 : 16;

        int frameYOffset = (Main.tileFrame[Type] + 1) * AnimationFrameHeight;

        spriteBatch.Draw(
            TextureAssets.Tile[Type].Value,
            new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + zero,
            new Rectangle(tile.TileFrameX, tile.TileFrameY + frameYOffset, 16, height),
            Lighting.GetColor(i, j), 0f, default, 1f, SpriteEffects.None, 0f);

        spriteBatch.Draw(
            glowTexture.Value,
            new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + zero,
            new Rectangle(tile.TileFrameX, tile.TileFrameY + frameYOffset, 16, height),
            Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        return false;
    }
}

internal class ExampleAnimatedGlowmaskTileItem : ModItem
{
    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.VoidMonolith);
        Item.createTile = ModContent.TileType<ExampleAnimatedGlowmaskTile>();
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Furniture.ExampleWorkbench>()
            .Register();
    }
}
