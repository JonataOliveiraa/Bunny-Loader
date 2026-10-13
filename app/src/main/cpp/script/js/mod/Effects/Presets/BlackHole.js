// Buraco negro: o mapa de distorção puxa a imagem para o centro e a gira; por
// cima, um núcleo escuro com um anel de luz (na cor do efeito).
//   ScreenPresets.BlackHole.Spawn(projectile, { radius: 140, color: new Color(255, 160, 60) });
class BlackHole extends ModScreenEffect {
    static Source = `
#include <bl/screen>
#include <bl/shapes>
uniform float uCore;

void main() {
    float r = blDistance(vUV);
    float core = uRadius * uCore;
    if (r > core * 2.2) discard;

    vec4 base = blScreen(vUV);
    float hole = blCircle(r, core, core * 0.2);
    float ring = blRing(r, core * 1.3, core * 0.45, core * 0.25);
    vec3 col = mix(base.rgb, vec3(0.0), hole * clamp(uIntensity, 0.0, 1.0));
    col += uColor.rgb * ring * 0.9 * uIntensity;
    fragColor = vec4(col, 1.0);
}
`;

    Pull = 0.35;   // quanto puxa, em fração do Radius
    Spin = 2.5;    // giro no centro, em radianos
    Core = 0.18;   // tamanho do núcleo escuro, em fração do Radius

    SetDefaults() {
        this.Radius = 140;
        this.Color = [1.0, 0.6, 0.25, 1.0];
    }

    DrawDistortion(d) {
        d.Radial(this.Center, this.Radius, -this.Pull * this.Radius);
        d.Swirl(this.Center, this.Radius, this.Spin);
    }

    Draw(cmd) {
        cmd.DrawShader(this.Shader, { uniforms: { uCore: this.Core } });
    }
}
