const { ItemID } = Terraria.ID;

export class ExampleHookItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Tools/' + this.constructor.name;
    }

    SetDefaults() {
        this.CloneDefaults(ItemID.AmethystHook);
        this.Item.shootSpeed = 18;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleHookProjectile');
    }
}
