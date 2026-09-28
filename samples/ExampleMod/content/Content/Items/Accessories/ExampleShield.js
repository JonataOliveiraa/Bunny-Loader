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

    // Um escudo só: com outro escudo, a troca pelo toque vai para o slot dele.
    CanAccessoryBeEquippedWith(equippedItem, incomingItem, player) {
        return !(equippedItem.shieldSlot > 0 && incomingItem.shieldSlot > 0);
    }

    UpdateAccessory(item, player, vanity, hideVisual) {
        if (vanity) return;
        player.GetModPlayer(ExampleDashPlayer).DashAccessoryEquipped = true;
    }
}
