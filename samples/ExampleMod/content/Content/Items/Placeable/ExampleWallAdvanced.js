export class ExampleWallAdvanced extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableWall(ModContent.WallType('ExampleWallAdvanced'));
    }

    AddRecipes() {
        this.CreateRecipe(4)
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
