// Gradação por LUT: a imagem passa por uma tabela de cores feita num programa
// de foto. Comece da neutra (ScreenGraphics.Texture.SaveNeutralLut), aplique a
// gradação nela e salve como PNG no mod.
//   ScreenPresets.ColorGrade.Spawn(null, { lut: 'Content/Effects/Hell.png', amount: 0.8, fadeIn: 60 });
class ColorGrade extends ModScreenEffect {
    static Source = `
#include <bl/screen>
#include <bl/color>
#include <bl/mask>
uniform sampler2D uLut;
uniform float uLutSize;
uniform float uAmount;

void main() {
    float m = clamp(blMask(vUV) * uIntensity * uAmount, 0.0, 1.0);
    if (m <= 0.0) discard;

    vec3 base = blScreen(vUV).rgb;
    fragColor = vec4(mix(base, blLut(uLut, base, uLutSize), m), 1.0);
}
`;

    Lut = null;    // caminho do PNG (relativo ao mod) ou ScreenGraphics.Texture
    Amount = 1;

    SetDefaults() {
        this.Fullscreen = true;
    }

    OnSpawn() {
        this.#ResolveLut();
    }

    // Também no Draw: renovado pela Key, o efeito recebe a lut nova sem OnSpawn.
    #ResolveLut() {
        if (typeof this.Lut === 'string') this.Lut = ScreenTexture.Load(this.Lut);
        if (!(this.Lut instanceof ScreenTexture)) throw new TypeError('ColorGrade: set lut (a PNG path or ScreenGraphics.Texture)');

        const size = this.Lut.Height;
        if (this.Lut.Width !== size * size) {
            throw new RangeError('ColorGrade: the LUT must be a strip of size² x size (got ' + this.Lut.Width + 'x' + size + ')');
        }
    }

    Draw(cmd) {
        if (!(this.Lut instanceof ScreenTexture)) this.#ResolveLut();
        cmd.DrawShader(this.Shader, {
            textures: { uLut: this.Lut },
            uniforms: { uLutSize: this.Lut.Height, uAmount: this.Amount },
        });
    }
}
