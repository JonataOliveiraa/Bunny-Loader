const { ItemID } = Terraria.ID;

// O pote de vaga-lume do jogo, colocando o tile animado de exemplo.
export class ExampleAnimatedTileItem extends ModItem {
    SetDefaults() {
        this.CloneDefaults(ItemID.FireflyinaBottle);
        this.Item.createTile = ModContent.TileType('ExampleAnimatedTile');
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
