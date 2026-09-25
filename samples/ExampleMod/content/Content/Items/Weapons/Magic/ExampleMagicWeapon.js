const { SoundID } = Terraria.ID;

export class ExampleMagicWeapon extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Magic/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.magic = true;
        this.Item.mana = 10;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleAdvancedAnimatedProjectile');
        this.Item.shootSpeed = 15;
        this.SetWeaponValues(30, 3, 10);
        this.SetDefaultWeaponStyle(25, true);
        this.Item.rare = ItemRarityID.Green;
        this.Item.value = Terraria.Item.sellPrice(0, 2, 50, 0);
        this.Item.UseSound = SoundID.Item43;
    }
}
