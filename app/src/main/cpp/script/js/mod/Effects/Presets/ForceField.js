// Campo de força: uma bolha que curva a imagem perto da borda (como vidro),
// com a borda acesa na cor do efeito e um brilho que corre por ela.
// Hit(posição) acende o lado atingido.
//   const shield = ScreenPresets.ForceField.Spawn(player, { radius: 70, color: '#40c0ff', key: 'mymod:shield' });
//   shield.Hit(npc.Center);
class ForceField extends ModScreenEffect {
    static Source = `
#include <bl/screen>
#include <bl/noise>
uniform float uRefraction;
uniform float uRim;
uniform float uFill;
uniform float uShimmer;
uniform float uHit;
uniform vec2 uHitDir;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    float x = r / max(uRadius, 1e-5);
    if (x > 1.0) discard;

    vec2 dir = r > 1e-6 ? d / r : vec2(0.0);
    float bend = x * x * x * uRefraction * uRadius * clamp(uIntensity, 0.0, 1.0);
    vec3 base = blScreen(blAtCenter(d - dir * bend)).rgb;

    float aa = uScreenTexel.y * 1.5 / max(uRadius, 1e-5);
    float edge = 1.0 - smoothstep(1.0 - aa, 1.0, x);
    float rim = smoothstep(1.0 - uRim, 1.0, x);
    float shimmer = blNoise(d / uRadius * 3.0 + vec2(uTime * 0.6, -uTime * 0.4));
    float glow = rim * mix(1.0, 0.4 + shimmer * 1.2, uShimmer);
    glow += uHit * rim * smoothstep(0.2, 1.0, dot(dir, uHitDir)) * 2.0;

    vec3 inside = base + uColor.rgb * (glow + uFill) * uIntensity * uColor.a;
    fragColor = vec4(mix(blScreen(vUV).rgb, inside, edge), 1.0);
}
`;

    Refraction = 0.12;   // quanto a borda curva a imagem, em fração do Radius
    Rim = 0.3;           // largura da borda acesa, em fração do Radius
    Fill = 0.06;         // luz no miolo
    Shimmer = 0.8;       // 0 = borda lisa
    HitDuration = 18;    // ticks do brilho do Hit

    #hitAge = Infinity;
    #hitDir = [0, 0];

    SetDefaults() {
        this.Radius = 64;
        this.Color = [0.3, 0.75, 1, 1];
    }

    // Acende o lado de onde veio o golpe (posição no mundo).
    Hit(from) {
        if (!this.Center || !from) return;
        const x = from.X - this.Center.X, y = this.Center.Y - from.Y;
        const length = Math.hypot(x, y) || 1;
        this.#hitDir = [x / length, y / length];
        this.#hitAge = 0;
    }

    Update() {
        this.#hitAge++;
    }

    Draw(cmd) {
        const hit = this.HitDuration > 0 ? Math.max(0, 1 - this.#hitAge / this.HitDuration) : 0;
        cmd.DrawShader(this.Shader, {
            margin: 2,
            uniforms: {
                uRefraction: this.Refraction,
                uRim: this.Rim,
                uFill: this.Fill,
                uShimmer: this.Shimmer,
                uHit: hit * hit,
                uHitDir: this.#hitDir,
            },
        });
    }
}
