// Um desenho do jogo dentro das barras de vida ou mana (um coração, uma
// estrela, um pedaço do fundo), entregue ao ModResourceOverlay.
//
// `texture` é o Asset<Texture2D> do jogo quando ele é conhecido (compare com
// TextureAssets.Heart, ou com ModContent.Request("Terraria/Images/...")), ou
// a Texture2D. Mexer nos campos e chamar Draw() desenha de novo, por cima.
class ResourceOverlayDrawContext {
    static #DRAW = 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';
    static Drawing = false;   // o loader não trata os desenhos do próprio Draw()

    snapshot = null;
    DisplaySet = null;        // { Name: 'Classic' | 'Fancy' | 'HorizontalBars', Index }
    resourceNumber = 0;       // o índice do coração ou da estrela (0 é o primeiro)
    drawingLife = true;
    texture = null;
    position = null;          // { X, Y }, na tela de UI
    source = null;            // Rectangle, ou null (a textura inteira)
    color = null;
    rotation = 0;
    origin = null;
    scale = 1;
    effects = 0;
    SpriteBatch = null;

    get Texture2D() { return UINative.Texture(this.texture); }

    Draw() {
        const texture = this.Texture2D;
        if (!texture) return;
        ResourceOverlayDrawContext.Drawing = true;
        try {
            this.SpriteBatch[ResourceOverlayDrawContext.#DRAW](texture, Vector2.new(this.position.X, this.position.Y), this.source,
                this.color, this.rotation, this.origin, this.scale, this.effects, 0);
        } finally {
            ResourceOverlayDrawContext.Drawing = false;
        }
    }
}
