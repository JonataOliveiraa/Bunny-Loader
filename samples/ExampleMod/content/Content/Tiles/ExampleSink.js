const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;

// Um móvel 2x2: o tile ocupa quatro células, e a forma dele (tamanho, onde se
// apoia, a altura de cada linha da textura) vem do TileObjectData.
export class ExampleSink extends ModTile {
    constructor() {
        super();
        this.DustType = DustID.Platinum;
    }

    SetStaticDefaults() {
        // Conta como água na hora de criar (as receitas que pedem água).
        TileID.Sets.CountsAsWaterForCrafting[this.Type] = true;

        Terraria.Main.tileSolid[this.Type] = false;
        Terraria.Main.tileLavaDeath[this.Type] = false;
        Terraria.Main.tileFrameImportant[this.Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(100, 100, 100), this.constructor.name);
    }
}
