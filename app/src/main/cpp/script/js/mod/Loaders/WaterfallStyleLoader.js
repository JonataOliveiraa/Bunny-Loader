// As cachoeiras de mod, como o WaterFallStylesLoader do tModLoader: números
// depois dos 28 do jogo (WaterfallManager.maxTypes) e a textura no
// WaterfallManager.waterfallTexture, que cresce. O desenho é o do jogo
// (DrawWaterfall com o número); quem pede é o WaterStyleLoader, pela água
// visível. A cor e a luz de cada estilo o celular embute no desenho: a luz de
// mod (AddLight) roda depois, pelas cachoeiras que o jogo achou na tela.
class WaterfallStyleLoader {
    static List = [];
    static VanillaCount = 28;          // WaterfallManager.maxTypes
    static #installed = false;

    static get TotalCount() { return WaterfallStyleLoader.VanillaCount + WaterfallStyleLoader.List.length; }

    static Add(inst) {
        inst.Slot = WaterfallStyleLoader.TotalCount;
        const file = ModFiles.Texture(inst.Texture);
        inst.__file = bl.file.exists(file) ? bl.mod.path + '/' + file : null;
        WaterfallStyleLoader.List.push(inst);
        Ready.Add(WaterfallStyleLoader.#Install);
    }

    static Get(slot) {
        return WaterfallStyleLoader.List[slot - WaterfallStyleLoader.VanillaCount];
    }

    static #Install() {
        if (WaterfallStyleLoader.#installed) return;
        WaterfallStyleLoader.#installed = true;

        const WF = Terraria.WaterfallManager;
        WF.waterfallTexture = WF.waterfallTexture.cloneResized(WaterfallStyleLoader.TotalCount);
        for (const style of WaterfallStyleLoader.List) {
            if (!style.__file) bl.log(`cachoeira de mod ${style.constructor.name}: falta ${style.Texture}.png`);
            Safe.Run('cachoeira ' + style.constructor.name, () => {
                WF.waterfallTexture[style.Slot] = style.__file ? bl.loadTextureAsset(style.__file) : WF.waterfallTexture[0];
            });
        }
        bl.log('cachoeiras de mod: ' + WaterfallStyleLoader.List.length + ' (números ' + WaterfallStyleLoader.VanillaCount +
               '..' + (WaterfallStyleLoader.TotalCount - 1) + ')');
        if (WaterfallStyleLoader.List.some((s) => Hooks.Overrides(s.constructor, ModWaterfallStyle, 'ColorMultiplier'))) {
            WaterfallStyleLoader.#HookColor();
        }
    }

    // A cor da cachoeira de mod (ColorMultiplier), como o StylizeColor do
    // tModLoader. No celular o StylizeColor e o desenho de cada pedaço estão
    // embutidos no DrawWaterfall, que chama o SpriteBatch.Draw direto (três
    // sobrecargas: ref Color, ref VertexColors, a das quatro pontas, e Color).
    // O DrawWaterfall só entra no JS com o número de uma de mod (filtro
    // nativo), e os Draw só dentro dele: a cachoeira do jogo não paga nada.
    static #HookColor() {
        const WF = Terraria.WaterfallManager;
        const SB = Microsoft.Xna.Framework.Graphics.SpriteBatch;
        const gate = WF['void DrawWaterfall(SpriteBatch spriteBatch, int Style, float Alpha)'];
        let current = null, alpha = 1;
        gate.hook((original, self, spriteBatch, style, a) => {
            const outer = current, outerAlpha = alpha;
            const s = WaterfallStyleLoader.Get(style);
            current = s && Hooks.Overrides(s.constructor, ModWaterfallStyle, 'ColorMultiplier') ? s : null;
            alpha = a;
            try {
                return original(self, spriteBatch, style, a);
            } finally {
                current = outer;
                alpha = outerAlpha;
            }
        }, { minType: WaterfallStyleLoader.VanillaCount, arg: 1 });

        const byte = (v) => Math.max(0, Math.min(255, Math.trunc(Number(v) || 0)));
        const tint = (c) => {
            const r = new Ref(c.R), g = new Ref(c.G), b = new Ref(c.B);
            Safe.Run(current.constructor.name + '.ColorMultiplier', () => current.ColorMultiplier(r, g, b, alpha));
            return Color.new(byte(r.value), byte(g.value), byte(b.value), c.A);
        };
        const copy = (c) => Color.new(c.R, c.G, c.B, c.A);

        // A cor por ref é a variável do DrawWaterfall, reusada no pedaço
        // seguinte: muda só durante o Draw, e volta.
        SB['void Draw(Texture2D texture, ref Vector2 position, ref Rectangle srcRect, ref Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects)'].hook(
            (original, self, texture, position, srcRect, color, rotation, origin, scale, effects) => {
                if (!current) return original(self, texture, position, srcRect, color, rotation, origin, scale, effects);
                const saved = copy(color.value);
                color.value = tint(saved);
                try {
                    return original(self, texture, position, srcRect, color, rotation, origin, scale, effects);
                } finally {
                    color.value = saved;
                }
            }, { whileIn: gate });

        SB['void Draw(Texture2D texture, Vector2 position, ref Rectangle srcRect, ref VertexColors color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
            (original, self, texture, position, srcRect, colors, rotation, origin, scale, effects, depth) => {
                if (!current) return original(self, texture, position, srcRect, colors, rotation, origin, scale, effects, depth);
                const v = colors.value;
                const saved = [copy(v.TopLeftColor), copy(v.TopRightColor), copy(v.BottomLeftColor), copy(v.BottomRightColor)];
                v.TopLeftColor = tint(saved[0]);
                v.TopRightColor = tint(saved[1]);
                v.BottomLeftColor = tint(saved[2]);
                v.BottomRightColor = tint(saved[3]);
                colors.value = v;
                try {
                    return original(self, texture, position, srcRect, colors, rotation, origin, scale, effects, depth);
                } finally {
                    [v.TopLeftColor, v.TopRightColor, v.BottomLeftColor, v.BottomRightColor] = saved;
                    colors.value = v;
                }
            }, { whileIn: gate });

        SB['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
            (original, self, texture, position, source, color, rotation, origin, scale, effects, depth) =>
                original(self, texture, position, source, current ? tint(color) : color, rotation, origin, scale, effects, depth),
            { whileIn: gate });
    }

    // A luz das cachoeiras de água (tipo 0 na lista do jogo) desenhadas com
    // `waterfall`: um ponto a cada 3 tiles, até 24 tiles de queda.
    static Light(manager, waterfall) {
        const style = WaterfallStyleLoader.Get(waterfall);
        if (!style || !Hooks.Overrides(style.constructor, ModWaterfallStyle, 'AddLight')) return;

        const list = manager.waterfalls;
        const count = Math.min(manager.currentMax, list.length);
        Safe.Run(style.constructor.name + '.AddLight', () => {
            for (let k = 0; k < count; k++) {
                const w = list[k];
                if (w.type !== 0) continue;
                const steps = Math.min(w.stopAtStep, 24);
                for (let s = 0; s <= steps; s += 3) style.AddLight(w.x, w.y + s);
            }
        });
    }
}
