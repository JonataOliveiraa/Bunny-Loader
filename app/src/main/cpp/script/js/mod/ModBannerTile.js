// Bandeira de inimigo 1x3. O estilo aponta para o item que a coloca, e
// BannerItem aponta desse item para o NPC representante.
class ModBannerTile extends ModTile {
    SetStaticDefaults() {
        const Main = Terraria.Main, { TileID } = Terraria.ID;
        const { TileObjectData } = Terraria.ObjectData;
        const { AnchorType } = Terraria.Enums;
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style1x2Top);
        tile.Height = 3;
        tile.CoordinateHeights = [16, 16, 16];
        tile.StyleHorizontal = true;
        tile.AnchorTop = AnchorData.new(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom | AnchorType.PlanterBox, tile.Width, 0);
        tile.DrawYOffset = -2;
        const alt = TileObjectData.newAlternate;
        alt.CopyFrom(tile);
        alt.AnchorTop = AnchorData.new(AnchorType.PlatformNonHammered | AnchorType.AllFlatHeight, tile.Width, 0);
        alt.DrawYOffset = -10;
        TileObjectData.addAlternate(0);
        TileObjectData.addTile(this.Type);
        this.DustType = -1;
        this.AddMapEntry(Color.new(13, 88, 130), 'MapObject.Banner');
    }

    PreDraw(i, j) {
        const tile = TileLoader.Tile(i, j);
        const data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(Tile getTile)'](tile);
        if (data && tile.frameX % data.CoordinateFullWidth === 0 && tile.frameY % data.CoordinateFullHeight === 0)
            TileDrawLoader.AddSpecialPoint(i, j, 5); // TileCounterType.MultiTileVine
        return false;
    }

    SetDrawPositions(i, j, width, offsetY) { offsetY.value += 2; }

    NearbyEffects(i, j, closer) {
        if (closer) return;
        const style = TileLoader.StyleAt(i, j);
        const item = TileLoader.GetItemDropFromTypeAndStyle(this.Type, style);
        const banner = BannerLoader.ByItem.get(item);
        if (banner === undefined) return;
        const effect = Terraria.ID.ItemID.Sets.BannerStrength[item];
        if (!effect || !effect.Enabled) return;
        const metrics = Terraria.Main.SceneMetrics;
        BannerLoader.Metrics(metrics);
        metrics.NPCBannerBuff[banner] = true;
        metrics.hasBanner = true;
    }
}
