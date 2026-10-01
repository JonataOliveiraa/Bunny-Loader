import { ExampleModRarity } from './ExampleModRarity.js';

// A raridade de cima da ExampleModRarity, com a cor piscando (o Disco do jogo).
// Com prefixo ruim, o item desce para a de baixo.
export class ExampleHigherTierModRarity extends ModRarity {
    get RarityColor() {
        const Main = Terraria.Main;
        return Color.new(Main.DiscoR / 2 | 0, Main.DiscoG / 1.25 | 0, Main.DiscoB / 1.5 | 0);
    }

    GetPrefixedRarity(offset, valueMult) {
        // offset -1 ou -2: prefixo ruim.
        if (offset < 0) return ModContent.RarityType(ExampleModRarity);
        return this.Type;
    }
}
