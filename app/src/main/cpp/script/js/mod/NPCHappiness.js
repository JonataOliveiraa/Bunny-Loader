class NPCHappiness {
    constructor(npcType) {
        this.NpcType = npcType;
    }

    SetNPCAffection(npcType, level) {
        const trait = Terraria.GameContent.Personalities.NPCPreferenceTrait.new();
        trait['void .ctor()']();
        trait.Level = level;
        trait.NpcId = npcType;

        NPCHappiness.#Register(this.NpcType, trait);
        return this;
    }

    SetBiomeAffection(biome, level) {
        const P = Terraria.GameContent.Personalities;
        let shopping;
        if (typeof biome === 'string') {
            const name = biome.endsWith('Biome') ? biome : biome + 'Biome';
            shopping = P[name].new();
            shopping['void .ctor()']();
        } else {
            const own = BiomeLoader.Resolve(biome);
            if (!own) throw new TypeError('SetBiomeAffection: passe o nome de um bioma do jogo ou um ModBiome registrado');
            shopping = BiomeShoppingLoader.Of(own);
        }

        const preference = P.BiomePreferenceListTrait.BiomePreference.new();
        preference['void .ctor(AffectionLevel affection, AShoppingBiome biome)'](level, shopping);

        const list = P.BiomePreferenceListTrait.new();
        list['void .ctor()']();
        list['void Add(BiomePreferenceListTrait.BiomePreference preference)'](preference);

        NPCHappiness.#Register(this.NpcType, list);
        return this;
    }

    static #Register(npcType, trait) {
        Terraria.Main.ShopHelper._database['void Register(int npcId, IShopPersonalityTrait trait)'](npcType, trait);
    }
}
