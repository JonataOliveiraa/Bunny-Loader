// A máscara do chefe de exemplo (MinionBossMask no tModLoader): só visual.
export class ExampleBossMask extends ModItem {
    SetDefaults() {
        this.Item.width = 22;
        this.Item.height = 28;
        this.Item.rare = ItemRarityID.Blue;
        this.Item.value = Terraria.Item.sellPrice(0, 0, 75, 0);
        this.Item.vanity = true;
        this.Item.maxStack = 1;
    }
}
