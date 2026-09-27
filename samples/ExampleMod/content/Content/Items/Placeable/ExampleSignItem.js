const { ItemID } = Terraria.ID;

export class ExampleSignItem extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleSign'), 0);
        this.Item.width = 26;
        this.Item.height = 22;
        this.Item.value = 50;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ItemID.Sign)
            .AddIngredient(ModContent.ItemType('ExampleItem'), 10)
            .Register();
    }
}
