export class ExampleStatAccessory extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Accessories/' + this.constructor.name;
        this.ExtraDamagePercent = 10;
        this.ExtraDamageMultiplier = 1 + this.ExtraDamagePercent / 100;
    }

    ModifyTooltipLines() {
        for (let i = 0; i < this.TooltipLines.length; i++) {
            this.TooltipLines[i] = this.TooltipLines[i].replace('{0}', this.ExtraDamagePercent);
        }
    }

    SetDefaults() {
        this.Item.width = 28;
        this.Item.height = 28;
        this.Item.accessory = true;
        this.Item.value = Terraria.Item.sellPrice(0, 5, 0, 0);
        this.Item.rare = ItemRarityID.Green;
    }

    UpdateAccessory(item, player, vanity, hideVisual) {
        if (vanity) return;
        player.meleeDamage *= this.ExtraDamageMultiplier;
        player.rangedDamage *= this.ExtraDamageMultiplier;
        player.magicDamage *= this.ExtraDamageMultiplier;
    }
}
