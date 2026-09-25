const { DustID, SoundID } = Terraria.ID;
const PlaySound = Terraria.Audio.SoundEngine['SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)'];
const NewDustDirect = Terraria.Dust['Dust NewDustDirect(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

export class ExampleDrillProjectile extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetDefaults() {
        this.Projectile.width = 22;
        this.Projectile.height = 22;
        this.DefaultToDrillOrChainsaw();
        this.Projectile.aiStyle = -1;
    }

    AI(proj) {
        const player = Terraria.Main.player[proj.owner];
        proj.timeLeft = 60;
        if (proj.soundDelay <= 0) {
            PlaySound(SoundID.Item22, proj.Center, 0, 1);
            proj.soundDelay = 20;
        }
        const center = player['Vector2 RotatedRelativePoint(Vector2 pos, bool reverseRotation, bool addGfxOffY)'](player.MountedCenter, false, true);
        if (Terraria.Main.myPlayer === proj.owner) {
            if (player.channel) {
                const reach = player.HeldItem.shootSpeed * proj.scale;
                const aim = Vector2.Multiply(Vector2.Normalize(Vector2.Subtract(Terraria.Main.MouseWorld, center)), reach);
                if (aim.X !== proj.velocity.X || aim.Y !== proj.velocity.Y) proj.netUpdate = true;
                proj.velocity = aim;
            } else {
                proj.Kill();
                return;
            }
        }
        if (proj.velocity.X > 0) player.ChangeDir(1);
        else if (proj.velocity.X < 0) player.ChangeDir(-1);
        proj.spriteDirection = proj.direction;
        player.ChangeDir(proj.direction);
        player.heldProj = proj.whoAmI;
        player.SetDummyItemTime(2);
        proj.Center = center;
        proj.rotation = Vector2.ToRotation(proj.velocity) + Math.PI / 2;
        player.itemRotation = Vector2.ToRotation(Vector2.Multiply(proj.velocity, proj.direction));
        proj.velocity.X *= 1 + Rand.Next(-3, 4) * 0.01;

        if (Rand.NextBool(10)) {
            const at = Vector2.Add(proj.position, Vector2.Multiply(proj.velocity, Rand.Next(6, 10) * 0.15));
            const dust = NewDustDirect(at, proj.width, proj.height, DustID.Smoke, 0, 0, 100, Color.White, 1);
            dust.position.X -= 4;
            dust.noGravity = true;
            dust.velocity.X *= 0.5;
            dust.velocity.Y -= Rand.Next(3, 8) * 0.1;
        }
    }
}
