// Mundo <-> tela para os efeitos. Coordenadas de tela em fração do viewport
// da câmera do mundo (0..1, Y de baixo para cima, como no GLSL); tamanhos em
// fração da ALTURA. Tudo passa pela tela virtual (Main.screenWidth/Height),
// que é o que o zoom do celular muda: um raio em pixels do mundo acompanha o
// zoom sozinho.
class ScreenSpace {
    static #frame = -1;
    static #x = 0;
    static #y = 0;
    static #width = 1;
    static #height = 1;

    // Lê a câmera uma vez por quadro (ScreenEffects chama antes dos efeitos).
    static BeginFrame(frame) {
        const Main = Terraria.Main;
        const position = Main.screenPosition;
        ScreenSpace.#frame = frame;
        ScreenSpace.#x = position.X;
        ScreenSpace.#y = position.Y;
        ScreenSpace.#width = Main.screenWidth || 1;
        ScreenSpace.#height = Main.screenHeight || 1;
    }

    static #Fresh() {
        if (ScreenSpace.#frame !== ScreenEffects.Frame) ScreenSpace.BeginFrame(ScreenEffects.Frame);
    }

    static WorldToUV(position) {
        ScreenSpace.#Fresh();
        return {
            X: (position.X - ScreenSpace.#x) / ScreenSpace.#width,
            Y: 1 - (position.Y - ScreenSpace.#y) / ScreenSpace.#height,
        };
    }

    static UVToWorld(uv) {
        ScreenSpace.#Fresh();
        return {
            X: ScreenSpace.#x + uv.X * ScreenSpace.#width,
            Y: ScreenSpace.#y + (1 - uv.Y) * ScreenSpace.#height,
        };
    }

    // Pixels do mundo -> fração da altura da tela (o uRadius dos shaders).
    static PixelsToUV(pixels) {
        ScreenSpace.#Fresh();
        return pixels / ScreenSpace.#height;
    }

    static UVToPixels(fraction) {
        ScreenSpace.#Fresh();
        return fraction * ScreenSpace.#height;
    }

    static get Aspect() {
        ScreenSpace.#Fresh();
        return ScreenSpace.#width / ScreenSpace.#height;
    }

    // O tamanho da tela virtual em pixels do mundo ({ X, Y }).
    static get Size() {
        ScreenSpace.#Fresh();
        return { X: ScreenSpace.#width, Y: ScreenSpace.#height };
    }

    static IsOnScreen(position, margin = 0) {
        ScreenSpace.#Fresh();
        const x = position.X - ScreenSpace.#x, y = position.Y - ScreenSpace.#y;
        return x >= -margin && y >= -margin && x <= ScreenSpace.#width + margin && y <= ScreenSpace.#height + margin;
    }
}
