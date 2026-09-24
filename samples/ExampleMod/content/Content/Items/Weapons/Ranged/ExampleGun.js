import { RARITY_GREEN } from '../../../Rarity.js';

const { SoundID, AmmoID, ProjectileID } = Terraria.ID;

// ExampleGun: a arma. O `shoot` dela e o de qualquer arma de fogo do jogo (o
// tModLoader poe PurificationPowder); com municao, vale o da municao.
export class ExampleGun extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Ranged/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.ranged = true;
        this.Item.shoot = ProjectileID.PurificationPowder;
        this.Item.shootSpeed = 10;
        this.Item.useAmmo = AmmoID.Bullet;

        // (dano, repulsao, critico)
        this.SetWeaponValues(20, 5, 0);
        // (useTime, autoReuse)
        this.SetDefaultWeaponStyle(10, true);

        this.Item.noMelee = true;   // o corpo da arma nao bate, so o tiro
        this.Item.value = Terraria.Item.sellPrice(0, 10, 0, 0);
        this.Item.rare = RARITY_GREEN;
        this.Item.UseSound = SoundID.Item41;
    }

    HoldoutOffset(item, player) {
        return { X: -18, Y: 0 };
    }
}
