// O desenho igual ao do jogo: um UIPanel/UIText do próprio jogo recebe o
// retângulo do elemento JS e desenha com o DrawSelf dele (as mesmas texturas,
// cantos e fontes). Só o desenho: layout e toque são do JS.
class UINative {
    static #DRAW_SELF = 'void DrawSelf(SpriteBatch spriteBatch)';

    static #Set(view, s) {
        view.X = s.X;
        view.Y = s.Y;
        view.Width = s.Width;
        view.Height = s.Height;
    }

    // O retângulo do elemento JS no nativo (as três medidas do UIElement).
    static Place(native, element) {
        UINative.#Set(native._dimensions, element.GetDimensions());
        UINative.#Set(native._innerDimensions, element.GetInnerDimensions());
        UINative.#Set(native._outerDimensions, element.GetOuterDimensions());
    }

    static DrawSelf(native, spriteBatch) {
        native[UINative.#DRAW_SELF](spriteBatch);
    }

    // Asset<Texture2D> ou Texture2D -> Texture2D. O asset do jogo pode chegar
    // ainda sem o Value (o celular carrega na hora do uso): bl.textureSize carrega.
    static Texture(texture) {
        if (!texture) return null;
        if (!bl.isTextureAsset(texture)) return texture;
        let value = texture.Value;
        if (!value) {
            bl.textureSize(texture);
            value = texture.Value;
        }
        return value;
    }

    static Width(texture) {
        return texture ? bl.textureSize(texture).Width : 0;
    }

    static Height(texture) {
        return texture ? bl.textureSize(texture).Height : 0;
    }
}
