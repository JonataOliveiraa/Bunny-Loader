const { AmmoID, ProjectileID, SoundID, ItemUseStyleID } = Terraria.ID;
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

export class ExampleShotgun extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Ranged/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.width = 44;
        this.Item.height = 18;
        this.Item.rare = ItemRarityID.Green;
        this.Item.value = Terraria.Item.buyPrice(0, 10, 0, 0);

        this.Item.useTime = 55;
        this.Item.useAnimation = 55;
        this.Item.useStyle = ItemUseStyleID.Shoot;
        this.Item.autoReuse = true;
        this.Item.UseSound = SoundID.Item36;

        this.Item.ranged = true;
        this.Item.damage = 10;
        this.Item.knockBack = 6;
        this.Item.noMelee = true;

        this.Item.shoot = ProjectileID.PurificationPowder;
        this.Item.shootSpeed = 10;
        this.Item.useAmmo = AmmoID.Bullet;
    }

    Shoot(item, player, position, velocity, type, damage, knockBack) {
        const source = player.GetProjectileSource_Item(item);
        for (let i = 0; i < 8; i++) {
            let v = Vector2.RotatedByRandom(velocity, MathHelper.ToRadians(15));
            v = Vector2.Multiply(v, 1 - Rand.NextFloat(0.3));
            NewProjectile(source, position.X, position.Y, v.X, v.Y, type, damage, knockBack,
                          player.whoAmI, 0, 0, 0, null);
        }
        return false;
    }

    HoldoutOffset() {
        return { X: -2, Y: -2 };
    }
}
