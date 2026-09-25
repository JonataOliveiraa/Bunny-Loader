const { DustID, ProjectileID, SoundID } = Terraria.ID;
const PlaySound = Terraria.Audio.SoundEngine['SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)'];
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

export class ExampleSentryShot extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ProjectileID.Sets.SentryShot[this.Type] = true;
    }

    SetDefaults() {
        this.Projectile.width = 16;
        this.Projectile.height = 16;
        this.Projectile.friendly = true;
        this.Projectile.timeLeft = 600;
    }

    AI(proj) {
        if (Rand.NextBool(3)) {
            NewDust(Vector2.Add(proj.position, proj.velocity), proj.width, proj.height, DustID.BlueFairy,
                    proj.velocity.X * 0.5, proj.velocity.Y * 0.5, 0, Color.SkyBlue, 1);
        }
    }

    OnKill(proj, timeLeft) {
        for (let k = 0; k < 5; k++) {
            NewDust(Vector2.Add(proj.position, proj.velocity), proj.width, proj.height, DustID.BlueFairy,
                    proj.oldVelocity.X * 0.5, proj.oldVelocity.Y * 0.5, 0, Color.SkyBlue, 1);
        }
        PlaySound(SoundID.Item25, proj.position, 0, 1);
    }
}
