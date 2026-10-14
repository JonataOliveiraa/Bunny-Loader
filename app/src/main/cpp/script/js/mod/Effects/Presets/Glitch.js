// Falha de sinal: faixas da imagem pulam para o lado e as cores se separam,
// trocando Rate vezes por segundo. Na tela inteira ou na máscara.
//   ScreenPresets.Glitch.Spawn(null, { strength: 0.7, duration: 30, fadeOut: 10 });
class Glitch extends ModScreenEffect {
    static Source = `
#include <bl/screen>
#include <bl/noise>
#include <bl/mask>
uniform float uStrength;
uniform float uSlices;
uniform float uRate;
uniform float uSplit;
uniform float uShift;

void main() {
    float m = clamp(blMask(vUV) * uIntensity * uStrength, 0.0, 1.0);
    if (m <= 0.0) discard;

    float t = floor(uTime * uRate);
    float row = floor(vUV.y * uSlices * mix(0.5, 1.5, blHash(vec2(t, 1.7))));
    float pick = blHash(vec2(row, t));
    float shift = pick > 1.0 - 0.4 * m ? (blHash(vec2(row, t + 7.1)) - 0.5) * 2.0 * uShift * m : 0.0;
    vec2 uv = vec2(vUV.x + shift, vUV.y);

    vec2 split = vec2((blHash(vec2(t, 3.3)) - 0.5) * 2.0 * uSplit * m, 0.0);
    fragColor = vec4(blScreen(uv + split).r, blScreen(uv).g, blScreen(uv - split).b, 1.0);
}
`;

    Strength = 0.6;   // 0 a 1
    Slices = 24;      // faixas por altura da tela
    Rate = 14;        // trocas por segundo
    Split = 4;        // separação das cores, em pixels do mundo
    Shift = 0.06;     // pulo máximo das faixas, em fração da largura

    SetDefaults() {
        this.Fullscreen = true;
    }

    Draw(cmd) {
        cmd.DrawShader(this.Shader, {
            uniforms: {
                uStrength: this.Strength,
                uSlices: this.Slices,
                uRate: this.Rate,
                uSplit: ScreenSpace.PixelsToUV(this.Split) / ScreenSpace.Aspect,
                uShift: this.Shift,
            },
        });
    }
}
