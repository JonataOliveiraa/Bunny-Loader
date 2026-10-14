// A sobreposição de debug dos efeitos de tela (/fx debug): o contorno de cada
// passo desenhado na tela (a caixa tracejada e o círculo do raio, na cor do
// efeito; a distorção em branco) e, no topo, um painel com os efeitos
// vivos e o custo (passos por quadro, GPU, fps). O contorno é um passo a
// mais, na camada Screen: entra na conta do painel.
class ScreenDebug {
    static MAX = 32;
    static Source = `
#include <bl/color>
uniform vec4 uBoxes[32];
uniform vec4 uCircles[32];
uniform float uCount;

void main() {
    vec2 px = vUV * uResolution;
    for (int i = 0; i < 32; i++) {
        if (float(i) >= uCount) break;
        vec4 c = uCircles[i];
        vec3 color = c.w < 0.0 ? vec3(1.0) : blHsvToRgb(vec3(c.w, 0.7, 1.0));
        if (c.z > 0.0) {
            vec2 d = (vUV - c.xy) * vec2(uAspect, 1.0);
            if (abs(length(d) - c.z) * uResolution.y < 1.25) {
                fragColor = vec4(color, 1.0);
                return;
            }
        }
        vec4 b = uBoxes[i] * uResolution.xyxy;
        vec2 lo = px - b.xy, hi = b.zw - px;
        float edge = min(min(lo.x, lo.y), min(hi.x, hi.y));
        if (edge > -1.0 && edge < 1.25 && mod(floor((px.x + px.y) / 6.0), 2.0) < 1.0) {
            fragColor = vec4(color, 1.0);
            return;
        }
    }
    discard;
}
`;

    static #enabled = false;
    static #installed = false;
    static #shader = null;
    static #boxes = [];
    static #circles = [];
    static #mark = null;
    static #summary = '';
    static #frames = 0;

    static #DRAW = 'Vector2 DrawBorderString(SpriteBatch sb, string text, Vector2 pos, Color color, float scale, float anchorx, float anchory, int maxCharactersDisplayed)';
    static #LINES = 8;

    static get Enabled() {
        return ScreenDebug.#enabled;
    }

    static set Enabled(value) {
        ScreenDebug.#enabled = value === true;
        ScreenDebug.#mark = null;
        ScreenDebug.#summary = '';
        if (ScreenDebug.#enabled) ScreenDebug.#Install();
    }

    // A cor de um efeito (matiz de 0 a 1): a mesma para a mesma classe e Key.
    static Hue(fx) {
        const text = fx.constructor.name + ':' + (fx.Key ?? '');
        let h = 0;
        for (let i = 0; i < text.length; i++) h = (h * 31 + text.charCodeAt(i)) >>> 0;
        return (h % 1000) / 1000;
    }

    // Os passos que o efeito acabou de gravar (ScreenEffects, no quadro).
    static Track(fx, draws) {
        const hue = ScreenDebug.Hue(fx);
        for (const d of draws) {
            if (d.dest !== 0) continue;
            const radius = !fx.Center ? 0 : fx.Fullscreen ? ScreenSpace.PixelsToUV(6) : d.radius;
            ScreenDebug.#Add(d.box, d.center, radius, hue);
        }
    }

    // Põe o passo do contorno no fim do quadro (e a caixa da distorção).
    static Decorate(frame, distortion) {
        if (distortion) ScreenDebug.#Add(distortion.box, [0, 0], 0, -1);
        const count = ScreenDebug.#boxes.length / 4;
        if (count === 0) return;
        if (!ScreenDebug.#shader) ScreenDebug.#shader = ScreenShader.FromSource(ScreenDebug.Source, 'bl/ScreenDebug');

        frame.Draws.push({
            shader: ScreenDebug.#shader.Id,
            layer: ScreenEffects.Layer.Screen,
            dest: 0,
            source: 0,
            box: [0, 0, 1, 1],
            center: [0.5, 0.5],
            radius: 0,
            intensity: 1,
            progress: 0,
            age: 0,
            color: [1, 1, 1, 1],
            uniforms: {
                uBoxes: { components: 4, values: ScreenDebug.#boxes },
                uCircles: { components: 4, values: ScreenDebug.#circles },
                uCount: count,
            },
            textures: {},
        });
        ScreenDebug.#boxes = [];
        ScreenDebug.#circles = [];
    }

    static #Add(box, center, radius, hue) {
        if (ScreenDebug.#boxes.length / 4 >= ScreenDebug.MAX) return;
        ScreenDebug.#boxes.push(box[0], box[1], box[2], box[3]);
        ScreenDebug.#circles.push(center[0], center[1], radius, hue);
    }

    // O painel: no desenho das barras de invasão (DrawInterface_15), que roda
    // todo quadro com o SpriteBatch da interface aberto.
    static #Install() {
        if (ScreenDebug.#installed) return;
        ScreenDebug.#installed = true;
        Terraria.Main['void DrawInterface_15_InvasionProgressBars()'].hook((original) => {
            original();
            if (ScreenDebug.#enabled && !Terraria.Main.gameMenu) Safe.Run('ScreenDebug', () => ScreenDebug.#DrawPanel());
        });
    }

    // A linha do custo, refeita a cada 30 quadros (médias do intervalo).
    static #Summary() {
        if (ScreenDebug.#frames++ % 30 !== 0 && ScreenDebug.#summary) return ScreenDebug.#summary;

        const now = { at: Date.now(), s: bl.screen.__stats() };
        const mark = ScreenDebug.#mark;
        ScreenDebug.#mark = now;
        const live = ScreenEffects.Active.length;
        const pt = ScreenDebug.#Pt();
        let text = (pt ? 'Efeitos de tela: ' + live + ' vivo' + (live === 1 ? '' : 's') : 'Screen effects: ' + live + ' live');
        if (mark) {
            const a = mark.s, b = now.s;
            const frames = Math.max(b.worldPostRender - a.worldPostRender, 0);
            const samples = b.gpuSamples - a.gpuSamples;
            const fps = (b.worldPostRender - a.worldPostRender) / Math.max((now.at - mark.at) / 1000, 1e-3);
            text += '  ·  ' + (frames > 0 ? ((b.draws - a.draws) / frames).toFixed(1) : '0') + (pt ? ' passos/quadro' : ' passes/frame');
            if (samples > 0) text += '  ·  GPU ' + ((b.gpuNs - a.gpuNs) / samples / 1e6).toFixed(2) + ' ms';
            text += '  ·  ' + fps.toFixed(0) + ' fps';
        }
        if (!ScreenEffects.Enabled) text += pt ? '  ·  DESLIGADOS' : '  ·  OFF';
        if (ScreenEffects.HotReload) text += pt ? '  ·  recarga ligada' : '  ·  hot reload on';
        return (ScreenDebug.#summary = text);
    }

    static #DrawPanel() {
        const Main = Terraria.Main;
        const batch = Main.spriteBatch;
        const x = Math.round(Main.screenWidth / 2);
        const draw = (text, y, color) => Terraria.Utils[ScreenDebug.#DRAW](batch, text, Vector2.new(x, y), color, 0.8, 0.5, 0, -1);

        let y = Math.round(Main.screenHeight * 0.13);
        draw(ScreenDebug.#Summary(), y, Color.White);
        const active = ScreenEffects.Active;
        for (const fx of active.slice(0, ScreenDebug.#LINES)) {
            y += 20;
            const rgb = ColorMath.Hsv(ScreenDebug.Hue(fx) * 360, 0.45, 1);
            draw(ScreenDebug.#Describe(fx), y, Color.new(Math.round(rgb[0] * 255), Math.round(rgb[1] * 255), Math.round(rgb[2] * 255)));
        }
        if (active.length > ScreenDebug.#LINES) draw('+' + (active.length - ScreenDebug.#LINES), y + 20, Color.White);
    }

    static #Describe(fx) {
        let text = fx.constructor.name;
        if (fx.Key !== null && fx.Key !== undefined) text += ' [' + fx.Key + ']';
        if (fx.Fullscreen || !fx.Center) text += ScreenDebug.#Pt() ? '  tela' : '  screen';
        else text += '  r ' + Math.round(fx.Radius);
        text += '  ' + (fx.Age / 60).toFixed(1) + ' s';
        if (fx.Duration > 0) text += ' / ' + (fx.Duration / 60).toFixed(1) + ' s';
        if (fx.Intensity * fx.Opacity < 0.999) text += '  x' + (fx.Intensity * fx.Opacity).toFixed(2);
        if (fx.Shader && fx.Shader.Status === 'failed') text += ScreenDebug.#Pt() ? '  (shader com erro)' : '  (shader failed)';
        return text;
    }

    static #Pt() {
        return /^pt/.test(ModLocalization.ActiveCultureName || '');
    }
}
