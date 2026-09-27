const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

// Plataforma: o jogo enquadra pelas TileID.Sets.Platforms (as rampas também).
export class ExamplePlatform extends ModTile {
    SetStaticDefaults() {
        Main.tileLighted[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;
        Main.tileSolidTop[this.Type] = true;
        Main.tileSolid[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileTable[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.Platforms[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.RoomNeeds.CountsAsDoor[this.Type] = true;

        this.AddMapEntry(Color.new(200, 200, 200));
        this.DustType = DustID.Platinum;
        this.AdjTiles = [TileID.Platforms];

        const tile = TileObjectData.newTile;
        tile.CoordinateHeights = [16];
        tile.CoordinateWidth = 16;
        tile.CoordinatePadding = 2;
        tile.StyleHorizontal = true;
        tile.StyleMultiplier = 27;
        tile.StyleWrapLimit = 27;
        tile.UsesCustomCanPlace = false;
        tile.LavaDeath = true;
        TileObjectData.addTile(this.Type);
    }

    // Sólido, o SetupContent tira a luz do sol; a plataforma deixa passar.
    PostSetDefaults() {
        Main.tileNoSunLight[this.Type] = false;
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }
}
