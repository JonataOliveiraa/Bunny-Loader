const { ItemID } = Terraria.ID;

export class ExampleDresser extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleDresser'));
        this.Item.width = 26;
        this.Item.height = 22;
        this.Item.value = 500;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ItemID.Dresser)
            .AddIngredient(ModContent.ItemType('ExampleTileItem'), 10)
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
