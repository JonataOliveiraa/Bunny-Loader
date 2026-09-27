const { TileID } = Terraria.ID;
const Main = Terraria.Main;

// Bloco sólido que não barra a luz (e brilha laranja), queima quem encosta e
// não aceita rampa.
export class ExampleTransparentShapedTile extends ModTile {
    SetStaticDefaults() {
        Main.tileSolid[this.Type] = true;
        Main.tileLighted[this.Type] = true;
        Main.tileBlockLight[this.Type] = false;
        TileID.Sets.TouchDamageImmediate[this.Type] = 30;
        TileID.Sets.TouchDamageHot[this.Type] = true;
        TileID.Sets.CanBeSloped[this.Type] = false;
        TileID.Sets.DrawsWalls[this.Type] = true;

        this.AddMapEntry(Color.Orange);
    }

    ModifyLight(i, j, r, g, b) {
        r.value = 2;
        g.value = 1.33;
        b.value = 0.4;
    }
}
