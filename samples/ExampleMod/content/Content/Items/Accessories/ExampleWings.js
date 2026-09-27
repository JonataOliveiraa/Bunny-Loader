export class ExampleWings extends ModItem {
    // Tempo de voo, velocidade no ar e aceleração: o WingStats(180, 9, 2.5) do tModLoader.
    SetStaticDefaults() {
        this.SetWingStats(180, 9, 2.5);
    }

    SetDefaults() {
        this.Item.width = 22;
        this.Item.height = 20;
        this.Item.value = 10000;
        this.Item.rare = ItemRarityID.Green;
        this.Item.accessory = true;
    }

    VerticalWingSpeeds(item, player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier, maxAscentMultiplier, constantAscend) {
        ascentWhenFalling.value = 0.85;
        ascentWhenRising.value = 0.15;
        maxCanAscendMultiplier.value = 1;
        maxAscentMultiplier.value = 3;
        constantAscend.value = 0.135;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
