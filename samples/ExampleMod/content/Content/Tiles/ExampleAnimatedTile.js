const { SoundID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

const FLIP = 1;   // SpriteEffects.FlipHorizontally

// Pote de vaga-lume 1x2 pendurado: cada coluna anima fora de passo com as
// vizinhas (6 quadros lado a lado, 18 px cada). No tModLoader ele balança ao
// vento (MultiTileVine); aqui fica parado.
export class ExampleAnimatedTile extends ModTile {
    static AnimationFrameWidth = 18;

    SetStaticDefaults() {
        Main.tileLighted[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.MultiTileSway[this.Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2Top);
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(238, 145, 105), this.CreateMapEntryName());
    }

    ModifyLight(i, j, r, g, b) {
        r.value = 0.93;
        g.value = 0.11;
        b.value = 0.12;
    }

    SetSpriteEffects(i, j, spriteEffects) {
        if (i % 2 === 1) spriteEffects.value = FLIP;
    }

    AnimateIndividualTile(type, i, j, frameXOffset, frameYOffset) {
        let frame = Main.tileFrame[this.Type] + i;
        if (i % 2 === 0) frame += 3;
        if (i % 3 === 0) frame += 3;
        if (i % 4 === 0) frame += 3;
        frameXOffset.value = (frame % 6) * ExampleAnimatedTile.AnimationFrameWidth;
    }

    KillSound(i, j, fail) {
        if (fail) return true;

        SoundEngine.PlaySound(SoundID.Shatter, Vector2.new(i * 16 + 8, j * 16 + 8));
        return false;
    }

    // O mesmo passo do pote de vaga-lume do jogo.
    AnimateTile(frame, frameCounter) {
        frame.value = Main.tileFrame[TileID.FireflyinaBottle];
    }
}
