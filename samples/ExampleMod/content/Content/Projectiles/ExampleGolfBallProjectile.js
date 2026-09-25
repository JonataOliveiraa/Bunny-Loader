const { ProjectileID } = Terraria.ID;

export class ExampleGolfBallProjectile extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ProjectileID.Sets.IsAGolfBall[this.Type] = true;
        ProjectileID.Sets.TrailingMode[this.Type] = 0;
        ProjectileID.Sets.TrailCacheLength[this.Type] = 20;
    }

    SetDefaults() {
        this.Projectile.netImportant = true;
        this.Projectile.width = 7;
        this.Projectile.height = 7;
        this.Projectile.friendly = true;
        this.Projectile.penetrate = -1;
        this.Projectile.aiStyle = ProjAIStyleID.GolfBall;
        this.Projectile.tileCollide = false;
    }
}
