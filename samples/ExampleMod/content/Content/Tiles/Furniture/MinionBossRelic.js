const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { TileObjectDirection } = Terraria.Enums;
const Main = Terraria.Main;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const Draw = (sb, ...args) => sb['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](...args);

// Relíquia 3x4: o tile é só o pedestal; a relíquia flutua por cima, desenhada
// no SpecialDraw (pedido pelo DrawEffects na célula de cima à esquerda).
export class MinionBossRelic extends ModTile {
    static FrameWidth = 18 * 3;
    static FrameHeight = 18 * 4;
    static HorizontalFrames = 1;
    static VerticalFrames = 1;

    Texture = 'Content/Tiles/Furniture/RelicPedestal';
    RelicTexture = null;

    SetStaticDefaults() {
        Main.tileShine[this.Type] = 400;
        Main.tileFrameImportant[this.Type] = true;
        TileID.Sets.InteractableByNPCs[this.Type] = true;

        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style3x4);
        tile.LavaDeath = false;
        tile.DrawYOffset = 2;
        tile.Direction = TileObjectDirection.PlaceLeft;
        tile.StyleHorizontal = false;
        tile.StyleWrapLimitVisualOverride = 2;
        tile.StyleMultiplier = 2;
        tile.StyleWrapLimit = 2;
        tile.styleLineSkipVisualOverride = 0;

        TileObjectData.newAlternate.CopyFrom(tile);
        TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
        TileObjectData.addAlternate(1);
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(233, 207, 94), 'MapObject.Relic');
        this.RelicTexture = ModContent.Request('Content/Tiles/Furniture/MinionBossRelic');
    }

    CreateDust(i, j, type) {
        return false;
    }

    SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY) {
        tileFrameX.value %= MinionBossRelic.FrameWidth;
        tileFrameY.value %= MinionBossRelic.FrameHeight * 2;
    }

    DrawEffects(i, j, spriteBatch, drawData) {
        if (drawData.tileFrameX % MinionBossRelic.FrameWidth === 0 && drawData.tileFrameY % MinionBossRelic.FrameHeight === 0) {
            Main.instance.TilesRenderer['void AddSpecialLegacyPoint(int x, int y)'](i, j);
        }
    }

    SpecialDraw(i, j, spriteBatch) {
        if (bl.tiles.typeAt(i, j) < 0) return;

        const tile = tileAt(i, j);
        const texture = this.RelicTexture.Value;
        const frameHeight = texture.Height / MinionBossRelic.VerticalFrames;
        const frameY = Math.floor(tile.frameX / MinionBossRelic.FrameWidth);
        const frame = Rectangle.new(0, frameY * frameHeight, texture.Width / MinionBossRelic.HorizontalFrames, frameHeight);
        const origin = Vector2.new(frame.Width / 2, frame.Height / 2);
        const color = Terraria.Lighting['Color GetColor(int x, int y)'](i, j);

        // Virada para a direita: a segunda coluna de alternativas.
        const effects = Math.floor(tile.frameY / MinionBossRelic.FrameHeight) !== 0 ? 1 : 0;

        const TwoPi = Math.PI * 2;
        const time = Main.GlobalTimeWrappedHourly;
        const bob = Math.sin(time * TwoPi / 5);
        const sp = Main.screenPosition;
        const at = Vector2.new(i * 16 + 24 - sp.X, j * 16 + 64 - sp.Y - 40 + bob * 4);
        Draw(spriteBatch, texture, at, frame, color, 0, origin, 1, effects, 0);

        // O brilho pulsando em volta.
        const pulse = Math.sin(time * TwoPi / 2) * 0.3 + 0.7;
        const glow = Color.Multiply(color, 0.1 * pulse);
        glow.A = 0;
        for (let k = 0; k < 1; k += 355 / (678 * Math.PI)) {
            const angle = TwoPi * k;
            const around = Vector2.new(at.X + Math.cos(angle) * (6 + bob * 2), at.Y + Math.sin(angle) * (6 + bob * 2));
            Draw(spriteBatch, texture, around, frame, glow, 0, origin, 1, effects, 0);
        }
    }
}
