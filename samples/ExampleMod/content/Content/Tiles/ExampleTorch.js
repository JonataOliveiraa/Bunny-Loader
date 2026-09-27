const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { LiquidPlacement } = Terraria.Enums;
const Main = Terraria.Main;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const solidAt = (i, j) => Terraria.WorldGen['bool SolidTile(int i, int j, bool noDoors)'](i, j, false);
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

// Tocha: a forma (no chão, na parede, nos lados) é a da tocha do jogo. O
// estilo 1 (a segunda linha da textura) é a de água: acende molhada.
// A chama é a da tocha do jogo (Main.tileFlame), desenhada pelo próprio jogo:
// a do tModLoader (PostDraw com 7 desenhos por tocha) custava ~16 ms por
// quadro com 150 tochas na tela.
export class ExampleTorch extends ModTile {
    SetStaticDefaults() {
        Main.tileLighted[this.Type] = true;
        Main.tileFlame[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;
        Main.tileSolid[this.Type] = false;
        Main.tileNoAttach[this.Type] = true;
        Main.tileNoFail[this.Type] = true;
        Main.tileWaterDeath[this.Type] = true;
        TileID.Sets.FramesOnKillWall[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.DisableSmartInteract[this.Type] = true;
        TileID.Sets.Torches[this.Type] = true;

        this.DustType = DustID.WhiteTorch;
        this.AdjTiles = [TileID.Torches];
        TileID.Sets.RoomNeeds.CountsAsTorch[this.Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](TileID.Torches, 0, 0));

        // A variante de água (estilo 1, a segunda linha da textura): acende molhada.
        const water = TileObjectData.newSubTile;
        water.CopyFrom(TileObjectData.newTile);
        water.LinkedAlternates = true;
        water.WaterDeath = false;
        water.LavaDeath = false;
        water.WaterPlacement = LiquidPlacement.Allowed;
        water.LavaPlacement = LiquidPlacement.Allowed;
        TileObjectData['void addSubTile(int style)'](1);
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(200, 200, 200), 'Torch');
    }

    MouseOver(i, j) {
        const player = Main.LocalPlayer;
        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType('ExampleTorch');
    }

    NumDust(i, j, fail, num) {
        num.value = Rand.Next(1, 3);
    }

    // Acesa (frameX < 66): branca; a de água, verde.
    ModifyLight(i, j, r, g, b) {
        const tile = tileAt(i, j);
        if (tile.frameX >= 66) return;

        const style = Math.floor(tile.frameY / 22);   // uma linha de 22 px por estilo
        if (style === 0) {
            r.value = 0.9;
            g.value = 0.9;
            b.value = 0.9;
        } else if (style === 1) {
            r.value = 0.5;
            g.value = 1.5;
            b.value = 0.5;
        }
    }

    // Presa no teto, desce 4 px.
    SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY) {
        offsetY.value = solidAt(i, j - 1) ? 4 : 0;
    }

    // Só os parâmetros usados: o carregador não busca o Tile nem a luz que
    // ninguém lê (cada um é uma ida à ponte por tocha, por quadro).
    EmitParticles(i, j, tile, tileFrameX) {
        // 1 em 40 sorteado no JS: cada Rand do jogo é uma ida à ponte, por tocha, por quadro.
        if (tileFrameX >= 66 || Math.random() >= 1 / 40) return;

        const dust = Main.dust[NewDust(Vector2.new(i * 16 + 4, j * 16), 4, 4, this.DustType, 0, 0, 100, Color.White, 1)];
        if (!Rand.NextBool(3)) dust.noGravity = true;
        dust.velocity = Vector2.new(dust.velocity.X * 0.3, dust.velocity.Y * 0.3 - 1.5);
    }
}
