const { ItemID } = Terraria.ID;

export class ExampleTable extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleTable'));
        this.Item.width = 38;
        this.Item.height = 24;
        this.Item.value = 150;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ItemID.WoodenTable)
            .AddIngredient(ModContent.ItemType('ExampleItem'), 10)
            .Register();
    }
}
