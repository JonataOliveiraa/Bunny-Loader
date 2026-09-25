const { DustID, ProjectileID, SoundID } = Terraria.ID;
const AddLight = Terraria.Lighting['void AddLight(Vector2 position, float r, float g, float b)'];
const PlaySound = Terraria.Audio.SoundEngine['SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)'];
const NewDustDirect = Terraria.Dust['Dust NewDustDirect(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

const DashCooldown = 1000;
const DashSpeed = 20;
const FadeInTicks = 30;
const FullBrightTicks = 200;
const FadeOutTicks = 30;
const Range = 500;
const RangeHypotenuse = Math.SQRT2 * Range;
const RangeHypotenuseSquared = RangeHypotenuse * RangeHypotenuse;

export class ExampleLightPetProjectile extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Pets/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.projFrames[this.Type] = 1;
        Terraria.Main.projPet[this.Type] = true;
        ProjectileID.Sets.TrailingMode[this.Type] = 2;
        ProjectileID.Sets.LightPet[this.Type] = true;
    }

    SetDefaults() {
        this.Projectile.width = 30;
        this.Projectile.height = 30;
        this.Projectile.penetrate = -1;
        this.Projectile.netImportant = true;
        this.Projectile.timeLeft *= 5;
        this.Projectile.friendly = true;
        this.Projectile.ignoreWater = true;
        this.Projectile.scale = 0.8;
        this.Projectile.tileCollide = false;
    }

    AI(proj) {
        const player = Terraria.Main.player[proj.owner];
        if (!player.active) {
            proj.active = false;
            return;
        }
        if (!player.dead && player.FindBuffIndex(ModBuff.getTypeByName('ExampleLightPetBuff')) >= 0) proj.timeLeft = 2;

        const ai = new ProjAI(proj);
        this.UpdateDash(proj, ai, player);
        this.UpdateFading(proj, ai, player);
        this.UpdateExtraMovement(proj);

        proj.rotation += proj.velocity.X / 20;
        const opacity = 1 - proj.alpha / 255;
        AddLight(proj.Center, opacity * 0.9, opacity * 0.1, opacity * 0.3);
    }

    UpdateDash(proj, ai, player) {
        ai[1]++;
        if (ai[1] <= DashCooldown || Math.trunc(ai[0]) % 100 !== 0) return;
        const npcs = Terraria.Main.npc;
        for (let i = 0; i < npcs.length - 1; i++) {
            const npc = npcs[i];
            if (!npc.active || npc.friendly) continue;
            if (Vector2.DistanceSquared(player.Center, npc.Center) >= RangeHypotenuseSquared) continue;
            const toward = Vector2.Normalize(Vector2.Subtract(npc.Center, proj.Center));
            proj.velocity = Vector2.Add(proj.velocity, Vector2.Multiply(toward, DashSpeed));
            ai[1] = 0;
            PlaySound(SoundID.Item42, proj.Center, 0, 1);
            break;
        }
    }

    UpdateFading(proj, ai, player) {
        const playerCenter = player.Center;
        ai[0]++;
        if (ai[0] < FadeInTicks) {
            proj.alpha = Math.trunc(255 - 255 * ai[0] / FadeInTicks);
        } else if (ai[0] < FadeInTicks + FullBrightTicks) {
            proj.alpha = 0;
            if (Rand.NextBool(6)) {
                const dust = NewDustDirect(proj.position, proj.width, proj.height, DustID.PinkFairy, 0, 0, 200, Color.White, 0.8);
                dust.velocity = Vector2.Multiply(dust.velocity, 0.3);
            }
        } else if (ai[0] < FadeInTicks + FullBrightTicks + FadeOutTicks) {
            proj.alpha = Math.trunc(255 * (ai[0] - FadeInTicks - FullBrightTicks) / FadeOutTicks);
        } else {
            proj.Center = Vector2.Add(playerCenter, Rand.NextVector2Circular(Range, Range));
            ai[0] = 0;
            proj.velocity = Vector2.Multiply(Vector2.Normalize(Vector2.Subtract(playerCenter, proj.Center)), 2);
        }

        if (Vector2.Distance(playerCenter, proj.Center) > RangeHypotenuse) {
            proj.Center = Vector2.Add(playerCenter, Rand.NextVector2Circular(Range, Range));
            ai[0] = 0;
            const back = Vector2.Multiply(Vector2.Normalize(Vector2.Subtract(playerCenter, proj.Center)), 2);
            proj.velocity = Vector2.Add(proj.velocity, back);
        }

        if (Math.trunc(ai[0]) % 100 === 0) {
            proj.velocity = Vector2.RotatedByRandom(proj.velocity, MathHelper.ToRadians(90));
        }
    }

    UpdateExtraMovement(proj) {
        if (Vector2.Length(proj.velocity) > 1) proj.velocity = Vector2.Multiply(proj.velocity, 0.98);
        if (proj.velocity.X === 0 && proj.velocity.Y === 0) {
            proj.velocity = Vector2.Multiply(Vector2.RotatedBy(Vector2.UnitX, Rand.NextFloat() * MathHelper.TwoPi), 2);
        }
    }
}
