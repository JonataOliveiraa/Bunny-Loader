// Pixelado: a imagem vira blocos de Size pixels do mundo, presos ao mundo
// (não tremem quando a câmera anda). Num círculo, na máscara ou na tela inteira.
//   ScreenPresets.Pixelate.Spawn(player, { radius: 120, size: 8, duration: 90, fadeOut: 30 });
class Pixelate extends ModScreenEffect {
    static Source = `
#include <bl/screen>
#include <bl/mask>
uniform float uBlock;
uniform vec2 uAnchor;

void main() {
    float m = blMask(vUV) * uIntensity;
    if (m < 0.5) discard;

    vec2 p = vec2(vUV.x * uAspect, vUV.y) - uAnchor;
    vec2 q = (floor(p / uBlock) + 0.5) * uBlock + uAnchor;
    fragColor = blScreen(vec2(q.x / uAspect, q.y));
}
`;

    Size = 6;   // lado do bloco, em pixels do mundo

    SetDefaults() {
        this.Radius = 120;
    }

    Draw(cmd) {
        const block = ScreenSpace.PixelsToUV(Math.max(this.Size, 1));
        const origin = ScreenSpace.WorldToUV({ X: 0, Y: 0 });
        const wrap = (v) => v - Math.floor(v / block) * block;
        const round = !this.Fullscreen && this.Center && !this.Mask;
        cmd.DrawShader(this.Shader, {
            margin: this.Size,
            mask: round ? ScreenMask.Circle(this.Center, this.Radius, { feather: 0 }) : undefined,
            uniforms: { uBlock: block, uAnchor: [wrap(origin.X * ScreenSpace.Aspect), wrap(origin.Y)] },
        });
    }
}
