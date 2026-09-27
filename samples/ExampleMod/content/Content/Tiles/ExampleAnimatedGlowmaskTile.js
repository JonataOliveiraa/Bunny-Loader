const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const Draw = (sb, ...args) => sb['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](...args);

// Monólito 2x3 animado que se desenha sozinho no PreDraw: o tile com a luz do
// lugar e, por cima, a máscara de brilho (_Glow) sempre acesa.
export class ExampleAnimatedGlowmaskTile extends ModTile {
    AnimationFrameHeight = 56;
    glowTexture = null;

    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;

        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style2xX);
        tile.Height = 3;
        tile.Origin = Point16.new(1, 2);
        tile.CoordinateHeights = [16, 16, 18];
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(75, 139, 166));
        this.glowTexture = ModContent.Request(this.Texture + '_Glow');
    }

    AnimateTile(frame, frameCounter) {
        frame.value = Main.tileFrame[TileID.LunarMonolith];
    }

    PreDraw(i, j, spriteBatch) {
        const tile = tileAt(i, j);
        const height = tile.frameY % this.AnimationFrameHeight === 36 ? 18 : 16;
        const frameYOffset = (Main.tileFrame[this.Type] + 1) * this.AnimationFrameHeight;
        const sp = Main.screenPosition;
        const at = Vector2.new(i * 16 - Math.floor(sp.X), j * 16 - Math.floor(sp.Y));
        const source = Rectangle.new(tile.frameX, tile.frameY + frameYOffset, 16, height);

        const texture = Terraria.GameContent.TextureAssets.Tile[this.Type].Value;
        Draw(spriteBatch, texture, at, source, Terraria.Lighting['Color GetColor(int x, int y)'](i, j), 0, Vector2.new(0, 0), 1, 0, 0);
        Draw(spriteBatch, this.glowTexture.Value, at, source, Color.White, 0, Vector2.new(0, 0), 1, 0, 0);
        return false;
    }
}
