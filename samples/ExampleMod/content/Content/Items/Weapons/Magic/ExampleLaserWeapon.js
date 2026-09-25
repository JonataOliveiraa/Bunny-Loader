const { ItemID } = Terraria.ID;

export class ExampleLaserWeapon extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Magic/' + this.constructor.name;
    }

    SetDefaults() {
        this.CloneDefaults(ItemID.LastPrism);
        this.Item.mana = 4;
        this.Item.damage = 42;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleLaserHoldout');
        this.Item.shootSpeed = 1;
        this.Item.color = Color.White;
    }

    CanUseItem(item, player) {
        return player.ownedProjectileCounts[item.shoot] < 1;
    }
}
