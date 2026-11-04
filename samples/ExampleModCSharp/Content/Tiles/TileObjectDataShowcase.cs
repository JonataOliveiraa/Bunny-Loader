using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

public class TileObjectDataShowcase : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;

        TileObjectData.newTile.UsesCustomCanPlace = true;
        TileObjectData.newTile.Width = 2;
        TileObjectData.newTile.Height = 2;
        TileObjectData.newTile.CoordinateWidth = 16;
        TileObjectData.newTile.CoordinateHeights = [16, 16];
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.Origin = new Point16(0, 1);
        TileObjectData.newTile.CoordinatePadding = 0;

        TileObjectData.newTile.StyleMultiplier = 8;
        TileObjectData.newTile.RandomStyleRange = 4;
        TileObjectData.newTile.StyleWrapLimit = 16;
        TileObjectData.newTile.StyleLineSkip = 4;

        TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;

        TileObjectData.newTile.AnchorLeft = new AnchorData(AnchorType.AlternateTile, TileObjectData.newTile.Height, 0);
        TileObjectData.newTile.AnchorRight = new AnchorData(AnchorType.AlternateTile, TileObjectData.newTile.Height, 0);

        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(PostPlaceMethod, -1, 0, true);

        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
        TileObjectData.addAlternate(4);

        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Origin = new Point16(1, 1);
        TileObjectData.addAlternate(0);
        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
        TileObjectData.newAlternate.Origin = new Point16(1, 1);
        TileObjectData.addAlternate(4);

        TileObjectData.newSubTile.CopyFrom(TileObjectData.newTile);
        TileObjectData.newSubTile.LinkedAlternates = true;
        TileObjectData.newSubTile.AnchorAlternateTiles = [TileID.Gold];
        TileObjectData.addSubTile(0);

        TileObjectData.newSubTile.CopyFrom(TileObjectData.newTile);
        TileObjectData.newSubTile.LinkedAlternates = true;
        TileObjectData.newSubTile.AnchorAlternateTiles = [TileID.Silver];
        TileObjectData.addSubTile(1);

        TileObjectData.newSubTile.CopyFrom(TileObjectData.newTile);
        TileObjectData.newSubTile.LinkedAlternates = true;
        TileObjectData.newSubTile.AnchorAlternateTiles = [TileID.Copper];
        TileObjectData.addSubTile(2);

        TileObjectData.newSubTile.CopyFrom(TileObjectData.newTile);
        TileObjectData.newSubTile.LinkedAlternates = true;
        TileObjectData.newSubTile.AnchorAlternateTiles = [ModContent.TileType<ExampleBlock>()];
        TileObjectData.addSubTile(3);

        TileObjectData.addTile(Type);

        AnimationFrameHeight = TileObjectData.GetTileData(Type, 0).CoordinateFullHeight;
    }

    private int PostPlaceMethod(int x, int y, int type, int style, int direction, int alternate)
    {
        var tileData = TileObjectData.GetTileData(type, style, alternate);

        int alternatePlacement = -1;

        Main.NewText($"Style: {style}, Alternate Offset: {tileData.Style}, Random Offset: {alternatePlacement - tileData.Style}, Placement Style: {alternatePlacement}, Full Placement Style: {style * tileData.StyleMultiplier + alternatePlacement}, Direction: {direction}, Alternate Index: {alternate}, Origin: ({tileData.Origin.X}, {tileData.Origin.Y})");

        return 0;
    }

    public override bool RightClick(int i, int j)
    {
        SoundEngine.PlaySound(SoundID.Mech, new Vector2(i * 16, j * 16));

        Tile tile = Main.tile[i, j];
        (int topX, int topY) = TileObjectData.TopLeft(i, j);

        short frameAdjustment = (short)(tile.TileFrameY % 128 >= 96 ? -96 : 96);

        for (int x = topX; x < topX + 2; x++)
        {
            for (int y = topY; y < topY + 2; y++)
            {
                Main.tile[x, y].TileFrameY += frameAdjustment;
            }
        }

        if (Main.netMode != NetmodeID.SinglePlayer)
        {
            NetMessage.SendTileSquare(-1, topX, topY, 2, 2);
        }

        return true;
    }

    public override void AnimateTile(ref int frame, ref int frameCounter)
    {
        if (++frameCounter >= 16)
        {
            frameCounter = 0;
            frame = ++frame % 3;
        }
    }

    public override void AnimateIndividualTile(int type, int i, int j, ref int frameXOffset, ref int frameYOffset)
    {
        var tile = Main.tile[i, j];

        if (tile.TileFrameY % 128 >= 96)
        {
            frameYOffset = 0;
        }
    }
}

public class TileObjectDataShowcaseStyle0 : ModItem
{
    public override string Texture => $"ExampleMod/Content/Tiles/TileObjectDataShowcaseItemA";

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<TileObjectDataShowcase>(), 0);
    }
}

public class TileObjectDataShowcaseStyle1 : ModItem
{
    public override string Texture => $"ExampleMod/Content/Tiles/TileObjectDataShowcaseItemA";

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<TileObjectDataShowcase>(), 1);
    }
}

public class TileObjectDataShowcaseStyle2 : ModItem
{
    public override string Texture => $"ExampleMod/Content/Tiles/TileObjectDataShowcaseItemA";

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<TileObjectDataShowcase>(), 2);
    }
}

public class TileObjectDataShowcaseStyle3 : ModItem
{
    public override string Texture => $"ExampleMod/Content/Tiles/TileObjectDataShowcaseItemB";

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<TileObjectDataShowcase>(), 3);
    }
}
