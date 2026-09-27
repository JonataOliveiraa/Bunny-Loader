const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

// Troféu 3x3 de parede.
export class MinionBossTrophy extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.FramesOnKillWall[this.Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3Wall);
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(120, 85, 60), 'Trophy');
        this.DustType = DustID.WoodFurniture;
    }
}
