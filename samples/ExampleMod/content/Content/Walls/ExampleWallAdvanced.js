const { DustID, WallID } = Terraria.ID;
const { Main } = Terraria;

export class ExampleWallAdvanced extends ModWall {
    SetStaticDefaults() {
        Main.wallHouse[this.Type] = true;
        Main.wallBlend[this.Type] = WallID.CogWall;

        this.DustType = DustID.Stone;

        this.AddMapEntry(Color.new(68, 68, 68));
    }

    CreateDust(i, j, type) {
        type.value = this.DustType;
        const tile = Main.tile['Tile get_Item(int x, int y)'](i, j);
        if (tile['byte wallFrameNumber()']() === 0) type.value = DustID.GemEmerald;
        return true;
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 3 : 10;
    }

    WallFrame(i, j, randomizeFrame, style, frameNumber) {
        if (randomizeFrame && frameNumber.value === 0 && Math.random() < 0.75) {
            frameNumber.value = 1 + Math.floor(Math.random() * 2);
        }
        return true;
    }

    AnimateWall(frame, frameCounter) {
        if (++frameCounter.value >= 5) {
            frameCounter.value = 0;
            frame.value = (frame.value + 1) % 2;
        }
    }

    ModifyLight(i, j, r, g, b) {
        if (!Main.dayTime) {
            r.value = 0.1;
            g.value = 0.5;
            b.value = 0;
        }
    }
}
