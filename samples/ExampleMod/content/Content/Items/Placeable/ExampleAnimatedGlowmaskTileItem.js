const { ItemID } = Terraria.ID;

// O monólito do vazio do jogo, colocando o monólito de exemplo.
export class ExampleAnimatedGlowmaskTileItem extends ModItem {
    SetDefaults() {
        this.CloneDefaults(ItemID.VoidMonolith);
        this.Item.createTile = ModContent.TileType('ExampleAnimatedGlowmaskTile');
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
