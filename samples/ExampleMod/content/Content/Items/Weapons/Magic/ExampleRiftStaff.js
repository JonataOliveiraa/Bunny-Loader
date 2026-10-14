const { SoundID } = Terraria.ID;

// Cajado que abre uma fenda (ExampleRiftProjectile): o exemplo dos efeitos de
// tela de mod, com shader próprio (.frag), distorção e os prontos.
export class ExampleRiftStaff extends ModItem {
    get Texture() { return 'Content/Items/Weapons/Magic/ExampleMagicWeapon'; }

    SetDefaults() {
        this.Item.magic = true;
        this.Item.mana = 20;
        this.Item.shoot = ModContent.ProjectileType('ExampleRiftProjectile');
        this.Item.shootSpeed = 9;
        this.SetWeaponValues(35, 2, 4);
        this.SetDefaultWeaponStyle(40, true);
        this.Item.rare = ItemRarityID.LightRed;
        this.Item.value = Terraria.Item.sellPrice(0, 3, 0, 0);
        this.Item.UseSound = SoundID.Item8;
        this.Item.color = Color.new(200, 140, 255);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'), 10)
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
