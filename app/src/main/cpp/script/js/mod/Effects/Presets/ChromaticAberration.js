// Aberração cromática: o vermelho e o azul se separam. Radial (cresce do
// centro para fora, como uma lente) ou numa direção fixa (Direction).
//   ScreenPresets.ChromaticAberration.Spawn(null, { amount: 6, duration: 20, fadeOut: 20 });
//   ScreenPresets.ChromaticAberration.Spawn(npc, { fullscreen: false, radius: 150, direction: { X: 1, Y: 0 } });
class ChromaticAberration extends ModScreenEffect {
    static Source = `
#include <bl/screen>
#include <bl/mask>
uniform vec2 uOffset;
uniform float uRadial;

void main() {
    float m = clamp(blMask(vUV) * uIntensity, 0.0, 1.0);
    if (m <= 0.0) discard;

    vec2 off = uRadial > 0.5 ? blFromCenter(vUV) * uOffset.x : uOffset;
    off = blToUV(off) * m;
    vec4 base = blScreen(vUV);
    fragColor = vec4(blScreen(vUV + off).r, base.g, blScreen(vUV - off).b, 1.0);
}
`;

    Amount = 4;         // separação, em pixels do mundo, no canto da tela (ou no Radius)
    Direction = null;   // { X, Y } no mundo: separação fixa nessa direção

    SetDefaults() {
        this.Fullscreen = true;
        this.Radius = 160;
    }

    Draw(cmd) {
        const amount = ScreenSpace.PixelsToUV(this.Amount);
        let offset;
        if (this.Direction) {
            const length = Math.hypot(this.Direction.X, this.Direction.Y) || 1;
            offset = [(this.Direction.X / length) * amount, (-this.Direction.Y / length) * amount];
        } else {
            const reach = this.Fullscreen || !this.Center ? this.#CornerDistance() : ScreenSpace.PixelsToUV(this.Radius);
            offset = [amount / Math.max(reach, 1e-5), 0];
        }
        const round = !this.Fullscreen && this.Center && !this.Mask;
        cmd.DrawShader(this.Shader, {
            margin: this.Amount,
            mask: round ? ScreenMask.Circle(this.Center, this.Radius, { feather: this.Radius * 0.5 }) : undefined,
            uniforms: { uOffset: offset, uRadial: !this.Direction },
        });
    }

    // Do centro do efeito ao canto mais longe, em fração da altura.
    #CornerDistance() {
        const uv = this.Center ? ScreenSpace.WorldToUV(this.Center) : { X: 0.5, Y: 0.5 };
        const x = Math.max(uv.X, 1 - uv.X) * ScreenSpace.Aspect, y = Math.max(uv.Y, 1 - uv.Y);
        return Math.hypot(x, y);
    }
}
