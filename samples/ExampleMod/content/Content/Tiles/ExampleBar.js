const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;
const WorldGen = Terraria.WorldGen;

// Barra de exemplo empilhável: só fica em cima de chão firme.
export class ExampleBar extends ModTile {
    SetStaticDefaults() {
        Main.tileShine[this.Type] = 1100;
        Main.tileSolid[this.Type] = true;
        Main.tileSolidTop[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(200, 200, 200), 'Metal Bar');
    }

    TileFrame(i, j, resetFrame, noBreak) {
        if (!WorldGen.SolidTileAllowBottomSlope(i, j + 1)) {
            WorldGen['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](i, j, false, false, false);
        }
        return true;
    }
}
