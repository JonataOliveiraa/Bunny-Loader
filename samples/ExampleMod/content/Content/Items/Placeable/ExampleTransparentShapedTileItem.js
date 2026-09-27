export class ExampleTransparentShapedTileItem extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleTransparentShapedTile'));
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleTileItem'))
            .Register();
    }
}
