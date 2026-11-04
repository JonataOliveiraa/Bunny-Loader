using ExampleMod.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

internal class ExampleAnimatedTile : ModTile
{
    private Asset<Texture2D> flameTexture;
    public override void Load()
    {
        flameTexture = ModContent.Request<Texture2D>(Texture + "_Flame");
    }

    public override void SetStaticDefaults()
    {
        Main.tileLighted[Type] = true;

        Main.tileFrameImportant[Type] = true;

        Main.tileLavaDeath[Type] = true;

        TileID.Sets.MultiTileSway[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2Top);

        TileObjectData.addTile(Type);

        LocalizedText name = CreateMapEntryName();
        AddMapEntry(new Color(238, 145, 105), name);
    }

    private readonly int animationFrameWidth = 18;

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        r = 0.93f;
        g = 0.11f;
        b = 0.12f;
    }

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        if (i % 2 == 1)
            spriteEffects = SpriteEffects.FlipHorizontally;
    }

    public override void AnimateIndividualTile(int type, int i, int j, ref int frameXOffset, ref int frameYOffset)
    {
        int uniqueAnimationFrame = Main.tileFrame[Type] + i;
        if (i % 2 == 0)
            uniqueAnimationFrame += 3;
        if (i % 3 == 0)
            uniqueAnimationFrame += 3;
        if (i % 4 == 0)
            uniqueAnimationFrame += 3;
        uniqueAnimationFrame %= 6;

        frameXOffset = uniqueAnimationFrame * animationFrameWidth;
    }

    public override bool KillSound(int i, int j, bool fail)
    {
        if (!fail)
        {
            SoundEngine.PlaySound(SoundID.Shatter, new Vector2(i, j).ToWorldCoordinates());
            return false;
        }
        return base.KillSound(i, j, fail);
    }

    public override void AnimateTile(ref int frame, ref int frameCounter)
    {
        frame = Main.tileFrame[TileID.FireflyinaBottle];
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];

        if (TileObjectData.IsTopLeft(tile))
        {
            Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.MultiTileVine);
        }

        return false;
    }

    public override void AdjustMultiTileVineParameters(int i, int j, ref float? overrideWindCycle, ref float windPushPowerX, ref float windPushPowerY, ref bool dontRotateTopTiles, ref float totalWindMultiplier, ref Texture2D glowTexture, ref Color glowColor)
    {
        overrideWindCycle = 1f;
        windPushPowerY = 0f;
    }

    public override void GetTileFlameData(int i, int j, ref TileDrawing.TileFlameData tileFlameData)
    {
        tileFlameData.flameTexture = flameTexture.Value;
        tileFlameData.flameColor = new Color(200, 200, 200, 0);
        tileFlameData.flameCount = 1;
    }
}

internal class ExampleAnimatedTileItem : ModItem
{
    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.FireflyinaBottle);
        Item.createTile = ModContent.TileType<ExampleAnimatedTile>();
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Furniture.ExampleWorkbench>()
            .Register();
    }
}
