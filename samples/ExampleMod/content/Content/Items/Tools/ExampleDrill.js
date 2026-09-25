const { ItemID, SoundID } = Terraria.ID;

export class ExampleDrill extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Tools/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ItemID.Sets.IsDrill[this.Type] = true;
    }

    SetDefaults() {
        this.Item.damage = 27;
        this.Item.melee = true;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleDrillProjectile');
        this.Item.shootSpeed = 32;
        this.Item.useStyle = Terraria.ID.ItemUseStyleID.Shoot;
        this.Item.useTime = 4;
        this.Item.useAnimation = 15;
        this.Item.noMelee = true;
        this.Item.noUseGraphic = true;
        this.Item.channel = true;
        this.Item.pick = 190;
        this.Item.tileBoost = 10;
        this.Item.value = Terraria.Item.sellPrice(0, 12, 60, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.UseSound = SoundID.Item23;
    }
}
