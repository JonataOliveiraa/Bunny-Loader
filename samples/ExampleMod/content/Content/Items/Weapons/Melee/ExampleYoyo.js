const { ItemID, SoundID } = Terraria.ID;

export class ExampleYoyo extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Melee/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ItemID.Sets.Yoyo[this.Type] = true;
        ItemID.Sets.GamepadExtraRange[this.Type] = 15;
    }

    SetDefaults() {
        this.Item.melee = true;
        this.Item.noMelee = true;
        this.Item.noUseGraphic = true;
        this.Item.channel = true;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleYoyoProjectile');
        this.Item.shootSpeed = 15;
        this.SetWeaponValues(50, 2.5, 8);
        this.SetDefaultWeaponStyle(25, true);
        this.Item.useStyle = Terraria.ID.ItemUseStyleID.Shoot;
        this.Item.value = Terraria.Item.sellPrice(0, 1, 0, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.UseSound = SoundID.Item1;
    }

    ModifyTooltips(item, tooltips) {
        const logo = new TooltipLine('OneDropLogo', '');
        logo.OneDropLogo = true;
        tooltips.push(logo);
    }
}
