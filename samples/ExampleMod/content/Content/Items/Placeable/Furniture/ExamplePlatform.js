export class ExamplePlatform extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExamplePlatform'));
        this.Item.width = 8;
        this.Item.height = 10;
    }

    AddRecipes() {
        this.CreateRecipe(2)
            .AddIngredient(ModContent.ItemType('ExampleTileItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
