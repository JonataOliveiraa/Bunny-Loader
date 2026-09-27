export class ExampleCampfire extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleCampfire'), 0);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddRecipeGroup('Wood', 10)
            .AddIngredient(ModContent.ItemType('ExampleTorch'), 5)
            .Register();
    }
}
