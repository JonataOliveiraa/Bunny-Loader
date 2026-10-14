// Ondas na água: anéis que saem do centro. Com Duration, um pulso de Count
// anéis vai até o Radius e some; sem Duration, ondulam sem parar.
//   ScreenPresets.Ripple.Spawn(projectile.Center, { radius: 200, count: 3, duration: 60 });
//   ScreenPresets.Ripple.Spawn(player, { radius: 90, strength: 2, duration: 0, key: 'mymod:puddle' });
class Ripple extends ModScreenEffect {
    static Source = `
#include <bl/screen>
#include <bl/math>
uniform float uCount;
uniform float uWavelength;
uniform float uStrength;
uniform float uSpeed;
uniform float uPulse;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    if (r > uRadius || r < 1e-5) discard;

    float wl = max(uWavelength, 1e-5);
    float wave, env;
    if (uPulse > 0.5) {
        float phase = ((uRadius + uCount * wl) * uProgress - r) / wl;
        if (phase < 0.0 || phase > uCount) discard;
        wave = sin(phase * BL_TAU);
        env = sin(phase / uCount * BL_PI);
    } else {
        wave = sin((r / wl - uAge * uSpeed) * BL_TAU);
        env = 1.0;
    }
    env *= 1.0 - r / uRadius;
    vec2 offset = (d / r) * wave * env * uStrength * uIntensity;
    fragColor = blScreen(blAtCenter(d - offset));
}
`;

    Count = 3;         // anéis do pulso
    Wavelength = 24;   // distância entre os anéis, em pixels do mundo
    Strength = 4;      // deslocamento máximo, em pixels do mundo
    Speed = 1.5;       // anéis por segundo (sem Duration)

    SetDefaults() {
        this.Radius = 180;
        this.Duration = 60;
    }

    Draw(cmd) {
        cmd.DrawShader(this.Shader, {
            margin: this.Strength,
            uniforms: {
                uCount: this.Count,
                uWavelength: ScreenSpace.PixelsToUV(this.Wavelength),
                uStrength: ScreenSpace.PixelsToUV(this.Strength),
                uSpeed: this.Speed,
                uPulse: this.Duration > 0,
            },
        });
    }
}
