// O que um efeito desenha no quadro: o `cmd` do ModScreenEffect.Draw. Grava
// na lista do quadro, que o ScreenEffects manda ao núcleo (bl.screen.submit)
// depois de todos os efeitos.
class ScreenCommands {
    #effect;
    #draws;

    constructor(effect, draws) {
        this.#effect = effect;
        this.#draws = draws;
    }

    // Copia a área do efeito e redesenha com o shader. Opções (todas
    // opcionais; o padrão vem do efeito):
    //   center: posição no mundo   radius: em pixels do mundo
    //   fullscreen: a tela inteira  margin: pixels do mundo além do raio que o
    //     shader lê (o que ele amostra fora da caixa vem de quadros antigos)
    //   uniforms: { nome: número | [até 4] | Vector2 | Color | bool }
    //   layer: ScreenEffects.Layer
    DrawShader(shader = this.#effect.Shader, options = {}) {
        const fx = this.#effect;
        if (!(shader instanceof ScreenShader)) throw new TypeError('cmd.DrawShader: pass a ScreenGraphics.Shader');

        const center = options.center ?? fx.Center;
        const radius = options.radius ?? fx.Radius;
        const margin = options.margin ?? fx.CaptureMargin;
        const fullscreen = options.fullscreen ?? fx.Fullscreen;
        const uv = center ? ScreenSpace.WorldToUV(center) : { X: 0.5, Y: 0.5 };
        const r = ScreenSpace.PixelsToUV(radius);

        this.#draws.push({
            shader: shader.Id,
            layer: options.layer ?? fx.Layer,
            box: fullscreen || !center ? [0, 0, 1, 1] : ScreenCommands.#Box(uv, r + ScreenSpace.PixelsToUV(margin)),
            center: [uv.X, uv.Y],
            radius: r,
            intensity: fx.Intensity * fx.Opacity,
            progress: fx.Progress,
            age: fx.Age / 60,
            color: ScreenCommands.ToFloats(fx.Color) ?? [1, 1, 1, 1],
            uniforms: ScreenCommands.#Uniforms(fx.Uniforms, options.uniforms),
        });
    }

    // A caixa de um círculo (raio em fração da altura) em fração da tela.
    static #Box(uv, r) {
        const rx = r / ScreenSpace.Aspect;
        return [uv.X - rx, uv.Y - r, uv.X + rx, uv.Y + r];
    }

    static #Uniforms(...maps) {
        const out = {};
        for (const map of maps) {
            if (!map) continue;
            for (const name of Object.keys(map)) {
                const value = ScreenCommands.ToFloats(map[name]);
                if (value) out[name] = value.length === 1 ? value[0] : value;
            }
        }
        return out;
    }

    // Número, array, bool, Vector2 ({X, Y}), Vector3/4 e Color (R, G, B, A de
    // 0 a 255) -> floats para o GLSL. null se não dá.
    static ToFloats(value) {
        if (value === undefined || value === null) return null;
        if (typeof value === 'number') return [value];
        if (typeof value === 'boolean') return [value ? 1 : 0];
        if (Array.isArray(value)) return value.slice(0, 4).map(Number);
        if (typeof value.R === 'number') return [value.R / 255, value.G / 255, value.B / 255, (value.A ?? 255) / 255];
        if (typeof value.X === 'number') {
            const v = [value.X, value.Y];
            if (typeof value.Z === 'number') v.push(value.Z);
            if (typeof value.W === 'number') v.push(value.W);
            return v;
        }
        return null;
    }
}
