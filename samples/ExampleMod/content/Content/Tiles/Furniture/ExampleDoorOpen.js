const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { AnchorType, TileObjectDirection } = Terraria.Enums;
const Main = Terraria.Main;

// A porta aberta: 2x3, aberta para a direita (origem na coluna 0) ou para a
// esquerda (origem na coluna 1). Tocar fecha: CloseDoorID.
export class ExampleDoorOpen extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileSolid[this.Type] = false;
        Main.tileLavaDeath[this.Type] = true;
        Main.tileNoSunLight[this.Type] = true;
        TileID.Sets.HousingWalls[this.Type] = true;   // sem sólido, conta como parede da casa
        TileID.Sets.HasOutlines[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.CloseDoorID[this.Type] = ModContent.TileType('ExampleDoorClosed');
        TileID.Sets.DrawTileInSolidLayer[this.Type] = true;
        TileID.Sets.RoomNeeds.CountsAsDoor[this.Type] = true;

        this.DustType = DustID.Platinum;
        this.AdjTiles = [TileID.OpenDoor];
        // Quebrada aberta, dá a porta (o item que coloca a fechada).
        this.RegisterItemDrop(ModContent.ItemType('ExampleDoor'), 0);
        this.AddMapEntry(Color.new(200, 200, 200), 'Door');

        const tile = TileObjectData.newTile;
        tile.Width = 2;
        tile.Height = 3;
        tile.Origin = Point16.new(0, 0);
        tile.AnchorTop = AnchorData.new(AnchorType.SolidTile, 1, 0);
        tile.AnchorBottom = AnchorData.new(AnchorType.SolidTile, 1, 0);
        tile.UsesCustomCanPlace = true;
        tile.LavaDeath = true;
        tile.CoordinateHeights = [16, 16, 16];
        tile.CoordinateWidth = 16;
        tile.CoordinatePadding = 2;
        tile.StyleHorizontal = true;
        tile.StyleMultiplier = 2;
        tile.StyleWrapLimit = 2;
        tile.Direction = TileObjectDirection.PlaceRight;

        // Aberta para a direita, tocada em qualquer das três linhas.
        for (const row of [1, 2]) {
            TileObjectData.newAlternate.CopyFrom(tile);
            TileObjectData.newAlternate.Origin = Point16.new(0, row);
            TileObjectData.addAlternate(0);
        }
        // Aberta para a esquerda: a porta fica na coluna 1.
        for (const row of [0, 1, 2]) {
            const alt = TileObjectData.newAlternate;
            alt.CopyFrom(tile);
            alt.Origin = Point16.new(1, row);
            alt.AnchorTop = AnchorData.new(AnchorType.SolidTile, 1, 1);
            alt.AnchorBottom = AnchorData.new(AnchorType.SolidTile, 1, 1);
            alt.Direction = TileObjectDirection.PlaceLeft;
            TileObjectData.addAlternate(1);
        }
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
