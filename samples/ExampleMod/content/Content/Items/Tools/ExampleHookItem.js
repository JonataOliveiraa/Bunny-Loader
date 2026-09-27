const { ItemID } = Terraria.ID;

export class ExampleHookItem extends ModItem {
    SetDefaults() {
        this.CloneDefaults(ItemID.AmethystHook);
        this.Item.shootSpeed = 18;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleHookProjectile');
    }
}
