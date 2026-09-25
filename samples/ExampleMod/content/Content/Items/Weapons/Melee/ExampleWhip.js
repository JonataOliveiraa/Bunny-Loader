const { ItemID } = Terraria.ID;

export class ExampleWhip extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Melee/' + this.constructor.name;
    }

    SetStaticDefaults() {
        const tag = Terraria.GameContent.Items.WhipTagEffect.new();
        tag['void .ctor()']();
        tag.TagDamage = 10;
        tag.CritChance = 5;
        tag.PlayerBuffId = 312;
        tag.PlayerBuffTime = 240;
        ItemID.Sets.UniqueTagEffects[this.Type] = tag;
    }

    SetDefaults() {
        this.DefaultToWhip(ModProjectile.getTypeByName('ExampleWhipProjectile'), 20, 2, 4);
        this.Item.channel = true;
        this.SetShopValues(ItemRarityID.Green, ModItem.sellPrice(0, 10, 0, 0));
    }
}
