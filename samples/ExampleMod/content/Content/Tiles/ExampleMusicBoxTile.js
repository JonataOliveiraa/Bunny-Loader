const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

const NewGore = Terraria.Gore['int NewGore(Vector2 Position, Vector2 Velocity, int Type, float Scale)'];

// Caixa de música 2x2. Tocar (ou o fio) liga e desliga, como a do jogo, e
// ligada toca a faixa do MusicLoader.AddMusicBox (no item) e solta notas.
export class ExampleMusicBoxTile extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileObsidianKill[this.Type] = true;
        TileID.Sets.HasOutlines[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;

        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style2x2);
        tile.Origin = Point16.new(0, 1);
        tile.LavaDeath = false;
        tile.DrawYOffset = 2;
        tile.StyleLineSkip = 2;
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(191, 142, 111), 'Music Box');
    }

    MouseOver(i, j) {
        const player = Main.LocalPlayer;
        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType('ExampleMusicBox');
    }

    // Ligada (frameX 36), da célula de cima: uma nota de vez em quando.
    EmitParticles(i, j, tile, tileFrameX, tileFrameY, tileLight, visible) {
        if (!visible || tileFrameX !== 36 || tileFrameY % 36 !== 0) return;
        if (Math.floor(Main.timeForVisualEffects) % 7 !== 0 || !Rand.NextBool(3)) return;

        const note = Rand.Next(570, 573);
        const x = i * 16 + 8 - (note === 572 ? 8 : note === 571 ? 4 : 0);
        const velocity = Vector2.new(Main.WindForVisuals * 2 * Rand.NextFloat(0.5, 1.5), -0.5 * Rand.NextFloat(0.5, 1.5));
        NewGore(Vector2.new(x, j * 16 - 8), velocity, note, 0.8);
    }
}
