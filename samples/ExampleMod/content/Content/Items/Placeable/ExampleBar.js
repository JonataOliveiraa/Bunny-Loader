const { ItemID, TileID } = Terraria.ID;

export class ExampleBar extends ModItem {
    SetStaticDefaults() {
        ItemID.Sets.SortingPriorityMaterials[this.Type] = 59;   // logo depois da barra de platina
    }

    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleBar'));
        this.Item.width = 20;
        this.Item.height = 20;
        this.Item.value = 750;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleOreItem'), 4)
            .AddTile(TileID.Furnaces)
            .Register();
    }
}
