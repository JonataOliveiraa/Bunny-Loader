// Correção de cor: tinge (com um modo de mistura) e ajusta saturação,
// contraste, brilho, gama, matiz e temperatura, onde a máscara deixar. Na
// tela inteira por padrão; com centro e sem máscara, num círculo suave.
//   ScreenPresets.Tint.Spawn(null, {
//       color: Color.new(255, 60, 40), blend: ScreenGraphics.BlendMode.SoftLight, amount: 0.6,
//       saturation: 0.8, contrast: 1.1, mask: ScreenUtils.Mask.Vignette(),
//       pulse: { period: 60, amount: 0.3 }, fadeIn: 10, fadeOut: 30, duration: 120, key: 'mymod:rage',
//   });
class Tint extends ModScreenEffect {
    static Source = `
#include <bl/screen>
#include <bl/color>
#include <bl/mask>
uniform float uBlend;
uniform float uAmount;
uniform float uPreserve;
uniform float uSaturation;
uniform float uContrast;
uniform float uBrightness;
uniform float uGamma;
uniform float uHue;
uniform float uTemperature;
uniform float uPulse;

void main() {
    float m = clamp(blMask(vUV) * uIntensity * uPulse, 0.0, 1.0);
    if (m <= 0.0) discard;

    vec3 base = blScreen(vUV).rgb;
    vec3 graded = blAdjust(base, uSaturation, uContrast, uBrightness, uGamma, uHue, uTemperature);
    vec3 tinted = blBlend(graded, uColor.rgb, int(uBlend + 0.5));
    if (uPreserve > 0.5) tinted = blSetLuma(tinted, blLuma(graded));
    vec3 result = mix(graded, tinted, clamp(uAmount * uColor.a, 0.0, 1.0));
    fragColor = vec4(mix(base, result, m), 1.0);
}
`;

    Blend = 6;                 // ScreenGraphics.BlendMode (Color: a cor com o brilho da imagem)
    Amount = 0.5;              // quanto da cor entra (sem Color, só os ajustes)
    PreserveLuminance = false; // depois da mistura, devolve o brilho da imagem
    Saturation = 1;            // 0 = cinza
    Contrast = 1;
    Brightness = 0;            // -1 a 1
    Gamma = 1;
    HueShift = 0;              // graus
    Temperature = 0;           // -1 frio a 1 quente
    Pulse = null;              // { period: ticks, amount: 0 a 1 }: pulsa a força

    SetDefaults() {
        this.Fullscreen = true;
        this.Radius = 300;
    }

    Draw(cmd) {
        const pulse = this.Pulse && this.Pulse.period > 0
            ? 1 - (this.Pulse.amount ?? 0.3) * (0.5 - 0.5 * Math.cos((2 * Math.PI * this.Age) / this.Pulse.period))
            : 1;
        const round = !this.Fullscreen && this.Center && !this.Mask;
        cmd.DrawShader(this.Shader, {
            mask: round ? ScreenMask.Circle(this.Center, this.Radius, { feather: this.Radius * 0.5 }) : undefined,
            uniforms: {
                uBlend: this.Blend,
                uAmount: this.Color ? this.Amount : 0,
                uPreserve: this.PreserveLuminance,
                uSaturation: this.Saturation,
                uContrast: this.Contrast,
                uBrightness: this.Brightness,
                uGamma: this.Gamma,
                uHue: this.HueShift / 360,
                uTemperature: this.Temperature,
                uPulse: pulse,
            },
        });
    }
}
