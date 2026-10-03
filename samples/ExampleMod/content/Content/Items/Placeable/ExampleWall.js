export class ExampleWall extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableWall(ModContent.WallType('ExampleWall'));
    }

    AddRecipes() {
        this.CreateRecipe(4)
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
