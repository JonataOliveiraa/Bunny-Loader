// A relíquia do Olho de ??? (cai dele no modo mestre).
export class MinionBossRelic extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('MinionBossRelic'), 0);
        this.Item.width = 30;
        this.Item.height = 40;
        this.Item.rare = ItemRarityID.Master;
        this.Item.value = ModItem.buyPrice(0, 5, 0, 0);
    }
}
