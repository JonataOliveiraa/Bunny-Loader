const { ItemID, TileID } = Terraria.ID;

// Coloca o bloco de gemas aceso (o fio apaga).
export class ExampleGemsparkBlock extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleGemsparkBlockOn'));
    }

    AddRecipes() {
        this.CreateRecipe(20)
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddIngredient(ItemID.Glass, 20)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
