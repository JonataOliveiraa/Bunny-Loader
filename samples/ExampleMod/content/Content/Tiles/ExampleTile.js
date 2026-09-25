const { DustID, TileID } = Terraria.ID;

export class ExampleTile extends ModTile {
    constructor() {
        super();
        this.Texture = 'Tiles/' + this.constructor.name;
        this.DustType = DustID.Stone;
    }

    SetStaticDefaults() {
        Terraria.Main.tileSolid[this.Type] = true;
        Terraria.Main.tileBlockLight[this.Type] = true;
        Terraria.Main.tileMergeDirt[this.Type] = true;
        TileID.Sets.ChecksForMerge[this.Type] = true;

        this.AddMapEntry(Color.new(0, 200, 255), this.constructor.name);
    }
}
