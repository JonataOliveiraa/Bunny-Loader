// Desfoque de velocidade: a imagem se arrasta para fora do centro (linhas de
// velocidade de uma investida). O miolo, até Clear, fica nítido.
//   ScreenPresets.ZoomBlur.Spawn(player, { strength: 0.12, duration: 20, fadeOut: 15 });
class ZoomBlur extends ModScreenEffect {
    static Source = `
#include <bl/screen>
uniform float uStrength;
uniform float uClear;
uniform float uSamples;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    float m = smoothstep(uClear, uClear * 2.0 + 0.05, r) * clamp(uIntensity, 0.0, 1.0);
    if (m <= 0.0) discard;

    vec3 sum = vec3(0.0);
    float n = 0.0;
    for (int i = 0; i < 24; i++) {
        if (float(i) >= uSamples) break;
        float k = 1.0 - uStrength * m * float(i) / uSamples;
        sum += blScreen(blAtCenter(d * k)).rgb;
        n += 1.0;
    }
    fragColor = vec4(sum / n, 1.0);
}
`;

    Strength = 0.1;   // o quanto arrasta, em fração da distância ao centro
    Clear = 60;       // raio nítido em volta do centro, em pixels do mundo
    Samples = 12;     // amostras por pixel (até 24)

    SetDefaults() {
        this.Fullscreen = true;
    }

    Draw(cmd) {
        cmd.DrawShader(this.Shader, {
            uniforms: {
                uStrength: this.Strength,
                uClear: ScreenSpace.PixelsToUV(this.Clear),
                uSamples: Math.min(Math.max(Math.round(this.Samples), 2), 24),
            },
        });
    }
}
