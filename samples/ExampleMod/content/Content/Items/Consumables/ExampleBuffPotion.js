const { ItemID, ItemUseStyleID, SoundID } = Terraria.ID;

export class ExampleBuffPotion extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Consumables/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ItemID.Sets.DrinkParticleColors[this.Type] = [
            Color.new(240, 240, 240),
            Color.new(200, 200, 200),
            Color.new(140, 140, 140),
        ];
    }

    SetDefaults() {
        this.Item.width = 18;
        this.Item.height = 26;
        this.Item.useStyle = ItemUseStyleID.DrinkLiquid;
        this.Item.useAnimation = 15;
        this.Item.useTime = 15;
        this.Item.useTurn = true;
        this.Item.UseSound = SoundID.Item3;
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.consumable = true;
        this.Item.rare = ItemRarityID.Orange;
        this.Item.value = Terraria.Item.sellPrice(0, 1, 0, 0);
        this.Item.buffType = ModBuff.getTypeByName('ExampleDefenseBuff');
        this.Item.buffTime = 5400;
    }
}
