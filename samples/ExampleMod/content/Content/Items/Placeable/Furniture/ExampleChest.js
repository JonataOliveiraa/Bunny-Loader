const { ItemID } = Terraria.ID;

export class ExampleChest extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleChest'));
        this.Item.width = 26;
        this.Item.height = 22;
        this.Item.value = 500;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}

// A chave do baú trancado (o estilo 1, que só abre de noite).
export class ExampleChestKey extends ModItem {
    SetDefaults() {
        this.CloneDefaults(ItemID.GoldenKey);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ItemID.GoldenKey)
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .Register();
    }
}
