// Desfoque: em volta do centro (sumindo na borda) ou na tela inteira
// (fullscreen: true). Duas passadas em meia resolução (BlurH e BlurV).
//   ScreenPresets.Blur.Spawn(null, { fullscreen: true, strength: 6, fadeIn: 20 });
class Blur extends ModScreenEffect {
    static Source = `
#include <bl/screen>
uniform sampler2D uBlurred;
uniform float uFullscreen;

void main() {
    vec4 base = blScreen(vUV);
    float mask = uFullscreen > 0.5 ? 1.0 : 1.0 - smoothstep(uRadius * 0.6, uRadius, blDistance(vUV));
    if (mask <= 0.0) discard;
    fragColor = vec4(mix(base.rgb, texture(uBlurred, vUV).rgb, mask * clamp(uIntensity, 0.0, 1.0)), 1.0);
}
`;

    Strength = 4;   // raio do desfoque, em texels da meia resolução

    SetDefaults() {
        this.Radius = 160;
    }

    Draw(cmd) {
        const { Passes, Sources } = ScreenGraphics;
        const margin = this.Strength * 4;
        const a = cmd.Target('bl.blur.a', 0.5);
        const b = cmd.Target('bl.blur.b', 0.5);
        cmd.Blit(Sources.Screen, a, Passes.BlurH, { margin, uniforms: { uBlurRadius: this.Strength } });
        cmd.Blit(a, b, Passes.BlurV, { margin, uniforms: { uBlurRadius: this.Strength } });
        cmd.DrawShader(this.Shader, { textures: { uBlurred: b }, uniforms: { uFullscreen: this.Fullscreen } });
    }
}
