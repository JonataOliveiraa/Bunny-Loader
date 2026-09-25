const { ItemID, ItemUseStyleID, SoundID, TileID } = Terraria.ID;

export class ExampleMinionItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Summon/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ItemID.Sets.GamepadWholeScreenUseRange[this.Type] = true;
        ItemID.Sets.LockOnIgnoresCollision[this.Type] = true;
        ItemID.Sets.StaffMinionSlotsRequired[this.Type] = 1;
    }

    SetDefaults() {
        this.Item.damage = 30;
        this.Item.knockBack = 3;
        this.Item.mana = 10;
        this.Item.width = 32;
        this.Item.height = 32;
        this.Item.useTime = 36;
        this.Item.useAnimation = 36;
        this.Item.useStyle = ItemUseStyleID.Swing;
        this.Item.value = Terraria.Item.sellPrice(0, 30, 0, 0);
        this.Item.rare = ItemRarityID.Cyan;
        this.Item.UseSound = SoundID.Item44;
        this.Item.noMelee = true;
        this.Item.summon = true;
        this.Item.buffType = ModBuff.getTypeByName('ExampleMinionBuff');
        this.Item.shoot = ModProjectile.getTypeByName('ExampleMinion');
    }

    ModifyShootStats(item, player, stats) {
        const position = new Ref(Terraria.Main.MouseWorld);
        player.LimitPointToPlayerReachableArea(position);
        stats.position = position.value;
    }

    Shoot(item, player) {
        player.AddBuff(item.buffType, 2, false);
        return true;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
