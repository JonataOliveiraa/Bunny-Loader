const { ProjectileID } = Terraria.ID;

export class ExampleSpearProjectile extends ModProjectile {
    HoldoutRangeMin = 24;
    HoldoutRangeMax = 96;

    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetDefaults() {
        this.CloneDefaults(ProjectileID.Spear);
    }

    PreAI(proj) {
        const player = Terraria.Main.player[proj.owner];
        const duration = player.itemAnimationMax;
        player.heldProj = proj.whoAmI;
        if (proj.timeLeft > duration) proj.timeLeft = duration;

        proj.velocity = Vector2.Normalize(proj.velocity);
        const half = duration * 0.5;
        const progress = proj.timeLeft < half ? proj.timeLeft / half : (duration - proj.timeLeft) / half;
        const reach = Vector2.SmoothStep(Vector2.Multiply(proj.velocity, this.HoldoutRangeMin),
                                         Vector2.Multiply(proj.velocity, this.HoldoutRangeMax), progress);
        proj.Center = Vector2.Add(player.MountedCenter, reach);

        proj.rotation = Vector2.ToRotation(proj.velocity) + MathHelper.ToRadians(proj.spriteDirection === -1 ? 45 : 135);
        return false;
    }
}
