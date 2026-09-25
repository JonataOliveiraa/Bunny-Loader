const { AmmoID } = Terraria.ID;

export class ExampleBullet extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Ammo/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.damage = 12;
        this.Item.ranged = true;
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.consumable = true;
        this.Item.knockBack = 1.0;
        this.Item.value = Terraria.Item.sellPrice(0, 0, 10, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleBulletProjectile');
        this.Item.shootSpeed = 4.5;
        this.Item.ammo = AmmoID.Bullet;
    }
}
