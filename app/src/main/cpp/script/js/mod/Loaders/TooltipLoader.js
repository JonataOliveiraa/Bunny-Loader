// O celular desenha cada linha do tooltip com DrawString, sem o parser de tags
// [c/RRGGBB:texto] do PC: a linha com tag é desenhada aqui, trecho a trecho, e
// a medida dela ignora as tags.
class TooltipLoader {
    static #COLOR_TAG = /\[c\/([0-9a-fA-F]{6}):([^\]]*)\]/g;

    static Hex(color) {
        if (typeof color === 'string') return color.replace(/^#/, '').slice(0, 6).toUpperCase();

        const hex = (v) => Math.min(Math.max(Math.round(v || 0), 0), 255).toString(16).padStart(2, '0');
        return (hex(color.R) + hex(color.G) + hex(color.B)).toUpperCase();
    }

    static HasTags(text) { return typeof text === 'string' && text.indexOf('[c/') >= 0; }
    static StripTags(text) { return text.replace(TooltipLoader.#COLOR_TAG, '$2'); }

    static Segments(text) {
        const tag = TooltipLoader.#COLOR_TAG;
        const out = [];
        let at = 0;

        tag.lastIndex = 0;
        for (let m; (m = tag.exec(text));) {
            if (m.index > at) out.push({ text: text.slice(at, m.index) });

            const v = parseInt(m[1], 16);
            out.push({ text: m[2], rgb: [(v >> 16) & 255, (v >> 8) & 255, v & 255] });
            at = m.index + m[0].length;
        }
        if (at < text.length) out.push({ text: text.slice(at) });

        return out;
    }

    static Install() {
        const Main = Terraria.Main;
        const drawTooltip = Main['void MouseText_DrawItemTooltip(Main.MouseTextCache info, int rare, byte diff, int X, int Y)'];

        let drawing = 0;
        drawTooltip.hook((original) => {
            drawing++;
            try {
                return original();
            } finally {
                drawing--;
            }
        });

        TooltipLoader.#HookLines(() => drawing > 0);
        TooltipLoader.#HookDrawing(drawTooltip);
    }

    // As linhas do jogo viram TooltipLine para o ModifyTooltips, e voltam.
    static #HookLines(isDrawing) {
        Terraria.Main['void MouseText_DrawItemTooltip_GetLinesInfo(Item item, ref int yoyoLogo, ref int researchLine, ref int materialsLine, float oldKB, ref int numLines, string[] toolTipLine, bool[] preFixLine, bool[] badPreFixLine, ref int setBonusLine, ref Color setBonusColour)'].hook(
            (original, item, yoyo, research, materials, oldKB, numLines, lines, pre, bad, setBonus, setColor) => {
                original(item, yoyo, research, materials, oldKB, numLines, lines, pre, bad, setBonus, setColor);

                const m = ItemLoader.Of(item);
                const own = m && Hooks.Overrides(m.constructor, ModItem, 'ModifyTooltips') ? m : null;
                if (!own && !globalItems.AnyWith(item, ['ModifyTooltips'])) return;

                const special = new Map([[yoyo.value, 'OneDropLogo'], [research.value, 'JourneyResearch'],
                                         [materials.value, 'Material'], [setBonus.value, 'SetBonus'], [0, 'ItemName']]);
                const list = [];
                for (let i = 0; i < numLines.value; i++) {
                    const line = new TooltipLine(special.get(i) || 'Line' + i, lines[i]);
                    line.IsModifier = !!pre[i];
                    line.IsModifierBad = !!bad[i];
                    line.OneDropLogo = i === yoyo.value;
                    list.push(line);
                }

                if (own) Safe.Run(own.constructor.name + '.ModifyTooltips', () => own.ModifyTooltips(item, list));
                globalItems.Each(item, 'ModifyTooltips', (g) => g.ModifyTooltips(item, list));

                // Fora do tooltip (guia de criação, busca) ninguém pinta: texto limpo.
                const colored = isDrawing();
                const count = Math.min(list.length, lines.length);
                for (let i = 0; i < count; i++) {
                    const line = list[i];
                    let text = String(line.Text);
                    if (line.OverrideColor) text = TooltipLine.colorTag(TooltipLoader.StripTags(text), line.OverrideColor);

                    lines[i] = colored ? text : TooltipLoader.StripTags(text);
                    pre[i] = !!line.IsModifier;
                    bad[i] = !!line.IsModifierBad;
                }
                numLines.value = count;

                const indexOf = (name) => {
                    const i = list.findIndex((l) => l.Name === name);
                    return i < count ? i : -1;
                };
                let logo = -1;
                for (let i = 0; i < count; i++) if (list[i].OneDropLogo) logo = i;

                yoyo.value = logo;
                research.value = indexOf('JourneyResearch');
                materials.value = indexOf('Material');
                setBonus.value = indexOf('SetBonus');
            });
    }

    // Dentro do tooltip: medida sem as tags, e cada trecho na cor dele.
    static #HookDrawing(drawTooltip) {
        const Graphics = Microsoft.Xna.Framework.Graphics;
        const measure = 'Vector2 MeasureString(string text)';
        const { HasTags, StripTags } = TooltipLoader;

        Graphics.SpriteFont[measure].hook((original, font, text) =>
            HasTags(text) ? original(font, StripTags(text)) : original(), { whileIn: drawTooltip });

        Graphics.SpriteBatch['void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
            (original, batch, font, text, pos, color, rotation, origin, scale, effects, depth) => {
                if (!HasTags(text)) return original();

                // A sombra fica preta; o texto pega a cor de cada trecho, com o
                // alfa da linha (as cores do jogo são pré-multiplicadas).
                const shadow = color.R === 0 && color.G === 0 && color.B === 0;
                const alpha = color.A / 255;
                let x = pos.X;
                for (const seg of TooltipLoader.Segments(text)) {
                    if (!seg.text) continue;

                    const c = !shadow && seg.rgb
                        ? Color.new(seg.rgb[0] * alpha, seg.rgb[1] * alpha, seg.rgb[2] * alpha, color.A)
                        : color;
                    original(batch, font, seg.text, Vector2.new(x, pos.Y), c, rotation, origin, scale, effects, depth);
                    x += font[measure](seg.text).X * scale;
                }
                return undefined;
            }, { whileIn: drawTooltip });
    }
}
