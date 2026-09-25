const { ItemID } = Terraria.ID;

export class ExampleFishingRod extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Tools/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ItemID.Sets.CanFishInLava[this.Type] = true;
    }

    SetDefaults() {
        this.CloneDefaults(ItemID.WoodFishingPole);
        this.Item.fishingPole = 30;
        this.Item.shootSpeed = 12;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleBobber');
    }

    HoldItem(item, player) {
        player.accFishingLine = true;
    }
}
