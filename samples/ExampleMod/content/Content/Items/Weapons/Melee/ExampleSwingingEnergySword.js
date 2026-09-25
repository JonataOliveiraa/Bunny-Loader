const { SoundID } = Terraria.ID;
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

export class ExampleSwingingEnergySword extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Melee/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.melee = true;
        this.Item.noMelee = true;
        this.SetWeaponValues(72, 4.5, 0);
        this.SetDefaultWeaponStyle(20, true);
        this.Item.value = Terraria.Item.buyPrice(0, 23, 0, 0);
        this.Item.rare = ItemRarityID.Pink;
        this.Item.UseSound = SoundID.Item1;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleSwingingEnergySwordProjectile');
    }

    Shoot(item, player, position, velocity, type, damage, knockBack) {
        const scale = player.GetAdjustedItemScale(item);
        player.direction = Terraria.Main.MouseWorld.X < player.Center.X ? -1 : 1;
        NewProjectile(player.GetProjectileSource_Item(item), player.MountedCenter, Vector2.new(player.direction, 0),
                      type, damage, knockBack, player.whoAmI, player.direction * player.gravDir,
                      player.itemAnimationMax, scale, null);
        return false;
    }
}
