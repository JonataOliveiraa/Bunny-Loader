// O ModBiome no Bestiário, como o ModBiomeBestiaryInfoElement do tModLoader:
// na entrada de cada NPC com o bioma no SpawnModBiomes (o nome, o ícone e o
// fundo do retrato) e como filtro, para os biomas que algum NPC usa.
// A ponte não cria classe C#: o elemento é um SpawnConditionBestiaryInfoElement
// do jogo (o dos biomas do jogo) com a chave do DisplayName do bioma, e dois
// hooks trocam o que vem de arquivo do jogo: o ícone do filtro (GetDisplay,
// que a tela do celular usa) e o fundo do retrato (GetBackgroundImage).
// BestiaryIcon (30 x 30) e BackgroundPath (115 x 65) em Assets/Textures do mod;
// sem eles (ou em outro tamanho), o ícone de sombra e o fundo do jogo.
class BiomeBestiaryLoader {
    static #byAddress = new Map();   // endereço do elemento -> { biome, icon, background }
    static #elements = [];           // na ordem em que nasceram (a dos filtros)
    static #filtered = false;

    // O elemento do bioma (um por bioma), na thread do jogo.
    static Of(biome) {
        if (biome.__bestiary) return biome.__bestiary;

        const B = Terraria.GameContent.Bestiary;
        const element = B.SpawnConditionBestiaryInfoElement.new();
        // 64: o quadro de sombra do Icon_Tags_Shadow (16 x 5), o do tModLoader sem ícone.
        element['void .ctor(string nameLanguageKey, int filterIconFrame, string backgroundImagePath, Nullable`1 backgroundColor)'](
            biome.DisplayName.Key, 64, null, biome.BackgroundColor || null);
        biome.__bestiary = element;

        const art = {
            biome,
            icon: BiomeBestiaryLoader.#Load(biome, biome.BestiaryIcon, 30, 30),
            background: BiomeBestiaryLoader.#Load(biome, biome.BackgroundPath, 115, 65),
        };
        BiomeBestiaryLoader.#byAddress.set(bl.addressOf(element), art);
        BiomeBestiaryLoader.#elements.push(element);
        BiomeBestiaryLoader.#Hook();
        return element;
    }

    // Os ModBiome de um SpawnModBiomes (classes, instâncias ou Types).
    static Biomes(list) {
        if (!list) return [];
        const out = [];
        for (const which of list) {
            const biome = BiomeLoader.Resolve(which);
            if (biome && !out.includes(biome)) out.push(biome);
        }
        return out;
    }

    // Depois de todas as entradas: um filtro por bioma usado.
    static Finish() {
        if (BiomeBestiaryLoader.#filtered || !BiomeBestiaryLoader.#elements.length) return;
        BiomeBestiaryLoader.#filtered = true;

        const B = Terraria.GameContent.Bestiary;
        const db = Terraria.Main.BestiaryDB;
        for (const element of BiomeBestiaryLoader.#elements) {
            const filter = B.Filters.ByInfoElement.new();
            filter['void .ctor(IBestiaryInfoElement element)'](element);
            db['IBestiaryEntryFilter Register(IBestiaryEntryFilter filter)'](filter);
        }
        bl.log('Bestiario: ' + BiomeBestiaryLoader.#elements.length + ' bioma(s) de mod nos filtros');
    }

    // A textura do mod, ou null (sem o arquivo ou em outro tamanho).
    static #Load(biome, path, width, height) {
        if (typeof path !== 'string' || !path || !biome.Mod) return null;
        const name = ModFiles.Texture(ModFiles.TextureName(path));
        try {
            const asset = bl.loadTextureAsset(biome.Mod.path + '/' + name);
            const texture = asset.Value;
            if (texture.Width === width && texture.Height === height) return { asset, texture };
            bl.log(`${biome.constructor.name}: ${name} precisa ter ${width} x ${height} (tem ${texture.Width} x ${texture.Height})`);
        } catch (e) {
            // sem o arquivo: o do jogo, como o tModLoader
        }
        return null;
    }

    static #Hook() {
        Hooks.Once('biome.bestiary', () => {
            const B = Terraria.GameContent.Bestiary;
            B.FilterProviderInfoElement['void GetDisplay(out Texture2D texture, out Rectangle frame)'].hook(
                (original, self, texture, frame) => {
                    original(self, texture, frame);
                    const art = BiomeBestiaryLoader.#byAddress.get(bl.addressOf(self));
                    if (!art || !art.icon) return;
                    texture.value = art.icon.texture;
                    frame.value = Rectangle.new(0, 0, 30, 30);
                });
            B.SpawnConditionBestiaryInfoElement['Asset`1 GetBackgroundImage()'].hook((original, self) => {
                const art = BiomeBestiaryLoader.#byAddress.get(bl.addressOf(self));
                return art && art.background ? art.background.asset : original(self);
            });
        });
    }
}
