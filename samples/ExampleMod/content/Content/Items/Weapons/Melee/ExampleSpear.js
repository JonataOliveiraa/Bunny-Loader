const { ItemID, ItemUseStyleID, SoundID } = Terraria.ID;
const PlaySound = Terraria.Audio.SoundEngine['SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)'];

export class ExampleSpear extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/Weapons/Melee/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ItemID.Sets.SkipsInitialUseSound[this.Type] = true;
    }

    SetDefaults() {
        this.Item.rare = ItemRarityID.Pink;
        this.Item.value = Terraria.Item.sellPrice(0, 0, 10, 0);
        this.Item.useStyle = ItemUseStyleID.Shoot;
        this.Item.useAnimation = 12;
        this.Item.useTime = 18;
        this.Item.autoReuse = true;
        this.Item.UseSound = SoundID.Item71;
        this.Item.damage = 25;
        this.Item.knockBack = 6.5;
        this.Item.noUseGraphic = true;
        this.Item.melee = true;
        this.Item.noMelee = true;
        this.Item.shoot = ModProjectile.getTypeByName('ExampleSpearProjectile');
        this.Item.shootSpeed = 3.7;
    }

    CanUseItem(item, player) {
        return player.ownedProjectileCounts[item.shoot] < 1;
    }

    Shoot(item, player) {
        if (item.UseSound) PlaySound(item.UseSound, player.Center, 0, 1);
        return true;
    }
}
