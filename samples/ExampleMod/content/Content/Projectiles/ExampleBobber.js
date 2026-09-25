export class ExampleBobber extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetDefaults() {
        this.Projectile.width = 14;
        this.Projectile.height = 14;
        this.Projectile.friendly = true;
        this.Projectile.aiStyle = ProjAIStyleID.Bobber;
        this.Projectile.bobber = true;
        this.Projectile.penetrate = -1;
        this.Projectile.netImportant = true;
    }
}
