const { ProjectileDrawLayerID } = Terraria.ID;
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

export class ExampleLaserHoldout extends ModProjectile {
    HoldoutDistance = 30;
    ManaConsumptionRate = 5;

    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.projFrames[this.Type] = 5;
    }

    SetDefaults() {
        this.Projectile.width = 22;
        this.Projectile.height = 20;
        this.Projectile.alpha = 100;
        this.Projectile.friendly = true;
        this.Projectile.penetrate = -1;
        this.Projectile.tileCollide = false;
    }

    GetAlpha() {
        return Color.White;
    }

    AI(proj) {
        const ai = new ProjAI(proj);
        const player = Terraria.Main.player[proj.owner];
        const hand = player['Vector2 RotatedRelativePoint(Vector2 pos, bool reverseRotation, bool addGfxOffY)'](player.MountedCenter, true, true);
        ai[0] += 1;

        if (++proj.frameCounter >= 3) {
            proj.frameCounter = 0;
            if (++proj.frame >= Terraria.Main.projFrames[this.Type]) proj.frame = 0;
        }

        proj.rotation = Vector2.ToRotation(proj.velocity) + MathHelper.PiOver2;
        proj.spriteDirection = proj.direction;
        proj.Center = Vector2.Add(player.MountedCenter, Vector2.Multiply(Vector2.Normalize(proj.velocity), this.HoldoutDistance));
        player.ChangeDir(proj.direction);
        player.heldProj = proj.whoAmI;
        proj.drawLayer = ProjectileDrawLayerID.HeldProj;
        player.itemTime = 2;
        player.itemAnimation = 2;
        player.itemRotation = Vector2.ToRotation(Vector2.Multiply(proj.velocity, proj.direction));

        if (proj.owner === Terraria.Main.myPlayer) {
            this.Aim(proj, hand, player.HeldItem.shootSpeed);
            const pay = this.ShouldConsumeMana(ai);
            const hasMana = !pay || player['bool CheckMana(int amount, bool pay, bool blockQuickMana)'](player.HeldItem.mana, true, false);
            const inUse = player.channel && hasMana && !player.noItems && !player.CCed;
            if (inUse && ai[0] === 1) this.ShootBeam(proj, player);
            else if (!inUse) proj.Kill();
        }
        proj.timeLeft = 2;
    }

    Aim(proj, source, speed) {
        let aim = Vector2.SafeNormalize(Vector2.Subtract(Terraria.Main.MouseWorld, source), { X: 0, Y: -1 });
        aim = Vector2.Normalize(Vector2.Lerp(Vector2.Normalize(proj.velocity), aim, 0.08));
        aim = Vector2.Multiply(aim, speed);
        if (aim.X !== proj.velocity.X && aim.Y !== proj.velocity.Y) proj.netUpdate = true;
        proj.velocity = aim;
    }

    ShouldConsumeMana(ai) {
        const consume = ai[1] === 0;
        ai[1]++;
        if (ai[1] > this.ManaConsumptionRate) ai[1] = 0;
        return consume;
    }

    ShootBeam(proj, player) {
        const dir = Vector2.Normalize(proj.velocity);
        const tip = Vector2.Add(proj.Center, Vector2.Multiply(dir, this.HoldoutDistance));
        const i = NewProjectile(player.GetProjectileSource_Item(player.HeldItem), tip, dir,
                                ModProjectile.getTypeByName('ExampleLaserBeam'), player.HeldItem.damage,
                                proj.knockBack, proj.owner, proj.whoAmI, 0, 0, null);
        Terraria.Main.projectile[i].scale = 0.05;
    }
}
