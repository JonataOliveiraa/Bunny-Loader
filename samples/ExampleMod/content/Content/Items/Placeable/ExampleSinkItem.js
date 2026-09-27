export class ExampleSinkItem extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModTile.getTypeByName('ExampleSink'));
        this.Item.width = 24;
        this.Item.height = 30;
        this.Item.value = 3000;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(Terraria.ID.TileID.WorkBenches)
            .Register();
    }
}
