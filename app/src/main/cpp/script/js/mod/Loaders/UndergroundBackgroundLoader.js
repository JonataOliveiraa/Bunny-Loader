// Os fundos de subsolo de mod, como o UndergroundBackgroundStylesLoader do
// tModLoader, no fluxo do celular (medido na B5.0):
//   - DrawBackground_PickUndergroundBackgroundStyle: o estilo da cena entra
//     pela Priority, nos degraus do tModLoader (BiomeLow só no lugar da caverna
//     comum; BiomeMedium também da neve e da selva; BiomeHigh de tudo);
//   - o DrawBackground do celular monta, para o estilo atual e o anterior (a
//     transição), 7 números de textura (backTexture) e as Texture2D deles
//     (backTextureValues), e desenha dali, também nas threads de desenho. Um
//     estilo que o jogo não conhece sai com tudo 0. Os getters desses arrays
//     trocam as 5 primeiras entradas pelas do FillTextureArray. O jogo escreve
//     a posição k logo depois de pedir o array, então a troca confere a
//     posição 3 (a última das do mod) a cada pedido.
// Com o fundo desligado (Main.BackgroundEnabled) o jogo usa o desenho antigo,
// sem esses arrays: lá o estilo de mod não entra.
class UndergroundBackgroundLoader {
    static List = [];
    static #fills = new Map();   // estilo -> { slots, textures } deste desenho

    static VanillaCount = 22;

    static Add(inst) {
        inst.Slot = UndergroundBackgroundLoader.VanillaCount + UndergroundBackgroundLoader.List.length;
        UndergroundBackgroundLoader.List.push(inst);
        UndergroundBackgroundLoader.#Install();
    }

    static Get(style) {
        return UndergroundBackgroundLoader.List[style - UndergroundBackgroundLoader.VanillaCount];
    }

    // O estilo da caverna comum naquele X (o começo da escolha do jogo).
    static #Cave() {
        const Main = Terraria.Main;
        const x = Math.floor((Main.screenPosition.X + Math.floor(Main.screenWidth / 2)) / 16);
        const X = Main.caveBackX, S = Main.caveBackStyle;
        return (x > X[0] ? (x > X[1] ? (x > X[2] ? S[3] : S[2]) : S[1]) : S[0]) + 3;
    }

    static #Choose(vanilla) {
        const Main = Terraria.Main;
        if (Main.gameMenu || !Main.BackgroundEnabled) return vanilla;

        const scene = SceneEffectLoader.Of(Main.LocalPlayer).undergroundBackground;
        if (!scene || !UndergroundBackgroundLoader.Get(scene.value)) return vanilla;

        const P = SceneEffectPriority;
        if (scene.priority >= P.BiomeHigh) return scene.value;
        const cave = vanilla === UndergroundBackgroundLoader.#Cave();
        if (scene.priority >= P.BiomeMedium && (cave || vanilla === 1 || vanilla === 11)) return scene.value;
        if (scene.priority >= P.BiomeLow && cave) return scene.value;
        return vanilla;
    }

    // As texturas do estilo neste desenho: o FillTextureArray uma vez por
    // DrawBackground. [4] (a passagem para o inferno) vem com a da caverna do
    // jogo (a do estilo 0), a não ser que o mod escreva.
    static #Fill(style) {
        let fill = UndergroundBackgroundLoader.#fills.get(style);
        if (fill !== undefined) return fill;

        fill = null;
        const inst = UndergroundBackgroundLoader.Get(style);
        if (inst) {
            const slots = [-1, -1, -1, -1, 6];
            Safe.Run(inst.constructor.name + '.FillTextureArray', () => inst.FillTextureArray(slots));
            const Textures = Terraria.GameContent.TextureAssets;
            const textures = slots.map((slot) => {
                if (!Number.isInteger(slot) || slot < 0 || slot >= Textures.Background.length) return null;
                const asset = Textures.Background[slot];
                return asset ? asset.Value : null;
            });
            if (textures.some((t) => t)) fill = { slots, textures };
        }
        UndergroundBackgroundLoader.#fills.set(style, fill);
        return fill;
    }

    static #Patch(style, numbers, values) {
        const fill = UndergroundBackgroundLoader.#Fill(style);
        if (!fill || !values || values.length < 5) return;
        // A posição 3 é a última das do mod que o jogo reescreve.
        const last = fill.textures[3] ? 3 : fill.textures.findLastIndex((t) => t);
        if (values[last] === fill.textures[last]) return;

        for (let k = 0; k < 5; k++) {
            const texture = fill.textures[k];
            if (!texture) continue;
            values[k] = texture;
            numbers[k] = fill.slots[k];
        }
    }

    static #Install() {
        Hooks.Once('bg.underground', () => {
            const Main = Terraria.Main;

            Main['int DrawBackground_PickUndergroundBackgroundStyle(double magmaLayer)'].hook((original, magma) => {
                const vanilla = original(magma);
                UndergroundBackgroundLoader.#fills.clear();
                return Safe.Run('fundo do subsolo: escolha', () => UndergroundBackgroundLoader.#Choose(vanilla)) ?? vanilla;
            });

            Main['Texture2D[] get_backTextureValues()'].hook((original, self) => {
                const values = original(self);
                const style = Main.undergroundBackground;
                if (style >= UndergroundBackgroundLoader.VanillaCount) {
                    Safe.Run('fundo do subsolo', () => UndergroundBackgroundLoader.#Patch(style, LocalUserGameState.Instance.backTexture, values));
                }
                return values;
            });
            Main['Texture2D[] get_oldBackTextureValues()'].hook((original, self) => {
                const values = original(self);
                const style = Main.oldUndergroundBackground;
                if (style >= UndergroundBackgroundLoader.VanillaCount) {
                    Safe.Run('fundo do subsolo (anterior)', () => UndergroundBackgroundLoader.#Patch(style, LocalUserGameState.Instance.oldBackTexture, values));
                }
                return values;
            });
        });
    }
}
