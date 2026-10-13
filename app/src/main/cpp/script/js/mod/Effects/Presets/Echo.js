// Rastro: mistura o quadro anterior (ScreenGraphics.Sources.History, já com
// os efeitos) no atual; o que se move deixa um eco que some aos poucos.
//   ScreenPresets.Echo.Spawn(player, { radius: 120, persistence: 0.8, duration: 120, fadeOut: 30 });
class Echo extends ModScreenEffect {
    static Source = `
#include <bl/screen>
uniform sampler2D uHistory;
uniform float uPersistence;
uniform vec2 uDrift;
uniform float uFullscreen;

void main() {
    vec4 base = blScreen(vUV);
    float mask = uFullscreen > 0.5 ? 1.0 : 1.0 - smoothstep(uRadius * 0.7, uRadius, blDistance(vUV));
    if (mask <= 0.0) discard;
    vec4 previous = texture(uHistory, vUV + uDrift);
    fragColor = vec4(mix(base.rgb, previous.rgb, clamp(uPersistence * mask * uIntensity, 0.0, 0.97)), 1.0);
}
`;

    Persistence = 0.75;   // quanto do quadro anterior fica (0 a 0,97)
    Drift = [0, 0];       // o eco escorre nessa direção (fração da tela por quadro)

    SetDefaults() {
        this.Radius = 120;
    }

    Draw(cmd) {
        cmd.DrawShader(this.Shader, {
            textures: { uHistory: ScreenGraphics.Sources.History },
            uniforms: { uPersistence: this.Persistence, uDrift: this.Drift, uFullscreen: this.Fullscreen },
        });
    }
}
