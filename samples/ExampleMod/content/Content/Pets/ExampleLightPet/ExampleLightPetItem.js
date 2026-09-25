const { ItemUseStyleID, SoundID, TileID } = Terraria.ID;

export class ExampleLightPetItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Pets/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.damage = 0;
        this.Item.useStyle = ItemUseStyleID.Swing;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleLightPetProjectile');
        this.Item.width = 16;
        this.Item.height = 30;
        this.Item.UseSound = SoundID.Item2;
        this.Item.useAnimation = 20;
        this.Item.useTime = 20;
        this.Item.rare = ItemRarityID.Yellow;
        this.Item.noMelee = true;
        this.Item.value = Terraria.Item.sellPrice(0, 5, 50, 0);
        this.Item.buffType = ModBuff.getTypeByName('ExampleLightPetBuff');
    }

    UseStyle(item, player) {
        if (player.whoAmI === Terraria.Main.myPlayer && player.itemTime === 0) player.AddBuff(item.buffType, 3600, false);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
