const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

// Bancada: estação de criação (as receitas com AddTile dela) e, pelo
// AdjTiles, também conta como a bancada do jogo.
export class ExampleWorkbench extends ModTile {
    SetStaticDefaults() {
        Main.tileTable[this.Type] = true;
        Main.tileSolidTop[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.HasOutlines[this.Type] = true;
        TileID.Sets.IgnoredByNpcStepUp[this.Type] = true;

        this.DustType = DustID.Platinum;
        this.AdjTiles = [TileID.WorkBenches];

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x1);
        TileObjectData.newTile.CoordinateHeights = [18];
        TileObjectData.addTile(this.Type);

        TileID.Sets.RoomNeeds.CountsAsTable[this.Type] = true;
        this.AddMapEntry(Color.new(200, 200, 200), this.CreateMapEntryName());
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }
}
