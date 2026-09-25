const { ItemID, ItemUseStyleID, SoundID } = Terraria.ID;

export class ExampleFlail extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Melee/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ItemID.Sets.ToolTipDamageMultiplier[this.Type] = 2;
    }

    SetDefaults() {
        this.Item.width = 30;
        this.Item.height = 10;
        this.Item.melee = true;
        this.Item.noMelee = true;
        this.Item.channel = true;
        this.Item.scale = 1.1;
        this.Item.value = Terraria.Item.sellPrice(0, 2, 50, 0);
        this.Item.rare = ItemRarityID.Orange;
        this.Item.UseSound = SoundID.Item1;
        this.SetWeaponValues(32, 6.75, 7);
        this.SetDefaultWeaponStyle(45, false);
        this.Item.useStyle = ItemUseStyleID.Shoot;
        this.Item.noUseGraphic = true;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleFlailProjectile');
        this.Item.shootSpeed = 12;
    }
}
