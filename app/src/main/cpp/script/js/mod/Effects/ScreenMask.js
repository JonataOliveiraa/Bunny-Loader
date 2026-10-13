// Onde um efeito vale: 1 dentro, 0 fora, com a borda suave. Os prontos e o
// cmd.DrawShader aplicam sozinhos; no GLSL do mod, `#include <bl/mask>` e
// `blMask(vUV)`. Posições e tamanhos no mundo (acompanham a câmera e o zoom);
// convertidos a cada quadro.
//   Mask.Circle(player.Center, 300, { feather: 120 })
//   Mask.Gradient('Up', { end: 0.4 })   // forte embaixo, some até 40% da altura
class ScreenMask {
    static Falloff = Object.freeze({ Linear: 0, Smooth: 1, Exponential: 2 });

    #type;
    #params;
    #inverted;

    constructor(type, params, inverted = false) {
        this.#type = type;
        this.#params = params;
        this.#inverted = inverted;
    }

    static None() {
        return new ScreenMask(0, {});
    }

    // feather: a borda suave para dentro, em pixels do mundo.
    static Circle(center, radius, options = {}) {
        return new ScreenMask(1, ScreenMask.#Options({ center: ScreenMask.#Point(center), radius, feather: options.feather ?? radius * 0.25 }, options));
    }

    static Ring(center, radius, width, options = {}) {
        return new ScreenMask(2, ScreenMask.#Options({ center: ScreenMask.#Point(center), radius, width, feather: options.feather ?? width * 0.5 }, options));
    }

    // rect: { X, Y, Width, Height } no mundo (um Rectangle do jogo serve).
    static Rect(rect, options = {}) {
        const r = { X: rect.X, Y: rect.Y, Width: rect.Width, Height: rect.Height };
        return new ScreenMask(3, ScreenMask.#Options({ rect: r, feather: options.feather ?? 16 }, options));
    }

    // De um lado da tela ao outro: 'Up' (forte embaixo, some subindo),
    // 'Down', 'Left', 'Right' ou { X, Y } (Y para cima). start/end: onde
    // começa e termina de sumir, em fração da tela.
    static Gradient(direction = 'Up', options = {}) {
        const named = { Up: [0, 1], Down: [0, -1], Left: [-1, 0], Right: [1, 0] };
        let dir = named[direction] ?? [direction.X, direction.Y];
        const length = Math.hypot(dir[0], dir[1]) || 1;
        dir = [dir[0] / length, dir[1] / length];
        return new ScreenMask(4, ScreenMask.#Options({ dir, start: options.start ?? 0, end: options.end ?? 1 }, options));
    }

    // As bordas da tela (1 nos cantos, 0 no meio). start: de que distância do
    // centro começa (0 a 1); softness: a largura da transição; roundness: 1
    // redonda na tela, 0 seguindo o retângulo.
    static Vignette(options = {}) {
        return new ScreenMask(5, ScreenMask.#Options({ start: options.start ?? 0.45, softness: options.softness ?? 0.55, roundness: options.roundness ?? 1 }, options));
    }

    // Uma imagem: o alfa (padrão) ou o brilho ('luma') de cada pixel.
    static Texture(texture, options = {}) {
        if (!(texture instanceof ScreenTarget)) throw new TypeError('Mask.Texture: pass a ScreenGraphics.Texture or a cmd.Target');
        return new ScreenMask(6, ScreenMask.#Options({ texture, luma: options.channel === 'luma' }, options));
    }

    Inverted() {
        return new ScreenMask(this.#type, this.#params, !this.#inverted);
    }

    get Texture() {
        return this.#type === 6 ? this.#params.texture : null;
    }

    // Os uniforms do bl/mask para este quadro.
    ToUniforms() {
        const p = this.#params;
        const falloff = p.falloff ?? ScreenMask.Falloff.Smooth;
        const invert = this.#inverted ? 1 : 0;
        let a = [0, 0, 0, 0], b = [falloff, invert, 0, 0];
        if (this.#type === 1 || this.#type === 2) {
            const uv = ScreenSpace.WorldToUV(p.center);
            const r = ScreenSpace.PixelsToUV(p.radius);
            if (this.#type === 1) {
                a = [uv.X, uv.Y, r, Math.min(ScreenSpace.PixelsToUV(p.feather), r)];
            } else {
                a = [uv.X, uv.Y, r, ScreenSpace.PixelsToUV(p.width)];
                b[2] = ScreenSpace.PixelsToUV(p.feather);
            }
        } else if (this.#type === 3) {
            a = ScreenMask.#Uniforms3(p.rect);
            b[2] = ScreenSpace.PixelsToUV(p.feather);
        } else if (this.#type === 4) {
            a = [p.dir[0], p.dir[1], p.start, p.end];
        } else if (this.#type === 5) {
            a = [p.start, p.roundness, p.softness, 0];
        } else if (this.#type === 6) {
            b[2] = p.luma ? 1 : 0;
        }
        return { uMaskType: this.#type, uMaskA: a, uMaskB: b };
    }

    // A caixa em fração da tela onde a máscara pode valer algo (null: a tela
    // toda). Invertida, vale fora: a tela toda.
    Bounds() {
        if (this.#inverted) return null;
        const p = this.#params;
        if (this.#type === 1 || this.#type === 2) {
            const uv = ScreenSpace.WorldToUV(p.center);
            const r = ScreenSpace.PixelsToUV(p.radius + (this.#type === 2 ? p.width / 2 + p.feather : 0));
            const rx = r / ScreenSpace.Aspect;
            return [uv.X - rx, uv.Y - r, uv.X + rx, uv.Y + r];
        }
        if (this.#type === 3) {
            const f = ScreenSpace.PixelsToUV(p.feather);
            const u = ScreenMask.#Uniforms3(p.rect);
            return [u[0] - f, u[1] - f, u[2] + f, u[3] + f];
        }
        return null;
    }

    static #Uniforms3(rect) {
        const lo = ScreenSpace.WorldToUV({ X: rect.X, Y: rect.Y + rect.Height });
        const hi = ScreenSpace.WorldToUV({ X: rect.X + rect.Width, Y: rect.Y });
        return [lo.X, lo.Y, hi.X, hi.Y];
    }

    static #Point(p) {
        return { X: p.X, Y: p.Y };
    }

    static #Options(params, options) {
        if (options.falloff !== undefined) params.falloff = options.falloff;
        return params;
    }
}
