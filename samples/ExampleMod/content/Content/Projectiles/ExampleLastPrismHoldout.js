import { ExampleLastPrism } from '../Items/Weapons/Magic/ExampleLastPrism.js';

const { Main } = Terraria;
const { ProjectileID, SoundID } = Terraria.ID;
const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const { TextureAssets } = Terraria.GameContent;
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const EntitySpriteDraw = Main['void EntitySpriteDraw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float worthless)'];

export class ExampleLastPrismHoldout extends ModProjectile {
    static NumAnimationFrames = 5;
    static NumBeams = 10;
    static MaxCharge = 180;
    static DamageStart = 30;
    static AimResponsiveness = 0.08;
    static SoundInterval = 20;
    static MaxManaConsumptionDelay = 15;
    static MinManaConsumptionDelay = 5;

    get Texture() { return 'Content/Projectiles/ExampleLaserHoldout'; }

    SetStaticDefaults() {
        Main.projFrames[this.Type] = ExampleLastPrismHoldout.NumAnimationFrames;
    }

    SetDefaults() {
        this.CloneDefaults(ProjectileID.LastPrism);
    }

    CanDamage() {
        return false;
    }

    AI(proj) {
        const ai = new ProjAI(proj);
        const player = Main.player[proj.owner];
        const rrp = player['Vector2 RotatedRelativePoint(Vector2 pos, bool reverseRotation, bool addGfxOffY, int pushFromOriginHack)'](player.MountedCenter, true, true, 0);

        proj.damage = Math.trunc(player.GetDamage(DamageClass.Magic).ApplyTo(player.HeldItem.damage));
        ai[0] += 1;

        this.UpdateAnimation(proj, ai[0]);
        this.PlaySounds(proj, ai[0]);
        this.UpdatePlayerVisuals(proj, player, rrp);

        if (proj.owner === Main.myPlayer) {
            this.UpdateAim(proj, rrp, player.HeldItem.shootSpeed);
            const manaIsAvailable = !this.ShouldConsumeMana(proj, ai) || player['bool CheckMana(int amount, bool pay, bool blockQuickMana)'](player.HeldItem.mana, true, false);
            const stillInUse = player.channel && manaIsAvailable && !player.noItems && !player.CCed;
            if (stillInUse && ai[0] === 1) {
                this.FireBeams(proj);
            } else if (!stillInUse) {
                proj.Kill();
            }
        }
        proj.timeLeft = 2;
    }

    UpdateAnimation(proj, frameCounter) {
        proj.frameCounter++;
        const max = ExampleLastPrismHoldout.MaxCharge;
        const framesPerAnimationUpdate = frameCounter >= max ? 2 : frameCounter >= max * 0.66 ? 3 : 4;
        if (proj.frameCounter >= framesPerAnimationUpdate) {
            proj.frameCounter = 0;
            if (++proj.frame >= ExampleLastPrismHoldout.NumAnimationFrames) proj.frame = 0;
        }
    }

    PlaySounds(proj, frameCounter) {
        if (proj.soundDelay <= 0) {
            proj.soundDelay = ExampleLastPrismHoldout.SoundInterval;
            if (frameCounter > 1) SoundEngine.PlaySound(SoundID.Item15, proj.position);
        }
    }

    UpdatePlayerVisuals(proj, player, playerHandPos) {
        proj.Center = playerHandPos;
        proj.rotation = Vector2.ToRotation(proj.velocity) + MathHelper.PiOver2;
        proj.spriteDirection = proj.direction;

        player.ChangeDir(proj.direction);
        player.heldProj = proj.whoAmI;
        player.itemTime = 2;
        player.itemAnimation = 2;
        player.itemRotation = Vector2.ToRotation(Vector2.Multiply(proj.velocity, proj.direction));
    }

    ShouldConsumeMana(proj, ai) {
        const local = new ProjAI(proj, true);
        if (local[0] === 0) {
            ai[1] = local[0] = ExampleLastPrismHoldout.MaxManaConsumptionDelay;
            return true;
        }

        const consume = ai[0] === ai[1];
        if (consume) {
            local[0] = MathHelper.Clamp(local[0] - 1, ExampleLastPrismHoldout.MinManaConsumptionDelay, ExampleLastPrismHoldout.MaxManaConsumptionDelay);
            ai[1] += local[0];
        }
        return consume;
    }

    UpdateAim(proj, source, speed) {
        let aim = Vector2.SafeNormalize(Vector2.Subtract(Main.MouseWorld, source), { X: 0, Y: -1 });
        aim = Vector2.Normalize(Vector2.Lerp(Vector2.Normalize(proj.velocity), aim, ExampleLastPrismHoldout.AimResponsiveness));
        aim = Vector2.Multiply(aim, speed);
        if (aim.X !== proj.velocity.X || aim.Y !== proj.velocity.Y) proj.netUpdate = true;
        proj.velocity = aim;
    }

    // O tModLoader passa o ProjectileKey do prisma no ai[1] dos feixes; aqui
    // vai o índice (whoAmI), e o feixe confere o tipo de quem está nele.
    FireBeams(proj) {
        const beamVelocity = Vector2.SafeNormalize(proj.velocity, { X: 0, Y: -1 });
        const type = ModContent.ProjectileType('ExampleLastPrismBeam');
        const player = Main.player[proj.owner];
        const source = player.GetProjectileSource_Item(player.HeldItem);
        for (let b = 0; b < ExampleLastPrismHoldout.NumBeams; ++b) {
            NewProjectile(source, proj.Center, beamVelocity, type, proj.damage, proj.knockBack, proj.owner, b, proj.whoAmI, 0, null);
        }
        proj.netUpdate = true;
    }

    PreDraw(proj) {
        const effects = proj.spriteDirection === -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        const texture = TextureAssets.Projectile[this.Type].Value;
        const frameHeight = texture.Height / Main.projFrames[this.Type];
        const spriteSheetOffset = frameHeight * proj.frame;
        const position = Vector2.new(Math.floor(proj.Center.X - Main.screenPosition.X), Math.floor(proj.Center.Y + proj.gfxOffY - Main.screenPosition.Y));
        const origin = Vector2.new(texture.Width / 2, frameHeight / 2);
        EntitySpriteDraw(texture, position, Rectangle.new(0, spriteSheetOffset, texture.Width, frameHeight), ExampleLastPrism.OverrideColor,
            proj.rotation, origin, Vector2.new(proj.scale, proj.scale), effects, 0);
        return false;
    }
}
