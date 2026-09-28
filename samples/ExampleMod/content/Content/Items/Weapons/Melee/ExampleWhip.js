const { ItemID } = Terraria.ID;
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

export class ExampleWhip extends ModItem {
    static ExampleWhipTagDamage = 5;

    SetStaticDefaults() {
        const tag = Terraria.GameContent.Items.WhipTagEffect.new();
        tag['void .ctor()']();
        tag.TagDamage = ExampleWhip.ExampleWhipTagDamage;
        ItemID.Sets.UniqueTagEffects[this.Type] = tag;
    }

    SetDefaults() {
        this.DefaultToWhip(ModProjectile.getTypeByName('ExampleWhipProjectile'), 20, 2, 4);
        this.SetShopValues(ItemRarityID.Green, ModItem.sellPrice(0, 10, 0, 0));
    }

    // A curva do golpe (ai[1]): o jogo sorteia só para os chicotes dele (uma
    // lista no ItemCheck_Shoot). Sem ela o chicote vai e volta reto e só acerta
    // o que está na linha do meio.
    Shoot(item, player, position, velocity, type, damage, knockBack) {
        let swingDirection = 0.6 + 0.4 * Rand.NextFloat();
        if (Rand.NextBool(3)) swingDirection *= -2.5;

        NewProjectile(player.GetProjectileSource_Item(item), position.X, position.Y, velocity.X, velocity.Y,
                      type, damage, knockBack, player.whoAmI, 0, swingDirection, 0, null);
        return false;
    }
}
