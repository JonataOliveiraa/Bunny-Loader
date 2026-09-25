const { DustID, ProjectileID, SoundID } = Terraria.ID;
const PlaySound = Terraria.Audio.SoundEngine['SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)'];
const NewDustPerfect = Terraria.Dust['Dust NewDustPerfect(Vector2 Position, int Type, Nullable`1 Velocity, int Alpha, Color newColor, float Scale)'];
const NewDustDirect = Terraria.Dust['Dust NewDustDirect(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const CanHit = Terraria.Collision['bool CanHit(Vector2 Position1, int Width1, int Height1, Vector2 Position2, int Width2, int Height2)'];

const ShootFrequency = 60;
const TargetingRange = 50 * 16;
const FireVelocity = 10;
const SentryLifeTime = 36000;

export class ExampleSentry extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.projFrames[this.Type] = 4;
        ProjectileID.Sets.MinionTargetingFeature[this.Type] = true;
    }

    SetDefaults() {
        this.Projectile.width = 42;
        this.Projectile.height = 30;
        this.Projectile.sentry = true;
        this.Projectile.timeLeft = SentryLifeTime;
        this.Projectile.ignoreWater = true;
        this.Projectile.netImportant = true;
        this.Projectile.decidesManualFallThrough = true;
        this.Projectile.shouldFallThrough = false;
    }

    OnTileCollide(proj, oldVelocity) {
        return false;
    }

    GetAlpha(proj, lightColor) {
        return Color.White;
    }

    AI(proj) {
        const ai = new ProjAI(proj);
        const localAI = new ProjAI(proj, true);
        const floating = ai[2] === 0;

        if (localAI[0] === 0) {
            localAI[0] = 1;
            ai[0] = ShootFrequency * 1.5;
            PlaySound(SoundID.Item46, proj.position, 0, 1);
            for (let i = 0; i < 50; i++) {
                const speed = Rand.NextVector2Unit();
                const d = NewDustPerfect(proj.Center, DustID.BlueCrystalShard, Vector2.Multiply(speed, 4), 0, Color.White, 1.5);
                d.noGravity = true;
            }
        }

        if (Rand.NextBool(10)) {
            const dust = NewDustDirect(Vector2.Add(proj.position, proj.velocity), proj.width, proj.height, DustID.Firework_Blue,
                                       proj.oldVelocity.X * 0.5, proj.oldVelocity.Y * 0.5, 0, Color.White, 1);
            dust.noGravity = true;
            dust.velocity = Vector2.Multiply(dust.velocity, 0.8);
        }

        let fall = proj.velocity.Y;
        if (!floating) fall = Math.min(fall + 0.2, 16);
        proj.velocity = Vector2.new(0, fall);

        const targetNPC = this.FindTarget(proj);
        if (targetNPC && ai[0] <= 0) {
            ai[0] = ShootFrequency;
            PlaySound(SoundID.Item102, proj.Center, 0, 0.4);
            if (Terraria.Main.myPlayer === proj.owner) {
                const shootDirection = Vector2.SafeNormalize(Vector2.Subtract(targetNPC.Center, proj.Center), Vector2.UnitX);
                NewProjectile(proj.GetProjectileSource_FromThis(), Vector2.new(proj.Center.X - 4, proj.Center.Y),
                              Vector2.Multiply(shootDirection, FireVelocity), ModProjectile.getTypeByName('ExampleSentryShot'),
                              proj.damage, 3, proj.owner, 0, 0, 0, null);
            }
        }
        ai[0]--;

        if (ai[0] > ShootFrequency) {
            proj.frame = 0;
        } else if (!targetNPC) {
            if (++proj.frameCounter >= 60) proj.frameCounter = 0;
            proj.frame = proj.frameCounter < 30 ? 1 : 2;
        } else {
            proj.frame = 3;
        }
    }

    FindTarget(proj) {
        let closestTargetDistance = TargetingRange;
        let targetNPC = null;
        const tryTargeting = (npc) => {
            if (!npc || !npc.active || !npc.CanBeChasedBy(proj, false)) return;
            const distance = Vector2.Distance(proj.Center, npc.Center);
            if (distance < closestTargetDistance && CanHit(proj.position, proj.width, proj.height, npc.position, npc.width, npc.height)) {
                closestTargetDistance = distance;
                targetNPC = npc;
            }
        };
        tryTargeting(proj.OwnerMinionAttackTargetNPC);
        if (!targetNPC) {
            const npcs = Terraria.Main.npc;
            for (let i = 0; i < npcs.length - 1; i++) tryTargeting(npcs[i]);
        }
        return targetNPC;
    }

    OnKill(proj, timeLeft) {
        for (let i = 0; i < 50; i++) {
            const speed = Rand.NextVector2Unit();
            const d = NewDustPerfect(Vector2.Add(proj.Center, Vector2.Multiply(speed, 50)), DustID.BlueCrystalShard,
                                     Vector2.Multiply(speed, -5), 0, Color.White, 1.5);
            d.noGravity = true;
        }
    }
}
