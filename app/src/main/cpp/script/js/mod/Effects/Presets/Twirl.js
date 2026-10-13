// Redemoinho: a imagem gira em volta do centro, mais forte perto dele.
//   ScreenPresets.Twirl.Spawn(projectile, { radius: 96, spin: 4, speed: 1 });
class Twirl extends ModScreenEffect {
    static Source = `
#include <bl/screen>
#include <bl/math>
uniform float uSpin;
uniform float uSpeed;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    if (r > uRadius) discard;

    float f = 1.0 - smoothstep(0.0, uRadius, r);
    float angle = (uSpin + uAge * uSpeed) * f * f * uIntensity;
    fragColor = blScreen(blAtCenter(blRotate(d, angle)));
}
`;

    Spin = 3;      // radianos de giro no centro
    Speed = 0.8;   // radianos por segundo a mais

    SetDefaults() {
        this.Radius = 96;
    }

    Draw(cmd) {
        cmd.DrawShader(this.Shader, { uniforms: { uSpin: this.Spin, uSpeed: this.Speed } });
    }
}
