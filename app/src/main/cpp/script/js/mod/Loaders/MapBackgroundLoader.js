// O fundo do mapa em tela cheia, como o DrawMapFullscreenBackground do
// tModLoader: com um MapBackground na cena do jogador (o caminho de uma
// textura em Assets/Textures do mod dono do efeito), ela cobre a tela no lugar
// do fundo do jogo. A cor é a do céu (Main.ColorOfTheSkies) com a tela na
// superfície e branca abaixo dela; MapBackgroundFullbright a deixa sempre
// branca, e MapBackgroundColor(color) muda o que sobrar.
// O celular desenha o mapa pelo GUIMap (o do Main, estático, é o do PC):
// os dois ficam com o hook.
class MapBackgroundLoader {
    static #installed = false;
    static #assets = new Map();        // arquivo -> Asset<Texture2D>, ou null (falhou)
    static #draw = null;

    static Install() {
        if (MapBackgroundLoader.#installed) return;
        MapBackgroundLoader.#installed = true;

        const hook = (original, self, screenPosition, screenWidth, screenHeight) => {
            const drawn = Safe.Run('fundo do mapa', () => MapBackgroundLoader.#Draw(screenPosition, screenWidth, screenHeight));
            if (drawn !== true) return original(self, screenPosition, screenWidth, screenHeight);
        };
        GUIMap['void DrawMapFullscreenBackground(Vector2 screenPosition, int screenWidth, int screenHeight)'].hook(hook);
        Terraria.Main['void DrawMapFullscreenBackground(Vector2 screenPosition, int screenWidth, int screenHeight)'].hook(
            (original, screenPosition, screenWidth, screenHeight) =>
                hook((self, p, w, h) => original(p, w, h), null, screenPosition, screenWidth, screenHeight));
    }

    static Texture(effect, path) {
        const mod = effect.Mod;
        if (!mod || typeof path !== 'string' || path === '') return null;
        const rel = ModFiles.Texture(ModFiles.TextureName(path), mod);
        const file = mod.path + '/' + rel;

        let asset = MapBackgroundLoader.#assets.get(file);
        if (asset === undefined) {
            asset = null;
            try {
                asset = bl.loadTextureAsset(file);
            } catch (e) {
                bl.log(`fundo do mapa de ${effect.constructor.name}: sem ${rel} (${e})`);
            }
            MapBackgroundLoader.#assets.set(file, asset);
        }
        return asset ? asset.Value : null;
    }

    static #Draw(screenPosition, screenWidth, screenHeight) {
        const Main = Terraria.Main;
        const scene = SceneEffectLoader.Of(Main.LocalPlayer).mapBackground;
        if (!scene || !scene.from) return false;

        const effect = scene.from;
        const texture = MapBackgroundLoader.Texture(effect, scene.value);
        if (!texture) return false;

        const name = effect.constructor.name;
        const fullbright = Safe.Run(name + '.MapBackgroundFullbright', () => effect.MapBackgroundFullbright) === true;
        const color = new Ref(!fullbright && screenPosition.Y <= Main.worldSurface * 16 ? Main.ColorOfTheSkies : Color.White);
        Safe.Run(name + '.MapBackgroundColor', () => effect.MapBackgroundColor(color));

        const draw = MapBackgroundLoader.#draw || (MapBackgroundLoader.#draw =
            Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Rectangle destinationRectangle, Color color)']);
        draw(Main.spriteBatch, texture, Rectangle.new(0, 0, screenWidth, screenHeight), color.value);
        return true;
    }
}
