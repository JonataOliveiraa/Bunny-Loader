// Onda de choque: um anel que sai do centro até o Radius ao longo da
// Duration, empurrando a imagem para fora, e some no fim.
//   ScreenPresets.Shockwave.Spawn(npc.Center, { radius: 320, duration: 45 });
class Shockwave extends ModScreenEffect {
    static Source = `
#include <bl/screen>
uniform float uThickness;
uniform float uStrength;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    float front = uRadius * uProgress;
    float width = max(uRadius * uThickness, 1e-4);
    float x = (r - front) / width;
    if (abs(x) > 1.0 || r < 1e-5) discard;

    float wave = sin(x * 3.14159265) * (1.0 - abs(x));
    float fade = (1.0 - uProgress) * uIntensity;
    vec2 offset = (d / r) * wave * width * uStrength * fade;
    fragColor = blScreen(blAtCenter(d - offset));
}
`;

    Thickness = 0.18;   // largura do anel, em fração do Radius
    Strength = 0.6;     // deslocamento, em fração da largura

    SetDefaults() {
        this.Radius = 240;
        this.Duration = 40;
    }

    Draw(cmd) {
        cmd.DrawShader(this.Shader, {
            margin: this.Radius * this.Thickness,
            uniforms: { uThickness: this.Thickness, uStrength: this.Strength },
        });
    }
}
