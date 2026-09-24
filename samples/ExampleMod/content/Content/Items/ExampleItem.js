export class ExampleItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.value = Terraria.Item.buyPrice(0, 0, 1, 0);
    }

    AddRecipes() {
        this.CreateRecipe(999)
            .AddIngredient(Terraria.ID.ItemID.DirtBlock, 10)
            .AddTile(Terraria.ID.TileID.WorkBenches)
            .Register();
    }
}
