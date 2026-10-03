const { DustID, WallID } = Terraria.ID;
const { Main } = Terraria;

export class ExampleWall extends ModWall {
    SetStaticDefaults() {
        Main.wallHouse[this.Type] = true;

        this.DustType = DustID.Stone;
        this.VanillaFallbackOnModDeletion = WallID.DiamondGemspark;

        this.AddMapEntry(Color.new(150, 150, 150));
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }
}
