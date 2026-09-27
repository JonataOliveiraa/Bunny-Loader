const { ItemID, TileID } = Terraria.ID;

export class ExampleCutTile extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleCutTile'));
        this.Item.value = ModItem.buyPrice(0, 0, 10, 0);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ItemID.StoneBlock, 10)
            .AddIngredient(ItemID.Rope, 10)
            .AddTile(TileID.HeavyWorkBench)
            .Register();
    }
}
