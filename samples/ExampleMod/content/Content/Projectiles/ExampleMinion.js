const { ProjectileID } = Terraria.ID;
const AddLight = Terraria.Lighting['void AddLight(Vector2 position, float r, float g, float b)'];
const CanHitLine = Terraria.Collision['bool CanHitLine(Vector2 Position1, int Width1, int Height1, Vector2 Position2, int Width2, int Height2)'];
const minions = new Set();

export class ExampleMinion extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.projFrames[this.Type] = 4;
        ProjectileID.Sets.MinionTargetingFeature[this.Type] = true;
        Terraria.Main.projPet[this.Type] = true;
        ProjectileID.Sets.MinionSacrificable[this.Type] = true;
        ProjectileID.Sets.CultistIsResistantTo[this.Type] = true;
    }

    SetDefaults() {
        this.Projectile.width = 18;
        this.Projectile.height = 28;
        this.Projectile.tileCollide = false;
        this.Projectile.friendly = true;
        this.Projectile.minion = true;
        this.Projectile.minionSlots = 1;
        this.Projectile.penetrate = -1;
    }

    CanCutTiles(proj) {
        return false;
    }

    MinionContactDamage(proj) {
        return true;
    }

    AI(proj) {
        const owner = Terraria.Main.player[proj.owner];
        if (!this.CheckActive(proj, owner)) return;
        const idle = this.GeneralBehavior(proj, owner);
        const target = this.SearchForTargets(proj, owner);
        this.Movement(proj, target, idle);
        this.Visuals(proj);
    }

    CheckActive(proj, owner) {
        const buff = ModBuff.getTypeByName('ExampleMinionBuff');
        if (owner.dead || !owner.active) {
            owner.ClearBuff(buff);
            return false;
        }
        if (owner.FindBuffIndex(buff) >= 0) proj.timeLeft = 2;
        return true;
    }

    GeneralBehavior(proj, owner) {
        const idlePosition = Vector2.new(owner.Center.X + (10 + proj.minionPos * 40) * -owner.direction, owner.Center.Y - 48);
        const vectorToIdlePosition = Vector2.Subtract(idlePosition, proj.Center);
        const distanceToIdlePosition = Vector2.Length(vectorToIdlePosition);

        if (Terraria.Main.myPlayer === owner.whoAmI && distanceToIdlePosition > 2000) {
            proj.position = idlePosition;
            proj.velocity = Vector2.Multiply(proj.velocity, 0.1);
            proj.netUpdate = true;
        }

        const overlapVelocity = 0.04;
        const all = Terraria.Main.projectile;
        let vx = proj.velocity.X, vy = proj.velocity.Y;
        minions.add(proj.whoAmI);
        for (const i of minions) {
            const other = all[i];
            if (!other.active || other.type !== this.Type) {
                minions.delete(i);
                continue;
            }
            if (i === proj.whoAmI || other.owner !== proj.owner) continue;
            if (Math.abs(proj.position.X - other.position.X) + Math.abs(proj.position.Y - other.position.Y) >= proj.width) continue;
            vx += proj.position.X < other.position.X ? -overlapVelocity : overlapVelocity;
            vy += proj.position.Y < other.position.Y ? -overlapVelocity : overlapVelocity;
        }
        proj.velocity = Vector2.new(vx, vy);
        return { vectorToIdlePosition, distanceToIdlePosition };
    }

    SearchForTargets(proj, owner) {
        let distanceFromTarget = 700;
        let targetCenter = proj.position;
        let foundTarget = false;

        if (owner.HasMinionAttackTargetNPC) {
            const npc = Terraria.Main.npc[owner.MinionAttackTargetNPC];
            const between = Vector2.Distance(npc.Center, proj.Center);
            if (between < 2000) {
                distanceFromTarget = between;
                targetCenter = npc.Center;
                foundTarget = true;
            }
        }

        if (!foundTarget) {
            const npcs = Terraria.Main.npc;
            for (let i = 0; i < npcs.length - 1; i++) {
                const npc = npcs[i];
                if (!npc.active || !npc.CanBeChasedBy(null, false)) continue;
                const between = Vector2.Distance(npc.Center, proj.Center);
                const closest = Vector2.Distance(proj.Center, targetCenter) > between;
                const inRange = between < distanceFromTarget;
                const lineOfSight = CanHitLine(proj.position, proj.width, proj.height, npc.position, npc.width, npc.height);
                const closeThroughWall = between < 100;
                if (((closest && inRange) || !foundTarget) && (lineOfSight || closeThroughWall)) {
                    distanceFromTarget = between;
                    targetCenter = npc.Center;
                    foundTarget = true;
                }
            }
        }

        proj.friendly = foundTarget;
        return { foundTarget, distanceFromTarget, targetCenter };
    }

    Movement(proj, target, idle) {
        let speed = 8;
        let inertia = 20;
        const steer = (toward) => {
            const direction = Vector2.Multiply(Vector2.Normalize(toward), speed);
            proj.velocity = Vector2.Divide(Vector2.Add(Vector2.Multiply(proj.velocity, inertia - 1), direction), inertia);
        };

        if (target.foundTarget) {
            if (target.distanceFromTarget > 40) steer(Vector2.Subtract(target.targetCenter, proj.Center));
            return;
        }
        if (idle.distanceToIdlePosition > 600) {
            speed = 12;
            inertia = 60;
        } else {
            speed = 4;
            inertia = 80;
        }
        if (idle.distanceToIdlePosition > 20) {
            steer(idle.vectorToIdlePosition);
        } else if (proj.velocity.X === 0 && proj.velocity.Y === 0) {
            proj.velocity = Vector2.new(-0.15, -0.05);
        }
    }

    Visuals(proj) {
        proj.rotation = proj.velocity.X * 0.05;
        if (++proj.frameCounter >= 5) {
            proj.frameCounter = 0;
            if (++proj.frame >= Terraria.Main.projFrames[this.Type]) proj.frame = 0;
        }
        AddLight(proj.Center, 0.78, 0.78, 0.78);
    }
}
