const { AmmoID, ProjectileID, SoundID } = Terraria.ID;

export class ExampleRocketLauncher extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Ranged/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.ranged = true;
        this.Item.shoot = ProjectileID.RocketI;
        this.Item.shootSpeed = 5;
        this.Item.useAmmo = AmmoID.Rocket;
        this.SetWeaponValues(55, 4, 0);
        this.SetDefaultWeaponStyle(30, true);
        this.Item.noMelee = true;
        this.Item.rare = ItemRarityID.Yellow;
        this.Item.value = Terraria.Item.buyPrice(0, 40, 0, 0);
        this.Item.UseSound = SoundID.Item11;
    }

    HoldoutOffset() {
        return { X: -2, Y: -2 };
    }
}
