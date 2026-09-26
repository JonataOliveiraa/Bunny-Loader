const PossibleLineColors = [
    [255, 215, 0],
    [0, 191, 255],
];

export class ExampleBobber extends ModProjectile {
    fishingLineColorIndex = 0;

    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    get FishingLineColor() {
        const [r, g, b] = PossibleLineColors[this.fishingLineColorIndex];
        return Color.new(r, g, b);
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

    OnSpawn(proj) {
        this.fishingLineColorIndex = Math.floor(Math.random() * PossibleLineColors.length);
    }
}
