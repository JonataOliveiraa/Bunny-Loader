const { ProjectileID } = Terraria.ID;

export class ExampleYoyoProjectile extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ProjectileID.Sets.YoyosLifeTimeMultiplier[this.Type] = 3.5;
        ProjectileID.Sets.YoyosMaximumRange[this.Type] = 300;
        ProjectileID.Sets.YoyosTopSpeed[this.Type] = 13;
    }

    SetDefaults() {
        this.Projectile.width = 16;
        this.Projectile.height = 16;
        this.Projectile.aiStyle = ProjAIStyleID.Yoyo;
        this.Projectile.friendly = true;
        this.Projectile.penetrate = -1;
        this.Projectile.melee = true;
        this.Projectile.drawLayer = 7;
    }
}
