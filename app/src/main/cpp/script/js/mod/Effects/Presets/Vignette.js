// Bordas escurecidas (ou na cor do efeito), como uma lente.
//   ScreenPresets.Vignette.Spawn(null, { strength: 0.7, color: '#300000', key: 'mymod:lowhp' });
class Vignette extends ModScreenEffect {
    static Source = `
#include <bl/screen>
#include <bl/mask>
uniform float uStrength;

void main() {
    float m = clamp(blMask(vUV) * uIntensity * uStrength, 0.0, 1.0);
    if (m <= 0.0) discard;

    vec3 base = blScreen(vUV).rgb;
    fragColor = vec4(mix(base, uColor.rgb, m * uColor.a), 1.0);
}
`;

    Strength = 0.6;
    Start = 0.45;      // distância do centro em que começa (0 a 1)
    Softness = 0.55;   // largura da transição
    Roundness = 1;     // 1 redonda na tela, 0 seguindo o retângulo

    SetDefaults() {
        this.Fullscreen = true;
        this.Color = [0, 0, 0, 1];
    }

    Draw(cmd) {
        cmd.DrawShader(this.Shader, {
            mask: ScreenMask.Vignette({ start: this.Start, softness: this.Softness, roundness: this.Roundness }),
            uniforms: { uStrength: this.Strength },
        });
    }
}
