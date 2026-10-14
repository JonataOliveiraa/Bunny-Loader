import { ExampleLastPrismHoldout } from './ExampleLastPrismHoldout.js';

const { Main } = Terraria;
const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const { TextureAssets } = Terraria.GameContent;
const { Collision } = Terraria;
const LaserScan = Collision['void LaserScan(Vector2 samplingPoint, Vector2 directionUnit, float samplingWidth, float maxDistance, float[] samples)'];
const CanHitLine = Collision['bool CanHitLine(Vector2 Position1, int Width1, int Height1, Vector2 Position2, int Width2, int Height2)'];
const CheckAABBvLineCollision = Collision['bool CheckAABBvLineCollision(Vector2 objectPosition, Vector2 objectDimensions, Vector2 lineStart, Vector2 lineEnd, float lineWidth, ref float collisionPoint)'];
const NewDustDirect = Terraria.Dust['Dust NewDustDirect(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
const HslToRgb = Main['Color hslToRgb(float Hue, float Saturation, float Luminosity, byte a)'];
const AddLight = Terraria.Lighting['void AddLight(Vector2 position, float r, float g, float b)'];
let samples = null;
const DRAW = 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)';

export class ExampleLastPrismBeam extends ModProjectile {
    static PiBeamDivisor = Math.PI / ExampleLastPrismHoldout.NumBeams;
    static MaxDamageMultiplier = 1.5;
    static MaxBeamScale = 1.8;
    static MaxBeamSpread = 2;
    static MaxBeamLength = 2400;
    static BeamHitboxCollisionWidth = 22;
    static NumSamplePoints = 3;
    static BeamLengthChangeFactor = 0.75;
    static VisualEffectThreshold = 0.1;
    static OuterBeamOpacityMultiplier = 0.75;
    static InnerBeamOpacityMultiplier = 0.1;
    static BeamLightBrightness = 0.75;
    static LightStep = 96;
    static BeamColorHue = 0.57;
    static BeamHueVariance = 0.18;
    static BeamColorSaturation = 0.66;
    static BeamColorLightness = 0.53;

    Ripple = null;

    SetDefaults() {
        this.Projectile.width = 18;
        this.Projectile.height = 18;
        this.Projectile.magic = true;
        this.Projectile.penetrate = -1;
        this.Projectile.alpha = 255;
        this.Projectile.tileCollide = false;
        this.Projectile.usesLocalNPCImmunity = true;
        this.Projectile.localNPCHitCooldown = 10;
    }

    get BeamID() { return this.Projectile.ai.val0; }
    get BeamLength() { return this.Projectile.localAI.val1; }
    set BeamLength(value) { this.Projectile.localAI.val1 = value; }

    SendExtraAI(writer) {
        writer.Write(this.BeamLength);
    }

    ReceiveExtraAI(reader) {
        this.BeamLength = reader.ReadSingle();
    }

    AI(proj) {
        const hostPrism = Main.projectile[proj.ai.val1 | 0];
        if (!hostPrism || !hostPrism.active || hostPrism.type !== ModContent.ProjectileType('ExampleLastPrismHoldout')) {
            proj.Kill();
            return;
        }

        const C = ExampleLastPrismBeam;
        const hostPrismDir = Vector2.SafeNormalize(hostPrism.velocity, { X: 0, Y: -1 });
        const charge = hostPrism.ai.val0;
        const chargeRatio = MathHelper.Clamp(charge / ExampleLastPrismHoldout.MaxCharge, 0, 1);

        proj.damage = Math.trunc(hostPrism.damage * this.GetDamageMultiplier(chargeRatio));
        proj.friendly = charge > ExampleLastPrismHoldout.DamageStart;

        const beamIdOffset = this.BeamID - ExampleLastPrismHoldout.NumBeams / 2 + 0.5;
        let beamSpread, spinRate, beamStartSidewaysOffset, beamStartForwardsOffset;
        if (chargeRatio < 1) {
            proj.scale = MathHelper.Lerp(0, C.MaxBeamScale, chargeRatio);
            beamSpread = MathHelper.Lerp(C.MaxBeamSpread, 0, chargeRatio);
            beamStartSidewaysOffset = MathHelper.Lerp(20, 6, chargeRatio);
            beamStartForwardsOffset = MathHelper.Lerp(-21, -17, chargeRatio);
            if (chargeRatio <= 0.66) {
                const phaseRatio = chargeRatio * 1.5;
                proj.Opacity = MathHelper.Lerp(0, 0.4, phaseRatio);
                spinRate = MathHelper.Lerp(20, 16, phaseRatio);
            } else {
                const phaseRatio = (chargeRatio - 0.66) * 3;
                proj.Opacity = MathHelper.Lerp(0.4, 1, phaseRatio);
                spinRate = MathHelper.Lerp(16, 6, phaseRatio);
            }
        } else {
            proj.scale = C.MaxBeamScale;
            proj.Opacity = 1;
            beamSpread = 0;
            spinRate = 6;
            beamStartSidewaysOffset = 6;
            beamStartForwardsOffset = -17;
        }

        const deviationAngle = (charge + beamIdOffset * spinRate) / (spinRate * ExampleLastPrismHoldout.NumBeams) * MathHelper.TwoPi;
        const unitRot = { X: -Math.sin(deviationAngle), Y: Math.cos(deviationAngle) };
        const hostPrismAngle = Vector2.ToRotation(hostPrism.velocity);
        const beamSpanVector = Vector2.RotatedBy({ X: unitRot.X * 4, Y: unitRot.Y * beamStartSidewaysOffset }, hostPrismAngle);
        const sinusoidYOffset = unitRot.Y * C.PiBeamDivisor * beamSpread;

        const forward = 16 + beamStartForwardsOffset;
        proj.Center = Vector2.new(
            hostPrism.Center.X + hostPrismDir.X * forward + beamSpanVector.X,
            hostPrism.Center.Y + hostPrismDir.Y * forward - hostPrism.gfxOffY + beamSpanVector.Y);

        proj.velocity = Vector2.SafeNormalize(Vector2.RotatedBy(hostPrismDir, sinusoidYOffset), { X: 0, Y: -1 });
        proj.rotation = Vector2.ToRotation(proj.velocity);

        const hitscanBeamLength = this.PerformBeamHitscan(proj, hostPrism, chargeRatio >= 1);
        this.BeamLength = MathHelper.Lerp(this.BeamLength, hitscanBeamLength, C.BeamLengthChangeFactor);

        const beamColor = this.GetOuterBeamColor();
        if (chargeRatio >= C.VisualEffectThreshold) {
            this.ProduceBeamDust(proj, beamColor);
            if (Main.netMode !== 2) this.ProduceRipples(proj, chargeRatio);
        }
        this.CastLight(proj, beamColor, chargeRatio);
    }

    GetDamageMultiplier(chargeRatio) {
        const f = chargeRatio * chargeRatio * chargeRatio;
        return MathHelper.Lerp(1, ExampleLastPrismBeam.MaxDamageMultiplier, f);
    }

    PerformBeamHitscan(proj, prism, fullCharge) {
        let samplingPoint = fullCharge ? prism.Center : proj.Center;
        const player = Main.player[proj.owner];
        if (!CanHitLine(player.Center, 0, 0, prism.Center, 0, 0)) samplingPoint = player.Center;

        if (!samples) samples = System.Single.newArray(ExampleLastPrismBeam.NumSamplePoints);
        LaserScan(samplingPoint, proj.velocity, 0, ExampleLastPrismBeam.MaxBeamLength, samples);
        let averageLengthSample = 0;
        for (let i = 0; i < ExampleLastPrismBeam.NumSamplePoints; ++i) averageLengthSample += samples[i];
        return averageLengthSample / ExampleLastPrismBeam.NumSamplePoints;
    }

    Colliding(proj, projHitbox, targetHitbox) {
        const a = projHitbox, b = targetHitbox;
        if (a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height) return true;

        const beamEndPos = Vector2.Add(proj.Center, Vector2.Multiply(proj.velocity, this.BeamLength));
        return CheckAABBvLineCollision(Vector2.new(b.X, b.Y), Vector2.new(b.Width, b.Height), proj.Center, beamEndPos,
            ExampleLastPrismBeam.BeamHitboxCollisionWidth * proj.scale, new Ref(0));
    }

    PreDraw(proj) {
        if (proj.velocity.X === 0 && proj.velocity.Y === 0) return false;

        const texture = TextureAssets.Projectile[this.Type].Value;
        const offset = proj.scale * 10.5;
        const start = Vector2.new(
            Math.floor(proj.Center.X) + proj.velocity.X * offset - Main.screenPosition.X,
            Math.floor(proj.Center.Y) + proj.velocity.Y * offset - Main.screenPosition.Y);
        const visualBeamLength = this.BeamLength - 14.5 * proj.scale * proj.scale;
        const end = Vector2.Add(start, Vector2.Multiply(proj.velocity, visualBeamLength));

        const C = ExampleLastPrismBeam;
        this.DrawBeam(texture, start, end, proj.scale, Color.Multiply(this.GetOuterBeamColor(), C.OuterBeamOpacityMultiplier * proj.Opacity));
        this.DrawBeam(texture, start, end, proj.scale * 0.5, Color.Multiply(Color.White, C.InnerBeamOpacityMultiplier * proj.Opacity));
        return false;
    }

    // O Utils.DrawLaser com o DelegateMethods.RainbowLaserDraw do jogo: a ponta,
    // o corpo e o fim. O jogo repete o corpo a cada 28 px; aqui ele é esticado
    // num desenho só (a textura do corpo é igual ao longo do feixe), o que
    // poupa dezenas de chamadas ao jogo por feixe.
    DrawBeam(texture, start, end, scale, color) {
        const dx = end.X - start.X, dy = end.Y - start.Y;
        const length = Math.hypot(dx, dy);
        if (!(length > 0)) return;

        const dir = { X: dx / length, Y: dy / length };
        const rotation = Math.atan2(dir.Y, dir.X) - MathHelper.PiOver2;
        const draw = (position, frame, origin, scaleY) => Main.spriteBatch[DRAW](texture, position, frame, color, rotation, origin,
            Vector2.new(scale, scaleY), SpriteEffects.None, 0);

        draw(start, Rectangle.new(0, 0, 26, 22), Vector2.new(13, 11), scale);
        const left = length - 33 * scale;
        let at = Vector2.new(start.X + dir.X * 11 * scale, start.Y + dir.Y * 11 * scale);
        if (left > 0) {
            draw(at, Rectangle.new(0, 25, 26, 28), Vector2.new(13, 0), left / 28);
            at = Vector2.new(at.X + dir.X * left, at.Y + dir.Y * left);
        }
        draw(at, Rectangle.new(0, 56, 26, 22), Vector2.new(13, 1), scale);
    }

    GetOuterBeamColor() {
        const C = ExampleLastPrismBeam;
        const hue = (this.BeamID / ExampleLastPrismHoldout.NumBeams) % C.BeamHueVariance + C.BeamColorHue;
        const c = HslToRgb(hue, C.BeamColorSaturation, C.BeamColorLightness, 255);
        c.A = 64;
        return c;
    }

    ProduceBeamDust(proj, beamColor) {
        const type = 15;
        const reach = this.BeamLength - 14.5 * proj.scale;
        const endPosition = Vector2.new(proj.Center.X + proj.velocity.X * reach, proj.Center.Y + proj.velocity.Y * reach);
        const angle = proj.rotation + (Rand.NextChance() ? 1 : -1) * MathHelper.PiOver2;
        const startDistance = Rand.NextFloat(1, 1.8);
        const scale = Rand.NextFloat(0.7, 1.1);
        const dust = NewDustDirect(endPosition, 0, 0, type, Math.cos(angle) * startDistance, Math.sin(angle) * startDistance, 0, beamColor, scale);
        dust.color = beamColor;
        dust.noGravity = true;
        if (proj.scale > 1) {
            dust.velocity = Vector2.Multiply(dust.velocity, proj.scale);
            dust.scale *= proj.scale;
        }
    }

    // No tModLoader o feixe agita a água (WaterShaderData.QueueRipple). Aqui a
    // onda é na tela: um ScreenPresets.Ripple contínuo onde os feixes batem,
    // que o primeiro feixe cria e leva junto. Mais forte com mais carga.
    ProduceRipples(proj, chargeRatio) {
        if (this.BeamID !== 0) return;

        const reach = this.BeamLength - 14.5 * proj.scale;
        const end = { X: proj.Center.X + proj.velocity.X * reach, Y: proj.Center.Y + proj.velocity.Y * reach };
        if (!this.Ripple || !this.Ripple.Active) {
            this.Ripple = ScreenPresets.Ripple.Spawn(end, { radius: 72, duration: 0, strength: 3, wavelength: 14, speed: 2.5, fadeIn: 20 });
        }
        this.Ripple.Center = end;
        this.Ripple.Intensity = chargeRatio;
    }

    // O DelegateMethods.CastLight pelo Utils.PlotTileLine, que pede um delegate:
    // aqui, uma luz a cada LightStep px ao longo do feixe.
    CastLight(proj, beamColor, chargeRatio) {
        const k = ExampleLastPrismBeam.BeamLightBrightness * chargeRatio / 255;
        if (k <= 0) return;

        const r = beamColor.R * k, g = beamColor.G * k, b = beamColor.B * k;
        for (let d = 0; d <= this.BeamLength; d += ExampleLastPrismBeam.LightStep) {
            AddLight(Vector2.new(proj.Center.X + proj.velocity.X * d, proj.Center.Y + proj.velocity.Y * d), r, g, b);
        }
    }

    OnKill() {
        if (this.Ripple) this.Ripple.Kill();
    }
}
