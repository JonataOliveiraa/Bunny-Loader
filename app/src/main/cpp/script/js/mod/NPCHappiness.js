// Gostos do morador no banco de personalidades do próprio jogo: ele calcula
// felicidade e preço com eles.
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

    // 'Forest', 'Desert', 'Snow', 'Jungle', 'Ocean', 'Underground', 'Hallow',
    // 'Mushroom', 'Dungeon', 'Corruption', 'Crimson'.
    SetBiomeAffection(biome, level) {
        const P = Terraria.GameContent.Personalities;
        const name = String(biome).endsWith('Biome') ? String(biome) : biome + 'Biome';

        const shopping = P[name].new();
        shopping['void .ctor()']();

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
