import { Sparkle } from '../Dusts/Sparkle.js';

const { ProjectileID } = Terraria.ID;
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

export class ExampleYoyoProjectile extends ModProjectile {
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

    // Qualquer IA serve para poeira, projéteis etc.: aqui o ioiô solta Sparkle.
    PostAI(proj) {
        if (Rand.NextBool(5)) {
            NewDust(proj.position, proj.width, proj.height, ModContent.DustType(Sparkle), 0, 0, 0, Color.White, 1);
        }
    }
}
