export class ExampleHamaxe extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Tools/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.melee = true;
        this.Item.axe = 30;
        this.Item.hammer = 100;
        this.SetWeaponValues(25, 6, 0);
        this.SetDefaultWeaponStyle(15, true);
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
