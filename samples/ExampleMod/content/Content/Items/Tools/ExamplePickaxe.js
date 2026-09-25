export class ExamplePickaxe extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Tools/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.melee = true;
        this.Item.pick = 220;
        this.SetWeaponValues(20, 6, 0);
        this.SetDefaultWeaponStyle(10, true);
        this.Item.value = Terraria.Item.sellPrice(0, 1, 0, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.UseSound = Terraria.ID.SoundID.Item1;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'), 10)
            .AddTile(Terraria.ID.TileID.WorkBenches)
            .Register();
    }
}
