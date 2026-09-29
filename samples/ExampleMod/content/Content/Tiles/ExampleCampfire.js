const { SoundID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;
const Wiring = Terraria.Wiring;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
const Draw = (sb, ...args) => sb['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](...args);

// Fogueira 3x2: animada (8 quadros de 36 px), liga e desliga ao tocar e no
// fio, e acesa dá o buff da fogueira (Main.SceneMetrics.HasCampfire).
export class ExampleCampfire extends ModTile {
    flameTexture = null;

    SetStaticDefaults() {
        Main.tileLighted[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;
        Main.tileWaterDeath[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.HasOutlines[this.Type] = true;
        TileID.Sets.InteractableByNPCs[this.Type] = true;
        TileID.Sets.Campfires[this.Type] = true;
        TileID.Sets.Wiring.IsAMechanism[this.Type] = true;
        TileID.Sets.Wiring.IgnoreWhenValidatingTraps[this.Type] = true;

        this.DustType = -1;   // sem pó ao quebrar
        this.AdjTiles = [TileID.Campfire];

        TileObjectData.newTile.CopyFrom(TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](TileID.Campfire, 0, 0));
        TileObjectData.newTile.StyleLineSkip = 9;
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(254, 121, 2), 'ItemName.Campfire');
        this.flameTexture = ModContent.Request(this.Texture + '_Flame');
    }

    NearbyEffects(i, j, closer) {
        if (closer) return;
        if (tileAt(i, j).frameY < 36) Main.SceneMetrics.HasCampfire = true;
    }

    MouseOver(i, j) {
        const player = Main.LocalPlayer;
        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType('ExampleCampfire');
    }

    RightClick(i, j) {
        SoundEngine.PlaySound(SoundID.Mech, Vector2.new(i * 16, j * 16));
        this.ToggleTile(i, j);
        return true;
    }

    HitWire(i, j) {
        this.ToggleTile(i, j);
    }

    // Acesa: frameY 0 e 18; apagada: 36 e 54.
    ToggleTile(i, j) {
        const tile = tileAt(i, j);
        const topX = i - Math.floor((tile.frameX % 54) / 18);
        const topY = j - Math.floor((tile.frameY % 36) / 18);
        const shift = tile.frameY >= 36 ? -36 : 36;

        for (let x = topX; x < topX + 3; x++) {
            for (let y = topY; y < topY + 2; y++) {
                const cell = tileAt(x, y);
                cell.frameY = cell.frameY + shift;
                if (Wiring.running) Wiring['void SkipWire(int x, int y)'](x, y);
            }
        }
        if (Main.netMode !== NetmodeID.SinglePlayer) {
            Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int xSize, int ySize, TileChangeType changeType)'](-1, topX, topY, 3, 2, 0);
        }
    }

    AnimateTile(frame, frameCounter) {
        if (++frameCounter.value >= 4) {
            frameCounter.value = 0;
            frame.value = (frame.value + 1) % 8;
        }
    }

    // Acesa anda pelos 8 quadros; apagada, o quadro 7 (252 px abaixo).
    AnimateIndividualTile(type, i, j, frameXOffset, frameYOffset) {
        frameYOffset.value = tileAt(i, j).frameY < 36 ? Main.tileFrame[type] * 36 : 252;
    }

    EmitParticles(i, j, tileCache, tileFrameX, tileFrameY, tileLight, visible) {
        if (tileFrameY !== 0 || Math.random() >= 1 / 3) return;

        const dust = Main.dust[NewDust(Vector2.new(i * 16 + 2, j * 16 - 4), 4, 8, 31, 0, 0, 100, Color.White, 1)];
        const position = dust.position;
        if (tileFrameX === 0) position.X += Rand.Next(8);
        if (tileFrameX === 36) position.X -= Rand.Next(8);
        dust.position = position;
        dust.alpha += Rand.Next(100);
        dust.velocity = Vector2.new(dust.velocity.X * 0.2, dust.velocity.Y * 0.2 - (0.5 + Rand.Next(10) * 0.1));
        dust.fadeIn = 0.5 + Rand.Next(10) * 0.1;
    }

    ModifyLight(i, j, r, g, b) {
        if (tileAt(i, j).frameY >= 36) return;

        const pulse = Rand.Next(28, 42) * 0.005 + (270 - Main.mouseTextColor) / 700;
        r.value = 0.1 + pulse;
        g.value = 0.9 + pulse;
        b.value = 0.3 + pulse;
    }

    PostDraw(i, j, spriteBatch) {
        const tile = tileAt(i, j);
        if (tile.frameY >= 36) return;

        const sp = Main.screenPosition;
        const addFrY = Main.tileFrame[this.Type] * 36;
        const source = Rectangle.new(tile.frameX, tile.frameY + addFrY, 16, 16);
        const at = Vector2.new(i * 16 - Math.floor(sp.X), j * 16 - Math.floor(sp.Y) + 2);
        Draw(spriteBatch, this.flameTexture.Value, at, source, Color.new(255, 255, 255, 0), 0, Vector2.new(0, 0), 1, 0, 0);
    }
}
