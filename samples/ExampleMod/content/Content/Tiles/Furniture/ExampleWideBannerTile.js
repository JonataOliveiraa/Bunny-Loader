const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { AnchorType } = Terraria.Enums;
const Main = Terraria.Main;

// Estandarte largo (2x3) pendurado no teto ou numa plataforma. O balanço ao
// vento do tModLoader (MultiTileVine) não existe aqui: ele fica parado.
export class ExampleWideBannerTile extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.MultiTileSway[this.Type] = true;

        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style2xX);
        tile.LavaDeath = true;
        tile.Origin = Point16.Zero;
        tile.AnchorBottom = AnchorData.Empty;
        tile.AnchorTop = AnchorData.new(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom | AnchorType.PlanterBox, tile.Width, 0);
        tile.DrawYOffset = -2;

        // Numa plataforma, desce mais.
        const alt = TileObjectData.newAlternate;
        alt.CopyFrom(tile);
        alt.AnchorTop = AnchorData.new(AnchorType.PlatformNonHammered | AnchorType.AllFlatHeight, tile.Width, 0);
        alt.DrawYOffset = -10;
        TileObjectData.addAlternate(0);
        TileObjectData.addTile(this.Type);
    }

    SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY) {
        offsetY.value += 2;
    }
}
