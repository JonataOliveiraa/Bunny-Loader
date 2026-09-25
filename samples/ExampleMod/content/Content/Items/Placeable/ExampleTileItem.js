export class ExampleTileItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Placeable/' + this.constructor.name;
    }

    SetDefaults() {
        this.DefaultToPlaceableTile(ModTile.getTypeByName('ExampleTile'));
        this.Item.width = 12;
        this.Item.height = 12;
    }

    AddRecipes() {
        this.CreateRecipe(10)
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(Terraria.ID.TileID.WorkBenches)
            .Register();
    }
}
