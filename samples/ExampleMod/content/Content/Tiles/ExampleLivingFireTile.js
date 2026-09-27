const { DustID, TileID } = Terraria.ID;
const Main = Terraria.Main;

// A cor da luz do fogo vivo de exemplo (o item também brilha dela no chão).
export const LivingFireLight = Object.freeze({ X: 0.7, Y: 0.8, Z: 0.8 });

// Bloco de fogo vivo: sem sólido, 4 quadros de 90 px, no mesmo passo do do jogo.
export class ExampleLivingFireTile extends ModTile {
    AnimationFrameHeight = 90;

    SetStaticDefaults() {
        Main.tileLighted[this.Type] = true;
        TileID.Sets.CanPlaceNextToNonSolidTile[this.Type] = true;

        this.DustType = DustID.WhiteTorch;
        this.AddMapEntry(Color.new(LivingFireLight.X * 255, LivingFireLight.Y * 255, LivingFireLight.Z * 255));
    }

    ModifyLight(i, j, r, g, b) {
        r.value = LivingFireLight.X;
        g.value = LivingFireLight.Y;
        b.value = LivingFireLight.Z;
    }

    SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY) {
        offsetY.value = 2;
    }

    AnimateTile(frame, frameCounter) {
        frame.value = Main.tileFrame[TileID.LivingFire];
    }
}
