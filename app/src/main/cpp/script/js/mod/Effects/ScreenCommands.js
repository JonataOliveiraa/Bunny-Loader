// O que um efeito desenha no quadro: o `cmd` do ModScreenEffect.Draw. Grava
// os passos na lista do quadro (ScreenFrame), que o ScreenEffects manda ao
// núcleo depois de todos os efeitos. Os passos rodam na ordem gravada.
class ScreenCommands {
    #effect;
    #frame;

    constructor(effect, frame) {
        this.#effect = effect;
        this.#frame = frame;
    }

    // Um alvo fora da tela, do tamanho do viewport vezes `scale` (0.5 = meia
    // resolução). Temporário: reaproveitado pelo nome, pode vir com lixo fora
    // do que você desenhou nele neste quadro. `{ persistent: true }` guarda o
    // conteúdo entre quadros (por classe de efeito + nome).
    Target(name, scale = 1, options = {}) {
        const persistent = options.persistent === true;
        const key = persistent ? this.#effect.constructor.name + ':' + name : String(name);
        return this.#frame.Target(key, scale, persistent);
    }

    // Copia a área do efeito da tela e redesenha com o shader. Opções (todas
    // opcionais; o padrão vem do efeito):
    //   center: posição no mundo   radius: em pixels do mundo
    //   fullscreen: a tela inteira  margin: pixels do mundo além do raio que o
    //     shader lê (o que ele amostra fora da caixa vem de quadros antigos)
    //   uniforms: { nome: número | [até 4] | Vector2 | Color | bool | ScreenGraphics.Array }
    //   textures: { nome do sampler2D: alvo | ScreenGraphics.Texture | Sources.* }
    //   mask: ScreenUtils.Mask (padrão: o Mask do efeito) -> blMask(vUV) no GLSL;
    //     com limite (círculo, retângulo), a caixa desenhada encolhe para ele
    //   source: o uSource/blSource do shader   layer: ScreenEffects.Layer
    DrawShader(shader = this.#effect.Shader, options = {}) {
        this.#Push(shader, null, options, 'DrawShader');
    }

    // Um passo para um alvo: lê `source` (alvo ou Sources.*) como uSource e
    // escreve em `dest` com o shader (ScreenGraphics.Passes.* ou seu).
    Blit(source, dest, shader = ScreenPasses.Copy, options = {}) {
        if (!(dest instanceof ScreenTarget) || dest.Texture <= 0) throw new TypeError('cmd.Blit: dest must be a cmd.Target(...)');

        this.#Push(shader, dest, Object.assign({}, options, { source }), 'Blit');
    }

    #Push(shader, dest, options, what) {
        const fx = this.#effect;
        if (!(shader instanceof ScreenShader)) {
            throw new TypeError('cmd.' + what + ': ' + fx.constructor.name + ' has no shader (put a .frag next to its .js, '
                + 'set `static Source` or pass a ScreenGraphics.Shader)');
        }

        const center = options.center ?? fx.Center;
        const radius = options.radius ?? fx.Radius;
        const margin = options.margin ?? fx.CaptureMargin;
        const fullscreen = options.fullscreen ?? fx.Fullscreen;
        const uv = center ? ScreenSpace.WorldToUV(center) : { X: 0.5, Y: 0.5 };
        const r = ScreenSpace.PixelsToUV(radius);
        const mask = options.mask ?? fx.Mask ?? ScreenMask.None();
        let box = fullscreen || !center ? [0, 0, 1, 1] : ScreenCommands.#Box(uv, r + ScreenSpace.PixelsToUV(margin));
        const limit = mask.Bounds();
        if (limit) box = [Math.max(box[0], limit[0]), Math.max(box[1], limit[1]), Math.min(box[2], limit[2]), Math.min(box[3], limit[3])];
        const textures = Object.assign({}, options.textures);
        if (mask.Texture) textures.uMaskTexture = mask.Texture;

        this.#frame.Draws.push({
            shader: shader.Id,
            layer: options.layer ?? fx.Layer,
            dest: dest ? dest.Texture : 0,
            source: this.#Texture(options.source),
            box,
            center: [uv.X, uv.Y],
            radius: r,
            intensity: fx.Intensity * fx.Opacity,
            progress: fx.Progress,
            age: fx.Age / 60,
            color: ColorMath.ToFloats(fx.Color) ?? [1, 1, 1, 1],
            uniforms: ScreenCommands.#Uniforms(mask.ToUniforms(), fx.Uniforms, options.uniforms),
            textures: this.#Textures(textures),
        });
    }

    #Texture(value) {
        if (value === undefined || value === null) return 0;
        if (!(value instanceof ScreenTarget)) throw new TypeError('expected a cmd.Target(...) or ScreenGraphics.Sources.*');
        if (value === ScreenTarget.History) this.#frame.History = true;
        return value.Texture;
    }

    #Textures(map) {
        const out = {};
        if (map) for (const name of Object.keys(map)) out[name] = this.#Texture(map[name]);
        return out;
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
                const value = map[name];
                if (value && typeof value === 'object' && Array.isArray(value.values)) {
                    out[name] = value;
                    continue;
                }
                const floats = ScreenCommands.ToFloats(value);
                if (floats) out[name] = floats.length === 1 ? floats[0] : floats;
            }
        }
        return out;
    }

    // Número, array, bool, Vector2 ({X, Y}), Vector3/4, Color (R, G, B, A de
    // 0 a 255) e '#rrggbb' -> floats para o GLSL. null se não dá.
    static ToFloats(value) {
        if (value === undefined || value === null) return null;
        if (typeof value === 'number') return [value];
        if (typeof value === 'string') return ColorMath.FromHex(value);
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
