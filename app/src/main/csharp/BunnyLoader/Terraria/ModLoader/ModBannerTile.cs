using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Core;
using Terraria.ObjectData;

namespace Terraria.ModLoader;

public abstract class ModBannerTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = true;
        TileID.Sets.DisableSmartCursor[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2Top);
        TileObjectData.newTile.Height = 3;
        TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom | AnchorType.PlanterBox, TileObjectData.newTile.Width, 0);
        TileObjectData.newTile.DrawYOffset = -2;

        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.AnchorTop = new AnchorData(AnchorType.PlatformNonHammered | AnchorType.AllFlatHeight, TileObjectData.newTile.Width, 0);
        TileObjectData.newAlternate.DrawYOffset = -10;
        TileObjectData.addAlternate(0);
        TileObjectData.addTile(Type);

        DustType = -1;
        AddMapEntry(new Color(13, 88, 130), Language.GetText("MapObject.Banner"));
    }

    public override void NearbyEffects(int i, int j, bool closer)
    {
        if (closer)
            return;
        int item = TileLoader.GetItemDropFromTypeAndStyle(Type, TileObjectData.GetTileStyle(Main.tile[i, j]));
        if (!BannerHooks.BannerOfItem(item, out int banner))
            return;
        var strength = ItemID.Sets.BannerStrength;
        if ((uint)item >= (uint)strength.Length || !strength[item].Enabled)
            return;
        SceneMetrics metrics = TileHooks.ScanningMetrics;
        BannerHooks.Fit(metrics);
        metrics.NPCBannerBuff[banner] = true;
        metrics.hasBanner = true;
    }

    public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY)
    {
        offsetY += 2;
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        var data = TileObjectData.GetTileData(tile);
        if (!data.IsNull && tile.frameX % data.CoordinateFullWidth == 0 && tile.frameY % data.CoordinateFullHeight == 0)
            TileHooks.AddVine(i, j);
        return false;
    }
}
