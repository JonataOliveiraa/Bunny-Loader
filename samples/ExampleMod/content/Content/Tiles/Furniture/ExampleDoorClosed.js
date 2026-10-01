const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

// A porta fechada: tocar abre para o lado de quem tocou. OpenDoorID diz em que
// tile ela vira; o formato (1x3, apoiada em cima e embaixo) é o da porta do jogo.
export class ExampleDoorClosed extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileBlockLight[this.Type] = true;
        Main.tileSolid[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.NotReallySolid[this.Type] = true;
        TileID.Sets.DrawsWalls[this.Type] = true;
        TileID.Sets.HasOutlines[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.OpenDoorID[this.Type] = ModContent.TileType('ExampleDoorOpen');
        TileID.Sets.RoomNeeds.CountsAsDoor[this.Type] = true;

        this.DustType = DustID.Platinum;
        this.AdjTiles = [TileID.ClosedDoor];
        this.AddMapEntry(Color.new(200, 200, 200), 'MapObject.Door');

        TileObjectData.newTile.CopyFrom(TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](TileID.ClosedDoor, 0, 0));
        TileObjectData.addTile(this.Type);
    }

    NumDust(i, j, fail, num) {
        num.value = 1;
    }

    MouseOver(i, j) {
        const player = Main.LocalPlayer;
        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType('ExampleDoor');
    }
}
