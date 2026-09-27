// Planta a erva de exemplo (na grama, na grama sagrada ou no bloco de exemplo).
export class ExampleHerbSeeds extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleHerb'));
        this.Item.width = 12;
        this.Item.height = 14;
        this.Item.value = 80;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleTileItem'))
            .Register();
    }
}
