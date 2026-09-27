const { DustID, TileID } = Terraria.ID;
const Main = Terraria.Main;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);

// Bloco de gemas: dois tiles, aceso e apagado, e o fio troca um pelo outro.
// O enquadramento é o dos blocos de gemas do jogo (SelfFrame8Way).
class ExampleGemsparkBlockBase extends ModTile {
    get IsOn() { return false; }
    get OtherVariant() { return ''; }

    SetStaticDefaults() {
        Main.tileSolid[this.Type] = true;
        if (this.IsOn) {
            Main.tileLighted[this.Type] = true;
            Main.tileShine2[this.Type] = true;
        }
        Main.tileBrick[this.Type] = true;
        TileID.Sets.GemsparkFramingTypes[this.Type] = this.Type;
        TileID.Sets.ForcedDirtMerging[this.Type] = true;
        TileID.Sets.AllBlocksWithSmoothBordersToResolveHalfBlockIssue[this.Type] = true;

        this.RegisterItemDrop(ModContent.ItemType('ExampleGemsparkBlock'));
        this.DustType = DustID.GemDiamond;
        this.AddMapEntry(Color.new(200, 200, 200));
    }

    TileFrame(i, j, resetFrame, noBreak) {
        Terraria.Framing.SelfFrame8Way(i, j, tileAt(i, j), resetFrame.value);
        return false;
    }

    HitWire(i, j) {
        const tile = tileAt(i, j);
        if (tile['bool actuator()']()) return;

        tile.type = ModContent.TileType(this.OtherVariant);
        Terraria.WorldGen.SquareTileFrame(i, j, true);
        Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, TileChangeType changeType)'](-1, i, j, 0);
    }
}

export class ExampleGemsparkBlockOn extends ExampleGemsparkBlockBase {
    get IsOn() { return true; }
    get OtherVariant() { return 'ExampleGemsparkBlockOff'; }
}

export class ExampleGemsparkBlockOff extends ExampleGemsparkBlockBase {
    get OtherVariant() { return 'ExampleGemsparkBlockOn'; }
}
