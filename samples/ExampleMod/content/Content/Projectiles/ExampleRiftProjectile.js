import { ExampleRiftEffect } from '../Effects/ExampleRiftEffect.js';

const { Main } = Terraria;
const { DustID, SoundID } = Terraria.ID;
const NewDustDirect = Terraria.Dust['Dust NewDustDirect(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
const AddLight = Terraria.Lighting['void AddLight(Vector2 position, float r, float g, float b)'];

// O projétil do Cajado da Fenda: invisível, quem aparece é o efeito de tela
// (ExampleRiftEffect), que ele leva junto. Ao morrer, fecha a fenda e solta
// uma onda de choque, um clarão e a aberração cromática (ScreenPresets).
export class ExampleRiftProjectile extends ModProjectile {
    get Texture() { return 'Content/Projectiles/ExampleSentryShot'; }

    Rift = null;

    SetDefaults() {
        this.Projectile.width = this.Projectile.height = 40;
        this.Projectile.friendly = true;
        this.Projectile.magic = true;
        this.Projectile.penetrate = -1;
        this.Projectile.timeLeft = 120;
        this.Projectile.ignoreWater = true;
        this.Projectile.usesLocalNPCImmunity = true;
        this.Projectile.localNPCHitCooldown = 15;
    }

    AI(proj) {
        proj.velocity = Vector2.Multiply(proj.velocity, 0.96);
        AddLight(proj.Center, 0.6, 0.3, 0.9);
        if (Rand.NextBool(2)) {
            const dust = NewDustDirect(proj.position, proj.width, proj.height, DustID.Shadowflame, 0, 0, 100, Color.White, 1.2);
            dust.noGravity = true;
        }
        if (Main.netMode === 2) return;

        if (!this.Rift) this.Rift = ExampleRiftEffect.Spawn(proj.Center);
        this.Rift.Center = { X: proj.Center.X, Y: proj.Center.Y };
    }

    PreDraw() {
        return false;
    }

    OnKill(proj) {
        SoundEngine.PlaySound(SoundID.Item14, proj.position);
        if (Main.netMode === 2) return;

        if (this.Rift && this.Rift.Active) this.Rift.Close();
        ScreenPresets.Shockwave.Spawn(proj.Center, { radius: 260, duration: 35 });
        ScreenPresets.Flash.Spawn(null, { color: Color.new(190, 120, 255), amount: 0.3, duration: 12 });
        ScreenPresets.ChromaticAberration.Spawn(proj.Center, { amount: 5, duration: 18, fadeOut: 18 });
    }
}
