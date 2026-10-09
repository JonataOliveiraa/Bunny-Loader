// A cada 50 slimes, o sistema de banners do Terraria libera este item.
export class ExampleSlimeBanner extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleEnemyBannerTile'), 1);
        this.Item.width = 10;
        this.Item.height = 24;
        this.Item.value = ModItem.buyPrice(0, 0, 0, 10);
    }
}
