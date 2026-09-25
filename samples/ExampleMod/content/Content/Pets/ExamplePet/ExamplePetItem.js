const { ItemID, TileID } = Terraria.ID;

export class ExamplePetItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Pets/' + this.constructor.name;
    }

    SetDefaults() {
        this.CloneDefaults(ItemID.ZephyrFish);
        this.Item.shoot = ModProjectile.getTypeByName('ExamplePetProjectile');
        this.Item.buffType = ModBuff.getTypeByName('ExamplePetBuff');
    }

    UseItem(item, player) {
        if (player.whoAmI === Terraria.Main.myPlayer) player.AddBuff(item.buffType, 3600, false);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
