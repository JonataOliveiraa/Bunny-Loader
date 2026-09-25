export class ExampleOreItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Placeable/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.ID.ItemID.Sets.SortingPriorityMaterials[this.Type] = 58;
    }

    SetDefaults() {
        this.DefaultToPlaceableTile(ModTile.getTypeByName('ExampleOre'));
        this.Item.width = 12;
        this.Item.height = 12;
        this.Item.value = Terraria.Item.sellPrice(0, 0, 30, 0);
        this.Item.rare = ItemRarityID.Green;
    }
}
