// O item que coloca o ExamplePylonTile. Os moradores o vendem como os
// pilares do jogo (Common/GlobalNPCs/ExamplePylonShop.js).
export class ExamplePylonItem extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModTile.getTypeByName('ExamplePylonTile'));
        this.Item.rare = ItemRarityID.Blue;
        this.Item.value = Terraria.Item.buyPrice(0, 10, 0, 0);
    }
}
