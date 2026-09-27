// A porta: coloca a fechada (ExampleDoorClosed).
export class ExampleDoor extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleDoorClosed'));
        this.Item.width = 14;
        this.Item.height = 28;
        this.Item.value = 150;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
