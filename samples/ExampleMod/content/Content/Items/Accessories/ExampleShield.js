import { ExampleDashPlayer } from '../../../Common/Players/ExampleDashPlayer.js';

export class ExampleShield extends ModItem {
    SetDefaults() {
        this.Item.width = 24;
        this.Item.height = 28;
        this.Item.value = Terraria.Item.buyPrice(10, 50, 0, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.accessory = true;
        this.Item.defense = 1000;
        this.Item.lifeRegen = 10;
    }

    UpdateAccessory(item, player, vanity, hideVisual) {
        if (vanity) return;
        player.GetModPlayer(ExampleDashPlayer).DashAccessoryEquipped = true;
    }
}
