import { RARITY_GREEN } from '../../Rarity.js';
import { ExampleBulletProjectile } from '../../Projectiles/ExampleBulletProjectile.js';

const { AmmoID } = Terraria.ID;

// ExampleBullet: a municao. Quem define o projetil e ELA: a arma pede "bala"
// (useAmmo) e o jogo dispara o `shoot` da bala que achar no inventario.
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
        this.Item.rare = RARITY_GREEN;
        this.Item.shoot = ExampleBulletProjectile;
        this.Item.shootSpeed = 4.5;
        this.Item.ammo = AmmoID.Bullet;
    }
}
