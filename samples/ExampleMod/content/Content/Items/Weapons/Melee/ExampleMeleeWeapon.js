import { RARITY_GREEN } from '../../../Rarity.js';

const { SoundID } = Terraria.ID;

// ExampleMeleeWeapon: a espada. Os numeros sao os do ExampleMod.
export class ExampleMeleeWeapon extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Melee/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.melee = true;

        // (dano, repulsao, critico)
        this.SetWeaponValues(50, 6, 6);
        // (useTime, autoReuse)
        this.SetDefaultWeaponStyle(20, true);

        this.Item.value = Terraria.Item.sellPrice(0, 1, 0, 0);
        this.Item.rare = RARITY_GREEN;
        this.Item.UseSound = SoundID.Item1;
    }
}
