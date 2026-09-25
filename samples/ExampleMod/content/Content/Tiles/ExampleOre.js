const { DustID, SoundID, TileID } = Terraria.ID;

export class ExampleOre extends ModTile {
    constructor() {
        super();
        this.Texture = 'Tiles/' + this.constructor.name;
        this.DustType = DustID.Platinum;
        this.HitSound = SoundID.Tink;
        this.MineResist = 4;
        this.MinPick = 200;
    }

    SetStaticDefaults() {
        const Main = Terraria.Main;
        Main.tileSolid[this.Type] = true;
        Main.tileBlockLight[this.Type] = true;
        Main.tileMergeDirt[this.Type] = true;
        Main.tileMerge[this.Type][this.Type] = true;
        Main.tileOreFinderPriority[this.Type] = 1000;
        Main.tileShine[this.Type] = 975;
        Main.tileShine2[this.Type] = true;
        Main.tileSpelunker[this.Type] = true;

        TileID.Sets.Ore[this.Type] = true;
        TileID.Sets.FriendlyFairyCanLureTo[this.Type] = true;

        this.AddMapEntry(Color.new(152, 171, 198), this.constructor.name);
    }
}
