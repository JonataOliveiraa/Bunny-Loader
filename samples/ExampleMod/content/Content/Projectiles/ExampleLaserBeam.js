const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const EntitySpriteDraw = Terraria.Main['void EntitySpriteDraw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float worthless)'];
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
const SolidCollision = Terraria.Collision['bool SolidCollision(Vector2 Position, int Width, int Height)'];
const AddLight = Terraria.Lighting['void AddLight(Vector2 position, float r, float g, float b)'];

export class ExampleLaserBeam extends ModProjectile {
    MaxLaserLength = 1000;
    HoldoutDistance = 10;

    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.projFrames[this.Type] = 3;
    }

    SetDefaults() {
        this.Projectile.width = this.Projectile.height = 10;
        this.Projectile.friendly = true;
        this.Projectile.magic = true;
        this.Projectile.penetrate = -1;
        this.Projectile.tileCollide = false;
        this.Projectile.usesLocalNPCImmunity = true;
        this.Projectile.localNPCHitCooldown = 10;
    }

    LaserColor() {
        return Color.new(128, 200, 255);
    }

    LaserEnd(start, rotation) {
        const cos = Math.cos(rotation), sin = Math.sin(rotation);
        const at = Vector2.new(start.X, start.Y);
        for (let dist = 0; dist <= this.MaxLaserLength; dist += 16) {
            at.X = start.X + cos * dist;
            at.Y = start.Y + sin * dist;
            if (SolidCollision(at, 1, 1)) return dist;
        }
        return this.MaxLaserLength;
    }

    AI(proj) {
        const ai = new ProjAI(proj);
        const player = Terraria.Main.player[proj.owner];
        const holdout = Terraria.Main.projectile[ai[0] | 0];
        if (!holdout || !holdout.active) {
            proj.Kill();
            return;
        }
        proj.timeLeft = 2;
        proj.rotation = holdout.rotation - MathHelper.PiOver2;
        const dir = Vector2.ToRotationVector2(proj.rotation);
        proj.Center = Vector2.Add(holdout.Center, Vector2.Multiply(dir, this.HoldoutDistance));
        proj.velocity = Vector2.Zero;
        proj.scale = Math.min(1, proj.scale + 0.05);
        ai[1] = this.LaserEnd(proj.Center, proj.rotation);

        if (proj.owner !== Terraria.Main.myPlayer) return;
        const len = ai[1];
        const start = proj.Center;
        const end = Vector2.Add(start, Vector2.Multiply(dir, len));
        if (ai[2] % 5 === 0) this.DamageNPCs(proj, player, start, end, len);
        ai[2]++;

        const color = this.LaserColor();
        const glow = 0.85 * Terraria.Main.essScale * proj.scale / 255;
        for (let i = 0; i <= len; i += 48) {
            AddLight(Vector2.new(start.X + dir.X * i, start.Y + dir.Y * i), color.R * glow, color.G * glow, color.B * glow);
        }
        if (ai[1] < this.MaxLaserLength && ai[2] % 5 === 0) {
            const at = Vector2.Subtract(end, 8);
            for (let i = 0; i < 16; i++) {
                const d = NewDust(at, 16, 16, 278, 0, 0, 100, color, proj.scale);
                Terraria.Main.dust[d].noGravity = true;
            }
        }
    }

    DamageNPCs(proj, player, start, end, len) {
        const maxDistSq = (len + 50) * (len + 50);
        const line = Vector2.Subtract(end, start);
        const lineLenSq = Vector2.LengthSquared(line) || 1;
        for (let i = 0; i < 200; i++) {
            const target = Terraria.Main.npc[i];
            if (!target.active || target.friendly || target.dontTakeDamage) continue;
            const dx = target.position.X - start.X;
            const dy = target.position.Y - start.Y;
            if (dx * dx + dy * dy > maxDistSq) continue;
            const cx = target.position.X + target.width / 2;
            const cy = target.position.Y + target.height / 2;
            const radius = Math.max(target.width, target.height) / 2;
            const t = Math.min(1, Math.max(0, ((cx - start.X) * line.X + (cy - start.Y) * line.Y) / lineLenSq));
            const ox = cx - (start.X + line.X * t);
            const oy = cy - (start.Y + line.Y * t);
            if (ox * ox + oy * oy >= (30 + radius) * (30 + radius)) continue;
            if (target.immune[proj.owner] !== 0) continue;
            const item = player.HeldItem;
            const crit = Rand.NextFloat() * 100 < player.GetWeaponCrit(item);
            const damage = target['double StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool noEffect, bool fromNet, int owner)'](
                player.GetWeaponDamage(item), player.GetWeaponKnockback(item, proj.knockBack), proj.direction, crit, false, false, proj.owner);
            player.addDPS(damage | 0);
            target.immune[proj.owner] = proj.localNPCHitCooldown;
        }
    }

    PreDraw(proj) {
        const texture = Terraria.GameContent.TextureAssets.Projectile[this.Type].Value;
        const fh = texture.Height / 3;
        const len = proj.ai.val1;
        const rot = proj.rotation + MathHelper.PiOver2;
        const cos = Math.cos(proj.rotation), sin = Math.sin(proj.rotation);
        const scale = Vector2.new(proj.scale, 1);
        const color = this.LaserColor();
        const origin = Vector2.new(texture.Width / 2, fh);
        const screen = Terraria.Main.screenPosition;
        let x = proj.Center.X - screen.X;
        let y = proj.Center.Y - screen.Y;
        EntitySpriteDraw(texture, Vector2.new(x, y), Rectangle.new(0, fh * 2, texture.Width, fh), color, rot, origin, scale, SpriteEffects.None, 0);
        x += cos * fh;
        y += sin * fh;
        const body = Rectangle.new(0, fh, texture.Width, fh);
        for (let i = 0; i < len - fh * 2; i += fh) {
            EntitySpriteDraw(texture, Vector2.new(x, y), body, color, rot, origin, scale, SpriteEffects.None, 0);
            x += cos * fh;
            y += sin * fh;
        }
        EntitySpriteDraw(texture, Vector2.new(x, y), Rectangle.new(0, 0, texture.Width, fh), color, rot, origin, scale, SpriteEffects.None, 0);
        return false;
    }
}
