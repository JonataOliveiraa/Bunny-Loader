// Brilho que vaza (bloom): o que passa do Threshold de brilho é desfocado e
// somado de volta, na cor do efeito. Na tela inteira por padrão.
//   ScreenPresets.Bloom.Spawn(null, { threshold: 0.6, intensity: 1.2, key: 'mymod:bloom' });
class Bloom extends ModScreenEffect {
    static Source = `
#include <bl/screen>
uniform sampler2D uBloom;
uniform float uFullscreen;

void main() {
    vec4 base = blScreen(vUV);
    float mask = uFullscreen > 0.5 ? 1.0 : 1.0 - smoothstep(uRadius * 0.6, uRadius, blDistance(vUV));
    if (mask <= 0.0) discard;
    vec3 glow = texture(uBloom, vUV).rgb * uColor.rgb * uIntensity * mask;
    fragColor = vec4(base.rgb + glow, 1.0);
}
`;

    Threshold = 0.65;   // brilho (0 a 1) a partir do qual vaza
    Spread = 6;         // raio do desfoque, em texels da meia resolução

    SetDefaults() {
        this.Fullscreen = true;
        this.Radius = 200;
    }

    Draw(cmd) {
        const { Passes, Sources } = ScreenGraphics;
        const margin = this.Spread * 4;
        const a = cmd.Target('bl.bloom.a', 0.5);
        const b = cmd.Target('bl.bloom.b', 0.5);
        cmd.Blit(Sources.Screen, a, Passes.Threshold, { margin, uniforms: { uThreshold: this.Threshold, uKnee: 0.15 } });
        cmd.Blit(a, b, Passes.BlurH, { margin, uniforms: { uBlurRadius: this.Spread } });
        cmd.Blit(b, a, Passes.BlurV, { margin, uniforms: { uBlurRadius: this.Spread } });
        cmd.DrawShader(this.Shader, { textures: { uBloom: a }, uniforms: { uFullscreen: this.Fullscreen } });
    }
}
