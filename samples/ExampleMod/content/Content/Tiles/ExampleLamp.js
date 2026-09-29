const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { LiquidPlacement } = Terraria.Enums;
const Main = Terraria.Main;
const Wiring = Terraria.Wiring;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
const Draw = (sb, ...args) => sb['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](...args);
const FLIP = 1;   // SpriteEffects.FlipHorizontally

// A semente de quadro das chamas (a mesma conta da tocha de exemplo).
function flameRandom(i, j) {
    let s = (Number(Main.TileFrameSeed) ^ Math.imul(i, 374761393) ^ Math.imul(j, 668265263)) >>> 0 || 1;
    return (min, max) => {
        s ^= s << 13; s >>>= 0;
        s ^= s >>> 17;
        s ^= s << 5; s >>>= 0;
        return min + (s % (max - min));
    };
}

// Luminária de chão 1x3. O fio liga e desliga (a coluna ao lado na textura é
// a apagada); as de coluna par ficam espelhadas. A chama sai no SpecialDraw.
export class ExampleLamp extends ModTile {
    flameTexture = null;

    SetStaticDefaults() {
        Main.tileLighted[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileWaterDeath[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.Wiring.IsAMechanism[this.Type] = true;
        TileID.Sets.Wiring.IgnoreWhenValidatingTraps[this.Type] = true;

        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style1xX);
        tile.DrawFlipHorizontal = true;
        tile.StyleLineSkip = 2;
        tile.DrawYOffset = 2;
        tile.WaterDeath = true;
        tile.WaterPlacement = LiquidPlacement.NotAllowed;
        tile.LavaPlacement = LiquidPlacement.NotAllowed;
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(253, 221, 3), 'MapObject.FloorLamp');
        this.flameTexture = ModContent.Request(this.Texture + '_Flame');
    }

    HitWire(i, j) {
        const tile = tileAt(i, j);
        const top = j - Math.floor(tile.frameY / 18) % 3;
        const shift = tile.frameX > 0 ? -18 : 18;

        for (let y = top; y < top + 3; y++) {
            const cell = tileAt(i, y);
            cell.frameX = cell.frameX + shift;
            Wiring['void SkipWire(int x, int y)'](i, y);
        }
        if (Main.netMode !== NetmodeID.SinglePlayer) {
            Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int centeredSquareSize, TileChangeType changeType)'](-1, i, top + 1, 3, 0);
        }
    }

    SetSpriteEffects(i, j, spriteEffects) {
        if (i % 2 === 0) spriteEffects.value = FLIP;
    }

    ModifyLight(i, j, r, g, b) {
        if (tileAt(i, j).frameX !== 0) return;

        r.value = 1;
        g.value = 0.75;
        b.value = 1;
    }

    EmitParticles(i, j, tile, tileFrameX, tileFrameY, tileLight, visible) {
        if (!visible || tileFrameX !== 0 || Math.random() >= 1 / 40) return;
        if (Math.floor(tileFrameY / 18) % 3 !== 0 || Math.floor(tileFrameY / 54) !== 0) return;

        const dust = Main.dust[NewDust(Vector2.new(i * 16 + 4, j * 16 + 2), 4, 4, 21, 0, 0, 100, Color.White, 1)];
        if (!Rand.NextBool(3)) dust.noGravity = true;
        dust.velocity = Vector2.new(dust.velocity.X * 0.3, dust.velocity.Y * 0.3 - 1.5);
    }

    // Só a célula de cima, acesa, pede a chama.
    DrawEffects(i, j, spriteBatch, drawData) {
        if (drawData.tileFrameX === 0 && Math.floor(drawData.tileFrameY / 18) % 3 === 0) {
            Main.instance.TilesRenderer['void AddSpecialLegacyPoint(int x, int y)'](i, j);
        }
    }

    SpecialDraw(i, j, spriteBatch) {
        const tile = tileAt(i, j);
        const random = flameRandom(i, j);
        const effects = i % 2 === 0 ? FLIP : 0;
        const width = 16, height = 16, offsetY = 2;
        const sp = Main.screenPosition;
        const source = Rectangle.new(tile.frameX, tile.frameY, width, height);

        for (let c = 0; c < 7; c++) {
            const shakeX = random(-10, 11) * 0.15;
            const shakeY = random(-10, 1) * 0.35;
            const at = Vector2.new(i * 16 - Math.floor(sp.X) - (width - 16) / 2 + shakeX, j * 16 - Math.floor(sp.Y) + offsetY + shakeY);
            Draw(spriteBatch, this.flameTexture.Value, at, source, Color.new(100, 100, 100, 0), 0, Vector2.new(0, 0), 1, effects, 0);
        }
    }
}
