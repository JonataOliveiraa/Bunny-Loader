const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { AnchorType } = Terraria.Enums;
const Main = Terraria.Main;
const Sign = Terraria.Sign;

// Placa 2x2: no chão, no teto, nas paredes laterais ou na de fundo. Nasce
// com um texto; tocar manda o texto no chat.
export class ExampleSign extends ModTile {
    static DefaultSignText = 'Example Mod sign';

    SetStaticDefaults() {
        Main.tileSign[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.FramesOnKillWall[this.Type] = true;
        TileID.Sets.AvoidedByNPCs[this.Type] = true;
        TileID.Sets.TileInteractRead[this.Type] = true;
        TileID.Sets.InteractableByNPCs[this.Type] = true;

        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style2x2);
        tile.StyleHorizontal = true;
        tile.StyleMultiplier = 5;   // cada estilo tem 5 formas de colocar
        tile.AnchorBottom = AnchorData.Empty;

        const side = () => AnchorData.new(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);
        const alternate = (index, origin, anchor) => {
            const alt = TileObjectData.newAlternate;
            alt.CopyFrom(tile);
            alt.Origin = origin;
            if (anchor) alt[anchor] = side();
            else alt.AnchorWall = true;
            TileObjectData.addAlternate(index);
        };
        alternate(1, Point16.Zero, 'AnchorTop');
        alternate(2, Point16.Zero, 'AnchorLeft');
        alternate(3, Point16.new(1, 0), 'AnchorRight');
        alternate(4, Point16.Zero, null);

        tile.AnchorBottom = AnchorData.new(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide, 2, 0);
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(200, 200, 200), this.CreateMapEntryName());
    }

    PlaceInWorld(i, j, item) {
        const id = Sign['int ReadSign(int i, int j, bool CreateIfMissing)'](i, j, true);
        if (id !== -1) Sign.TextSign(id, ExampleSign.DefaultSignText);
    }

    RightClick(i, j) {
        const id = Sign['int ReadSign(int i, int j, bool CreateIfMissing)'](i, j, false);
        if (id !== -1) Main['void NewText(string newText, byte R, byte G, byte B)'](Main.sign[id].text, 255, 255, 255);
        return true;
    }

    KillMultiTile(i, j, frameX, frameY) {
        Sign.KillSign(i, j);
    }
}
