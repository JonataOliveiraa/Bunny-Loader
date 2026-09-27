const { ItemID } = Terraria.ID;

export class ExampleWorkbench extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleWorkbench'));
        this.Item.width = 28;
        this.Item.height = 14;
        this.Item.value = 150;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ItemID.WorkBench)
            .AddIngredient(ModContent.ItemType('ExampleItem'), 10)
            .Register();
    }
}
