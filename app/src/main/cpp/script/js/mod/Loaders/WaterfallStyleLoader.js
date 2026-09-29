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
