const { ItemID, ItemUseStyleID, SoundID, TileID } = Terraria.ID;
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const SentryHeight = 30;

export class ExampleSentryItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Summon/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ItemID.Sets.GamepadWholeScreenUseRange[this.Type] = true;
        ItemID.Sets.LockOnIgnoresCollision[this.Type] = true;
    }

    SetDefaults() {
        this.Item.damage = 50;
        this.Item.summon = true;
        this.Item.sentry = true;
        this.Item.mana = 10;
        this.Item.width = 26;
        this.Item.height = 28;
        this.Item.useTime = 30;
        this.Item.useAnimation = 30;
        this.Item.useStyle = ItemUseStyleID.Swing;
        this.Item.noMelee = true;
        this.Item.knockBack = 3;
        this.Item.value = Terraria.Item.buyPrice(0, 30, 0, 0);
        this.Item.rare = ItemRarityID.Cyan;
        this.Item.UseSound = SoundID.Item83;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleSentry');
    }

    Shoot(item, player, position, velocity, type, damage, knockBack) {
        const inAir = player.direction === 1;
        const at = new Ref(Terraria.Main.MouseWorld);
        player.LimitPointToPlayerReachableArea(at);
        let x = at.value.X, y = at.value.Y - Math.ceil(SentryHeight / 2);
        if (!inAir) {
            const worldX = new Ref(), worldY = new Ref(), pushYUp = new Ref();
            player.FindSentryRestingSpot(type, worldX, worldY, pushYUp);
            x = worldX.value;
            y = worldY.value - Math.ceil(SentryHeight / 2);
        }
        NewProjectile(player.GetProjectileSource_Item(item), Vector2.new(x, y), Vector2.new(0, 0), type, damage, knockBack,
                      Terraria.Main.myPlayer, 0, 0, inAir ? 0 : 1, null);
        player.UpdateMaxTurrets();
        return false;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
