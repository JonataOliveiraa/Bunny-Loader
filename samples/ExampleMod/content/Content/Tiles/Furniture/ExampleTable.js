const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

export class ExampleTable extends ModTile {
    SetStaticDefaults() {
        Main.tileTable[this.Type] = true;
        Main.tileSolidTop[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.IgnoredByNpcStepUp[this.Type] = true;

        this.DustType = DustID.Platinum;
        this.AdjTiles = [TileID.Tables];

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.addTile(this.Type);

        TileID.Sets.RoomNeeds.CountsAsTable[this.Type] = true;
        this.AddMapEntry(Color.new(200, 200, 200), 'Table');
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }
}
