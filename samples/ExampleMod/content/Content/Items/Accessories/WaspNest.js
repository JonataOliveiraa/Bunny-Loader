export class WaspNest extends ModItem {
    SetDefaults() {
        this.Item.width = 26;
        this.Item.height = 26;
        this.Item.accessory = true;
        this.Item.value = Terraria.Item.sellPrice(0, 2, 0, 0);
        this.Item.rare = ItemRarityID.Green;
    }

    UpdateAccessory(item, player, vanity, hideVisual) {
        if (vanity) return;
        player.strongBees = true;
    }
}
