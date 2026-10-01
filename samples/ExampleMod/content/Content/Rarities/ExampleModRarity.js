import { ExampleHigherTierModRarity } from './ExampleHigherTierModRarity.js';

// Uma raridade de mod: a cor do nome do item (no tooltip, no chão, no texto
// que sobe ao pegar). Com prefixo bom, o item sobe para a raridade de cima.
export class ExampleModRarity extends ModRarity {
    get RarityColor() {
        return Color.new(200, 215, 230);
    }

    GetPrefixedRarity(offset, valueMult) {
        // offset 1 ou 2: prefixo bom.
        if (offset > 0) return ModContent.RarityType(ExampleHigherTierModRarity);
        return this.Type;
    }
}
