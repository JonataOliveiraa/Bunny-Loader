const { ItemID, TileID } = Terraria.ID;

export class ExampleToilet extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleToilet'));
        this.Item.width = 16;
        this.Item.height = 24;
        this.Item.value = 150;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ItemID.Toilet)
            .AddIngredient(ModContent.ItemType('ExampleItem'), 10)
            .AddTile(TileID.Anvils)
            .Register();
    }
}
