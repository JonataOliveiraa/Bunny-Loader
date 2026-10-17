import { Sparkle } from '../Dusts/Sparkle.js';

const { ProjectileID } = Terraria.ID;
const NewDustDirect = Terraria.Dust['Dust NewDustDirect(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

export class ExampleSpearProjectile extends ModProjectile {
    HoldoutRangeMin = 24;
    HoldoutRangeMax = 96;

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

        // O brilho do ExampleMod (nunca no servidor dedicado).
        if (!Terraria.Main.dedServ) {
            const sparkle = ModContent.DustType(Sparkle);
            if (Rand.NextBool(3)) {
                NewDustDirect(proj.position, proj.width, proj.height, sparkle, proj.velocity.X * 2, proj.velocity.Y * 2, 128, Color.White, 1.2);
            }
            if (Rand.NextBool(4)) {
                NewDustDirect(proj.position, proj.width, proj.height, sparkle, 0, 0, 128, Color.White, 0.3);
            }
        }
        return false;
    }
}
