// O troféu do Olho de ??? (cai dele, 1 em 10).
export class MinionBossTrophy extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('MinionBossTrophy'));
        this.Item.width = 32;
        this.Item.height = 32;
        this.Item.rare = ItemRarityID.Blue;
        this.Item.value = ModItem.buyPrice(0, 1, 0, 0);
    }
}
