export class ExampleWideBanner extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleWideBannerTile'));
        this.Item.value = ModItem.buyPrice(0, 0, 0, 10);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
