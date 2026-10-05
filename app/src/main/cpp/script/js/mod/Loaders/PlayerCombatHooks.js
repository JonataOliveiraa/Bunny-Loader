class PlayerCombatHooks {
    static #attack = null;
    static #strike = false;
    static PvpAttack = null;

    static Install(cls) {
        const want = PlayerLoader.Wants;
        want(cls, ['CanHitNPC', 'CanHitNPCWithItem', 'CanHitNPCWithProj'], 'player.CanHitNPC', () => {
            Terraria.Player['bool CanNPCBeHitByPlayerOrPlayerProjectile(NPC npc, Projectile projectile)'].hook((original, player, npc, projectile) => {
                const attack = PlayerCombatHooks.#attack;
                const checked = attack && attack.checked && bl.addressOf(attack.player) === bl.addressOf(player)
                    && bl.addressOf(attack.target) === bl.addressOf(npc);
                if (!checked && PlayerLoader.Veto(player, 'CanHitNPC', npc)) return false;
                const decision = projectile
                    ? PlayerLoader.Nullable(player, 'CanHitNPCWithProj', projectile, npc)
                    : attack && attack.item ? PlayerLoader.Nullable(player, 'CanHitNPCWithItem', attack.item, npc) : null;
                return decision === null ? original(player, npc, projectile) : decision;
            });
        });
        want(cls, ['CanHitNPCWithItem', 'CanMeleeAttackCollideWithNPC', 'ModifyHitNPC', 'ModifyHitNPCWithItem', 'OnHitNPC', 'OnHitNPCWithItem'],
            'player.ItemAttack', PlayerCombatHooks.#ItemAttack);
        want(cls, ['CanMeleeAttackCollideWithNPC'], 'player.MeleeCollision', PlayerCombatHooks.#MeleeCollision);
        want(cls, ['ModifyHitNPC', 'ModifyHitNPCWithProj', 'OnHitNPC', 'OnHitNPCWithProj'], 'player.ProjectileAttack', () => {
            Terraria.Projectile['void Damage_PVE(ref Rectangle projRectangle, float projectileSpecificDamageMultiplier)'].hook((original, projectile, rect, multiplier) => {
                const player = PlayerCombatHooks.Owner(projectile);
                return PlayerCombatHooks.#WithAttack(player ? { player, projectile } : null, () => original(projectile, rect, multiplier));
            });
        });
        want(cls, ['CanHitNPC', 'ModifyHitNPC', 'OnHitNPC'], 'player.DirectAttack', () => {
            Terraria.Player['void ApplyDamageToNPC(NPC npc, int damage, float knockback, int direction, bool crit, PlayerNPCHitSource hitSource)'].hook(
                (original, player, npc, damage, knockback, direction, crit, source) => {
                    if (PlayerLoader.Veto(player, 'CanHitNPC', npc)) return;
                    return PlayerCombatHooks.#WithAttack({ player, target: npc, checked: true }, () => original(player, npc, damage, knockback, direction, crit, source));
                });
        });
        want(cls, ['ModifyHitNPC', 'ModifyHitNPCWithItem', 'ModifyHitNPCWithProj', 'OnHitNPC', 'OnHitNPCWithItem', 'OnHitNPCWithProj'],
            'player.StrikeNPC', PlayerCombatHooks.#StrikeNPC);
        want(cls, ['CanHitPvp'], 'player.PvpAttack', () => {
            Terraria.Player['void ItemCheck_MeleeHitPVP(Item sItem, Rectangle itemRectangle, int damage, float knockBack)'].hook((original, player, item, rect, damage, knockback) => {
                const outer = PlayerCombatHooks.PvpAttack;
                PlayerCombatHooks.PvpAttack = { player, item };
                try { return original(player, item, rect, damage, knockback); }
                finally { PlayerCombatHooks.PvpAttack = outer; }
            });
        });
        want(cls, ['OnHitAnything'], 'player.OnHitAnything', () => {
            Terraria.Player['void OnHit(float x, float y, Entity victim)'].hook((original, player, x, y, victim) => {
                original(player, x, y, victim);
                PlayerLoader.Call(player, 'OnHitAnything', x, y, victim);
            });
        });
        want(cls, ['MeleeEffects'], 'player.MeleeEffects', () => {
            Terraria.Player['void ItemCheck_EmitUseVisuals(Item sItem, Rectangle itemRectangle)'].hook((original, player, item, rect) => {
                original(player, item, rect);
                PlayerLoader.Call(player, 'MeleeEffects', item, rect);
            });
        });
        want(cls, ['EmitEnchantmentVisualsAt'], 'player.Enchantments', () => {
            Terraria.Projectile['void EmitEnchantmentVisualsAt(Vector2 boxPosition, int boxWidth, int boxHeight)'].hook((original, projectile, position, width, height) => {
                original(projectile, position, width, height);
                const player = PlayerCombatHooks.Owner(projectile);
                if (player) PlayerLoader.Call(player, 'EmitEnchantmentVisualsAt', projectile, position, width, height);
            });
        });
    }

    static Owner(projectile) {
        const index = projectile.owner;
        return Number.isInteger(index) && index >= 0 && index < 255 ? Terraria.Main.player[index] : null;
    }

    static Cause(source) {
        const npcIndex = source ? source._sourceNPCIndex : -1;
        const projectileIndex = source ? source._sourceProjectileLocalIndex : -1;
        return {
            npc: npcIndex >= 0 && npcIndex < 200 ? Terraria.Main.npc[npcIndex] : null,
            projectile: projectileIndex >= 0 && projectileIndex < 1000 ? Terraria.Main.projectile[projectileIndex] : null,
        };
    }

    static #WithAttack(attack, run) {
        const outer = PlayerCombatHooks.#attack;
        PlayerCombatHooks.#attack = attack;
        if (PlayerCombatHooks.#strike) bl.hookFlags.set('player.Attack', !!attack);
        try { return run(); }
        finally {
            PlayerCombatHooks.#attack = outer;
            if (PlayerCombatHooks.#strike) bl.hookFlags.set('player.Attack', !!outer);
        }
    }

    static #ItemAttack() {
        Terraria.Player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'].hook(
            (original, player, item, rect, damage, knockback, index) => {
                const target = Terraria.Main.npc[index];
                if (!target || PlayerLoader.Veto(player, 'CanHitNPC', target)) return;
                const collision = PlayerLoader.Nullable(player, 'CanMeleeAttackCollideWithNPC', item, rect, target);
                if (collision === false) return;
                return PlayerCombatHooks.#WithAttack({ player, item, target, rect, collision, checked: true }, () => original(player, item, rect, damage, knockback, index));
            });
    }

    static #MeleeCollision() {
        const gate = Terraria.Player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'];
        Microsoft.Xna.Framework.Rectangle['bool Intersects(Rectangle rect)'].hook((original, rect, value) => {
            const attack = PlayerCombatHooks.#attack;
            if (attack && attack.collision === true && PlayerCombatHooks.#SameRect(rect, attack.rect) && PlayerCombatHooks.#SameRect(value, attack.target.Hitbox)) return true;
            return original(rect, value);
        }, { whileIn: gate });
    }

    static #SameRect(a, b) {
        return a && b && a.X === b.X && a.Y === b.Y && a.Width === b.Width && a.Height === b.Height;
    }

    static #StrikeNPC() {
        PlayerCombatHooks.#strike = true;
        Terraria.NPC['int StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool fromNet, int owner)'].hook(
            (original, npc, damage, knockback, direction, crit, fromNet, owner) => {
                const attack = PlayerCombatHooks.#attack;
                if (!attack || fromNet || attack.target && bl.addressOf(attack.target) !== bl.addressOf(npc)) return original(npc, damage, knockback, direction, crit, fromNet, owner);
                return PlayerCombatHooks.#WithAttack(null, () => {
                    const player = attack.player;
                    const modifiers = { damage, knockBack: knockback, hitDirection: direction, crit,
                        SourceDamage: StatModifier.Default, Knockback: StatModifier.Default,
                        SetCrit() { this.crit = true; }, DisableCrit() { this.crit = false; } };
                    PlayerLoader.Call(player, 'ModifyHitNPC', npc, modifiers);
                    if (attack.item) PlayerLoader.Call(player, 'ModifyHitNPCWithItem', attack.item, npc, modifiers);
                    if (attack.projectile) PlayerLoader.Call(player, 'ModifyHitNPCWithProj', attack.projectile, npc, modifiers);
                    const amount = Math.max(0, Math.floor(modifiers.SourceDamage.ApplyTo(modifiers.damage)));
                    const kb = Math.max(0, modifiers.Knockback.ApplyTo(modifiers.knockBack));
                    const done = original(npc, amount, kb, modifiers.hitDirection, modifiers.crit, fromNet, owner);
                    if (done <= 0) return done;
                    const hit = { Damage: done, SourceDamage: amount, Knockback: kb, HitDirection: modifiers.hitDirection, Crit: modifiers.crit };
                    PlayerLoader.Call(player, 'OnHitNPC', npc, hit, done);
                    if (attack.item) PlayerLoader.Call(player, 'OnHitNPCWithItem', attack.item, npc, hit, done);
                    if (attack.projectile) PlayerLoader.Call(player, 'OnHitNPCWithProj', attack.projectile, npc, hit, done);
                    return done;
                });
            }, { flag: 'player.Attack' });
    }
}
