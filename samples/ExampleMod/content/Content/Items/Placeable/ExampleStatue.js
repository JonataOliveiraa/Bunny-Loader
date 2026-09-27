const { ItemID } = Terraria.ID;

// A estátua de armadura do jogo, colocando a estátua de exemplo.
export class ExampleStatue extends ModItem {
    SetDefaults() {
        this.CloneDefaults(ItemID.ArmorStatue);
        this.Item.createTile = ModContent.TileType('ExampleStatue');
        this.Item.placeStyle = 0;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
