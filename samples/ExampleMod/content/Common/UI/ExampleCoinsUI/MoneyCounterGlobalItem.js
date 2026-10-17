import { ExampleCoinsUISystem } from './ExampleCoinsUISystem.js';

const { ItemID } = Terraria.ID;

// Conta as moedas que o jogador pega para a tela de moedas por minuto.
export class MoneyCounterGlobalItem extends GlobalItem {
    AppliesToEntity(item, lateInstantiation) {
        return item.type >= ItemID.CopperCoin && item.type <= ItemID.PlatinumCoin;
    }

    OnPickup(item, player) {
        ModContent.GetInstance(ExampleCoinsUISystem).exampleCoinsUI.UpdateValue(item.stack * (item.value / 5));
        return true;
    }
}
