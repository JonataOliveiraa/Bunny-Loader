const { Main, Utils } = Terraria;
const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const { TextureAssets } = Terraria.GameContent;
const Frame = Utils['Rectangle Frame(Texture2D tex, int horizontalFrames, int verticalFrames, int frameX, int frameY, int sizeOffsetX, int sizeOffsetY)'];
const Remap = Utils['float Remap(float fromValue, float fromMin, float fromMax, float toMin, float toMax, bool clamped)'];
const GetLerpValue = Utils['float GetLerpValue(float from, float to, float t, bool clamped)'];
const LightAt = Terraria.Lighting['Color GetColor(int x, int y)'];
const NewDustPerfect = Terraria.Dust['Dust NewDustPerfect(Vector2 Position, int Type, Nullable`1 Velocity, int Alpha, Color newColor, float Scale)'];

export class ExampleSwingingEnergySwordProjectile extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Main.projFrames[this.Type] = 4;
    }

    SetDefaults() {
        this.Projectile.width = this.Projectile.height = 16;
        this.Projectile.melee = true;
        this.Projectile.aiStyle = 190;
        this.Projectile.friendly = true;
        this.Projectile.tileCollide = false;
        this.Projectile.ignoreWater = true;
        this.Projectile.usesLocalNPCImmunity = true;
        this.Projectile.localNPCHitCooldown = -1;
        this.Projectile.ownerHitCheck = true;
        this.Projectile.ownerHitCheckDistance = 300;
        this.Projectile.usesOwnerMeleeHitCD = true;
        this.Projectile.penetrate = -1;
        this.Projectile.noEnchantmentVisuals = true;
    }

    AI(proj) {
        const ai = new ProjAI(proj);
        const localAI = new ProjAI(proj, true);
        const player = Main.player[proj.owner];
        localAI[0]++;
        const life = localAI[0] / ai[1];
        proj.Center = Vector2.Subtract(player['Vector2 RotatedRelativePoint(Vector2 pos, bool reverseRotation, bool addGfxOffY)'](player.MountedCenter, false, true), proj.velocity);
        proj.scale = 1 + life * 0.8;

        const offset = proj.rotation + (Rand.NextFloat() * 2 - 1) * (Math.PI / 2) * 0.7;
        const along = Vector2.ToRotationVector2(offset);
        const velocity = Vector2.ToRotationVector2(offset + ai[0] * (Math.PI / 2));
        if (Rand.NextFloat() < proj.Opacity) {
            const color = Color.Lerp(Color.SkyBlue, Color.White, Rand.NextFloat() * 0.3);
            const where = Vector2.Add(proj.Center, Vector2.Multiply(along, Rand.NextFloat() * 80 * proj.scale + 20 * proj.scale));
            const dust = NewDustPerfect(where, 278, velocity, 50, color, 0.8);
            dust.fadeIn = 0.4 + Rand.NextFloat() * 0.15;
            dust.noGravity = true;
        }
        if (Rand.NextFloat() * 1.5 < proj.Opacity) {
            const where = Vector2.Add(proj.Center, Vector2.Multiply(along, 84 * proj.scale));
            const dust = NewDustPerfect(where, 278, Vector2.Multiply(velocity, 1.5), 100,
                                        Color.Multiply(Color.SkyBlue, proj.Opacity), proj.Opacity);
            dust.noGravity = true;
        }
        if (localAI[0] >= ai[1]) proj.Kill();
        proj.scale *= ai[2];
    }

    PreDraw(proj) {
        this.DrawLikeExcalibur(proj, Color.new(60, 160, 180), Color.new(80, 255, 255), Color.new(150, 240, 255));
        return false;
    }

    DrawLikeExcalibur(proj, back, middle, front) {
        const ai = new ProjAI(proj);
        const localAI = new ProjAI(proj, true);
        const sb = Main.spriteBatch;
        const Draw = (...args) => sb['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](...args);
        const position = Vector2.Subtract(proj.Center, Main.screenPosition);
        const texture = TextureAssets.Projectile[this.Type].Value;
        const source = Frame(texture, 1, Main.projFrames[this.Type], 0, 0, 0, 0);
        const final = Frame(texture, 1, Main.projFrames[this.Type], 0, 3, 0, 0);
        const origin = Vector2.Multiply(Rectangle.Size(source), 0.5);
        const scale = proj.scale * 1.1;
        const effects = ai[0] >= 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
        const life = localAI[0] / ai[1];
        const lerpTime = Remap(life, 0, 0.5, 0, 1, true) * Remap(life, 0.5, 1, 1, 0, true);
        const c = proj.Center;
        const light = LightAt(Math.floor(c.X / 16), Math.floor(c.Y / 16));
        const v = Color.ToVector3(light);
        const lighting = Remap(Math.hypot(v.X, v.Y, v.Z) / Math.sqrt(3), 0.2, 1, 0, 1, true);

        const whiteLerp = Color.Multiply(Color.White, lerpTime * 0.5);
        whiteLerp.A = Math.round(whiteLerp.A * (1 - lighting));
        const faint = Color.Multiply(whiteLerp, lighting * 0.5);
        faint.G = Math.round(faint.G * lighting);
        faint.B = Math.round(faint.R * (0.25 + lighting * 0.75));

        Draw(texture, position, source, Color.Multiply(back, lighting * lerpTime), proj.rotation + ai[0] * MathHelper.PiOver4 * -1 * (1 - life), origin, scale, effects, 0);
        Draw(texture, position, source, Color.Multiply(faint, 0.15), proj.rotation + ai[0] * 0.01, origin, scale, effects, 0);
        Draw(texture, position, source, Color.Multiply(middle, lighting * lerpTime * 0.3), proj.rotation, origin, scale, effects, 0);
        Draw(texture, position, source, Color.Multiply(front, lighting * lerpTime * 0.5), proj.rotation, origin, scale * 0.975, effects, 0);
        Draw(texture, position, final, Color.Multiply(Color.White, 0.6 * lerpTime), proj.rotation + ai[0] * 0.01, origin, scale, effects, 0);
        Draw(texture, position, final, Color.Multiply(Color.White, 0.6 * lerpTime), proj.rotation + ai[0] * -0.05, origin, scale * 0.8, effects, 0);
        Draw(texture, position, final, Color.Multiply(Color.White, 0.6 * lerpTime), proj.rotation + ai[0] * -0.1, origin, scale * 0.6, effects, 0);

        for (let i = 0; i < 8; i++) {
            const rot = proj.rotation + ai[0] * i * (MathHelper.Pi * -2) * 0.025 + Remap(life, 0, 1, 0, MathHelper.PiOver4, true) * ai[0];
            const at = Vector2.Add(position, Vector2.Multiply(Vector2.ToRotationVector2(rot), (texture.Width * 0.5 - 6) * scale));
            this.DrawSparkle(proj.Opacity, SpriteEffects.None, at, Color.Multiply(Color.new(255, 255, 255, 0), lerpTime * (i / 9)),
                             middle, life, 0, 0.5, 0.5, 1, rot,
                             Vector2.Multiply(Vector2.new(0, Remap(life, 0, 1, 3, 0, true)), scale), Vector2.Multiply(Vector2.One, scale));
        }
        const edge = proj.rotation + Remap(life, 0, 1, 0, MathHelper.PiOver4, true) * ai[0];
        const tip = Vector2.Add(position, Vector2.Multiply(Vector2.ToRotationVector2(edge), (texture.Width * 0.5 - 4) * scale));
        this.DrawSparkle(proj.Opacity, SpriteEffects.None, tip, Color.Multiply(Color.new(255, 255, 255, 0), lerpTime * 0.5),
                         middle, life, 0, 0.5, 0.5, 1, 0,
                         Vector2.Multiply(Vector2.new(2, Remap(life, 0, 1, 4, 1, true)), scale), Vector2.Multiply(Vector2.One, scale));
    }

    DrawSparkle(opacity, dir, at, drawColor, shineColor, counter, fadeInStart, fadeInEnd, fadeOutStart, fadeOutEnd, rotation, scale, fatness) {
        const texture = TextureAssets.Extra[98].Value;
        const sb = Main.spriteBatch;
        const Draw = (...args) => sb['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)'](...args);
        const origin = Vector2.new(texture.Width / 2, texture.Height / 2);
        const lerp = GetLerpValue(fadeInStart, fadeInEnd, counter, true) * GetLerpValue(fadeOutEnd, fadeOutStart, counter, true);
        const big = Color.Multiply(shineColor, opacity * 0.5 * lerp);
        big.A = 0;
        const small = Color.Multiply(drawColor, 0.5 * lerp);
        const leftRight = Vector2.Multiply(Vector2.new(fatness.X * 0.5, scale.X), lerp);
        const upDown = Vector2.Multiply(Vector2.new(fatness.Y * 0.5, scale.Y), lerp);
        Draw(texture, at, null, big, MathHelper.PiOver2 + rotation, origin, leftRight, dir, 0);
        Draw(texture, at, null, big, rotation, origin, upDown, dir, 0);
        Draw(texture, at, null, small, MathHelper.PiOver2 + rotation, origin, Vector2.Multiply(leftRight, 0.6), dir, 0);
        Draw(texture, at, null, small, rotation, origin, Vector2.Multiply(upDown, 0.6), dir, 0);
    }
}
