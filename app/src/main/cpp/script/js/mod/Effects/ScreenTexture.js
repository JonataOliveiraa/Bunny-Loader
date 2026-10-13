// Um PNG do mod para os efeitos (ruído, máscara, LUT...). Vale onde vale um
// alvo: `textures: { uNoise: tex }`, `source`, Mask.Texture. No GLSL, v = 0 é
// o pé da imagem, como o vUV.
//   const noise = ScreenGraphics.Texture.Load('Content/Effects/Noise.png', { wrap: 'repeat' });
class ScreenTexture extends ScreenTarget {
    Width;
    Height;
    Path;

    constructor(id, path, width, height) {
        super(id, path);
        this.Path = path;
        this.Width = width;
        this.Height = height;
    }

    // Caminho relativo à pasta do mod de quem chama (.png se faltar a
    // extensão). filter: 'linear' (padrão) ou 'nearest' (pixel art, LUT
    // pequena); wrap: 'clamp' (padrão) ou 'repeat' (ruído que se repete).
    static Load(path, options = {}) {
        const file = /\.(png|jpe?g|webp)$/i.test(path) ? path : path + '.png';
        const info = bl.screen.texture(file, options.filter !== 'nearest', options.wrap === 'repeat');
        if (!info) throw new Error('ScreenGraphics.Texture.Load: ' + file + ' could not be loaded');

        return new ScreenTexture(info.id, file, info.width, info.height);
    }

    // Grava a LUT neutra em tira (size² x size) para editar num programa de
    // foto: o vermelho cresce para a direita, o azul de fatia em fatia e o
    // verde para baixo (a linha de cima é verde 0, como as LUTs do ReShade).
    static SaveNeutralLut(path, size = 16) {
        if (!bl.screen.saveNeutralLut(path, size)) throw new Error('ScreenGraphics.Texture.SaveNeutralLut: could not write ' + path);
    }
}
