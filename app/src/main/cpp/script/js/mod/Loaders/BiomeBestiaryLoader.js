// O ModBiome no Bestiário, como o ModBiomeBestiaryInfoElement do tModLoader:
// na entrada de cada NPC com o bioma no SpawnModBiomes (o nome, o ícone e o
// fundo do retrato) e como filtro, para os biomas que algum NPC usa.
// A ponte não cria classe C#: o elemento é um SpawnConditionBestiaryInfoElement
// do jogo (o dos biomas do jogo) com a chave do DisplayName do bioma, e hooks
// trocam o que vem de arquivo do jogo: o ícone do filtro (GetDisplay), o da
// lista "Surge em" da entrada (GUIBestiary.SpawnDraw, que desenha direto do
// Icon_Tags_Shadow pelo quadro, sem o GetDisplay) e o fundo do retrato
// (GetBackgroundImage).
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
            // O SpawnDraw do celular, para o bioma com ícone: o mesmo fundo, o
            // ícone do mod no lugar do quadro de sombra ("?") e o nome.
            GUIBestiary['void SpawnDraw(ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)'].hook(
                (original, self, gridLayout, index, position, scale) => {
                    const list = self.FilterProviders;
                    const element = list && index < list.Count ? list.get_Item(index) : null;
                    const art = element && BiomeBestiaryLoader.#byAddress.get(bl.addressOf(element));
                    if (!art || !art.icon) return original(self, gridLayout, index, position, scale);

                    const layout = Bestiary_Layout.Instance;
                    const backing = layout.EntrySpawnBacking;
                    const size = backing.Size;
                    ControlAnchor.SetGridItemRegion(Rectangle.new(position.X | 0, position.Y | 0, size.X | 0, size.Y | 0));
                    GUIPanel['void DrawAtPosition(Panel_Layout layout, Vector2 position, Vector2 size, bool cursorOver)'](backing, position, size, false);
                    const region = GUIPanel.Region(layout.EntrySpawnIcon);
                    Terraria.Main.spriteBatch['void Draw(Texture2D texture, Rectangle destinationRectangle, Color color)'](
                        art.icon.texture, region, Color.White);
                    GUIString['void Draw(String_Layout layout, string value)'](
                        layout.EntrySpawnType, Terraria.Localization.Language['string GetTextValue(string key)'](element._key));
                });
            B.SpawnConditionBestiaryInfoElement['Asset`1 GetBackgroundImage()'].hook((original, self) => {
                const art = BiomeBestiaryLoader.#byAddress.get(bl.addressOf(self));
                return art && art.background ? art.background.asset : original(self);
            });
        });
    }
}
