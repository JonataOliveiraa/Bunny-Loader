// O ModBiome na felicidade dos moradores, como o IShoppingBiome do tModLoader.
// O banco de personalidades do jogo guarda AShoppingBiome (abstrata, com
// IsInBiome(player) e NameKey), e a ponte não cria subclasse C#: cada bioma de
// mod ganha um procurador, um MushroomBiome do jogo com o NameKey do bioma, e
// o IsInBiome do MushroomBiome responde pelo bioma quando é um procurador (o
// resultado do IsBiomeActive do jogador, como o tModLoader). A fala lê
// TownNPCMoodBiomes.<NameKey>, que o BiomeLoader registra com o
// TownNPCDialogueName.
class BiomeShoppingLoader {
    static #byAddress = new Map();   // endereço do procurador -> ModBiome

    // O AShoppingBiome do bioma de mod (um por bioma).
    static Of(biome) {
        if (biome.__shopping) return biome.__shopping;

        const P = Terraria.GameContent.Personalities;
        const proxy = P.MushroomBiome.new();
        proxy['void .ctor()']();
        proxy.NameKey = biome.ShoppingNameKey;
        biome.__shopping = proxy;
        BiomeShoppingLoader.#byAddress.set(bl.addressOf(proxy), biome);

        Hooks.Once('biome.shopping', () => {
            P.MushroomBiome['bool IsInBiome(Player player)'].hook((original, self, player) => {
                const own = BiomeShoppingLoader.#byAddress.get(bl.addressOf(self));
                if (!own) return original(self, player);
                return !!(player && Safe.Run(own.constructor.name + ' (felicidade)', () => own.IsSceneEffectActive(player)));
            });
        });
        return proxy;
    }
}
