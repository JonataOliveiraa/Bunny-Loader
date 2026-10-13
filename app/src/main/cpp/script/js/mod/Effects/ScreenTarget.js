// Uma textura para os passos de um efeito: um alvo fora da tela (cmd.Target)
// ou uma das fontes fixas (ScreenGraphics.Sources.Screen/History). `Texture` é
// o número que o núcleo entende: > 0 um alvo, -1 a tela, -2 o quadro anterior.
// Todo alvo cobre o viewport inteiro (na escala dele): no GLSL,
// `texture(uAlgo, vUV)` lê o mesmo lugar da tela.
class ScreenTarget {
    Texture;
    Name;
    Scale;
    Persistent;

    constructor(texture, name, scale = 1, persistent = false) {
        this.Texture = texture;
        this.Name = name;
        this.Scale = scale;
        this.Persistent = persistent;
    }

    static Screen = Object.freeze(new ScreenTarget(-1, 'Screen'));
    static History = Object.freeze(new ScreenTarget(-2, 'History'));
}
