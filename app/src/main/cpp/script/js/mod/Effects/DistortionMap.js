// O mapa de distorção compartilhado: o `d` do ModScreenEffect.DrawDistortion.
// Cada chamada é uma forma que desloca a imagem; as de todos os efeitos do
// quadro somam numa passada só, desenhada na posição do primeiro efeito que
// distorceu (antes do Draw dele). Posições no mundo; raio e deslocamento em
// pixels do mundo (acompanham o zoom). Até 32 formas por quadro.
class DistortionMap {
    static MAX = 32;
    static Source = `
#include <bl/screen>
#include <bl/math>
#include <bl/noise>
uniform vec4 uShapeA[32];
uniform vec4 uShapeB[32];
uniform float uShapeCount;

vec2 shapeOffset(vec4 a, vec4 b, vec2 uv) {
    vec2 d = uv - a.yz;
    d.x *= uAspect;
    float r = length(d);
    if (r >= a.w) return vec2(0.0);

    float x = r / a.w;
    float fall = (1.0 - x * x) * (1.0 - x * x);
    vec2 dir = r > 1e-6 ? d / r : vec2(0.0);
    int type = int(a.x + 0.5);
    vec2 o = vec2(0.0);
    if (type == 1) {
        o = dir * b.x * fall;
    } else if (type == 2) {
        o = d - blRotate(d, -b.x * fall);
    } else if (type == 3) {
        float t = (r - b.y) / max(b.z, 1e-5);
        if (abs(t) < 1.0) o = dir * b.x * sin(t * BL_PI) * (1.0 - abs(t));
    } else if (type == 4) {
        vec2 p = d * b.y + vec2(0.0, -uTime * b.z);
        o = (vec2(blNoise(p), blNoise(p + 17.3)) - 0.5) * 2.0 * b.x * fall;
    } else if (type == 5) {
        o = vec2(b.y, b.z) * b.x * fall;
    }
    o.x /= uAspect;
    return o;
}

void main() {
    vec2 total = vec2(0.0);
    for (int i = 0; i < 32; i++) {
        if (float(i) >= uShapeCount) break;
        total += shapeOffset(uShapeA[i], uShapeB[i], vUV);
    }
    if (dot(total, total) < 1e-12) discard;
    fragColor = blScreen(vUV - total);
}
`;

    static #shader = null;
    static #warned = false;

    #a = [];
    #b = [];
    #box = null;
    #layer = 0;
    #scale = 1;

    get Count() {
        return this.#a.length / 4;
    }

    // O efeito que vai desenhar agora: a força das formas dele acompanha o
    // Intensity e o fade (Opacity).
    Begin(effect) {
        this.#scale = effect.Intensity * effect.Opacity;
        if (this.Count === 0) this.#layer = effect.Layer;
    }

    // Empurra a imagem para fora (strength > 0, lupa) ou puxa para o centro
    // (< 0, buraco negro), forte no centro e zero na borda.
    Radial(center, radius, strength) {
        this.#Add(1, center, radius, ScreenSpace.PixelsToUV(strength), 0, 0);
    }

    // Gira a imagem `angle` radianos no centro, nada na borda.
    Swirl(center, radius, angle) {
        this.#Add(2, center, radius, angle, 0, 0);
    }

    // Um anel de raio `ringRadius` e largura `width` que empurra para fora
    // (onda de choque).
    Ring(center, ringRadius, width, strength) {
        this.#Add(3, center, ringRadius + width, ScreenSpace.PixelsToUV(strength),
            ScreenSpace.PixelsToUV(ringRadius), ScreenSpace.PixelsToUV(width));
    }

    // Ar quente: ruído que ondula a imagem. scale: ondas por altura da tela;
    // speed: o quanto sobe por segundo.
    Haze(center, radius, strength, options = {}) {
        this.#Add(4, center, radius, ScreenSpace.PixelsToUV(strength), options.scale ?? 40, options.speed ?? 1.5);
    }

    // Desloca tudo numa direção ({ X, Y } no mundo).
    Push(center, radius, direction, strength) {
        const length = Math.hypot(direction.X, direction.Y) || 1;
        this.#Add(5, center, radius, ScreenSpace.PixelsToUV(strength), direction.X / length, -direction.Y / length);
    }

    #Add(type, center, radius, strength, p1, p2) {
        if (this.Count >= DistortionMap.MAX) {
            if (!DistortionMap.#warned) Safe.Once('distortion.max', 'DistortionMap: more than ' + DistortionMap.MAX + ' shapes in a frame; the rest is ignored');
            DistortionMap.#warned = true;
            return;
        }

        const uv = ScreenSpace.WorldToUV(center);
        const r = ScreenSpace.PixelsToUV(radius);
        this.#a.push(type, uv.X, uv.Y, r);
        this.#b.push(strength * this.#scale, p1, p2, 0);

        const rx = r / ScreenSpace.Aspect;
        const box = [uv.X - rx, uv.Y - r, uv.X + rx, uv.Y + r];
        this.#box = this.#box
            ? [Math.min(this.#box[0], box[0]), Math.min(this.#box[1], box[1]), Math.max(this.#box[2], box[2]), Math.max(this.#box[3], box[3])]
            : box;
    }

    // O passo que aplica tudo, ou null se nada distorceu.
    Build() {
        if (this.Count === 0) return null;
        if (!DistortionMap.#shader) DistortionMap.#shader = ScreenShader.FromSource(DistortionMap.Source, 'bl/DistortionMap');

        return {
            shader: DistortionMap.#shader.Id,
            layer: this.#layer,
            dest: 0,
            source: 0,
            box: this.#box,
            center: [0.5, 0.5],
            radius: 0,
            intensity: 1,
            progress: 0,
            age: 0,
            color: [1, 1, 1, 1],
            uniforms: {
                uShapeA: { components: 4, values: this.#a },
                uShapeB: { components: 4, values: this.#b },
                uShapeCount: this.Count,
            },
            textures: {},
        };
    }
}
