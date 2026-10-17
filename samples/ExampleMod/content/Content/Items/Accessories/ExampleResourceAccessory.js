import { ExampleResourcePlayer } from '../../../Common/Players/ExampleResourcePlayer.js';

// Aumenta o máximo do recurso de exemplo e a velocidade com que ele volta.
export class ExampleResourceAccessory extends ModItem {
    static ResourceBoost = 100;

    ModifyTooltipLines() {
        for (let i = 0; i < this.TooltipLines.length; i++) {
            this.TooltipLines[i] = this.TooltipLines[i].replace('{0}', ExampleResourceAccessory.ResourceBoost);
        }
    }

    SetDefaults() {
        this.Item.width = 26;
        this.Item.height = 32;
        this.Item.maxStack = 1;
        this.Item.value = Terraria.Item.sellPrice(0, 5, 0, 0);
        this.Item.accessory = true;
        this.Item.rare = ItemRarityID.Red;
    }

    UpdateAccessory(item, player, vanity, hideVisual) {
        if (vanity) return;
        const modPlayer = player.GetModPlayer(ExampleResourcePlayer);
        modPlayer.exampleResourceMax2 += ExampleResourceAccessory.ResourceBoost;   // +100 no máximo
        modPlayer.exampleResourceRegenRate *= 6;                                    // volta 6x mais rápido
    }
}
