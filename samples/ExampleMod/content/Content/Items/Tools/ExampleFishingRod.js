import { ExampleBobber } from '../../Projectiles/ExampleBobber.js';

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

    ModifyFishingLine(item, bobber, lineOriginOffset, lineColor) {
        lineOriginOffset.value = Vector2.new(43, -30);
        const m = bobber.ModProjectile;
        if (m instanceof ExampleBobber) {
            lineColor.value = m.FishingLineColor;
        } else {
            const Main = Terraria.Main;
            lineColor.value = Color.new(Main.DiscoR, Main.DiscoG, Main.DiscoB);
        }
    }
}
