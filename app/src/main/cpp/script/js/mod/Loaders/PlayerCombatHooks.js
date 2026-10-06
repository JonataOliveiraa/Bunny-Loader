class PlayerCombatHooks {
    static #attack = null;
    static #strike = false;
    static #hit = null;
    static #canHitAlways = false;
    static #collision = false;
    static PvpAttack = null;

    static Install(cls) {
        const want = PlayerLoader.Wants;
        if (['CanHitNPC', 'CanHitNPCWithItem', 'CanHitNPCWithProj'].some(name => Hooks.Overrides(cls, ModPlayer, name))) {
            PlayerCombatHooks.#canHitAlways = true;
            bl.hookFlags.set('player.CanHitNPCActive', true);
            Hooks.Once('player.CanHitNPC', PlayerCombatHooks.#CanHitNPC);
        }
        want(cls, ['CanHitNPCWithItem', 'CanMeleeAttackCollideWithNPC', 'ModifyHitNPC', 'ModifyHitNPCWithItem', 'OnHitNPC', 'OnHitNPCWithItem'],
            'player.ItemAttackGlobal', PlayerCombatHooks.InstallNPCItems);
        if (Hooks.Overrides(cls, ModPlayer, 'CanMeleeAttackCollideWithNPC')) PlayerCombatHooks.InstallCollision();
        want(cls, ['ModifyHitNPC', 'ModifyHitNPCWithProj', 'OnHitNPC', 'OnHitNPCWithProj'], 'player.ProjectileAttack', PlayerCombatHooks.#ProjectileAttack);
        want(cls, ['CanHitNPC', 'ModifyHitNPC', 'OnHitNPC'], 'player.DirectAttack', () => {
            Terraria.Player['void ApplyDamageToNPC(NPC npc, int damage, float knockback, int direction, bool crit, PlayerNPCHitSource hitSource)'].hook(
                (original, player, npc, damage, knockback, direction, crit, source) => {
                    if (PlayerLoader.Veto(player, 'CanHitNPC', npc)) return;
                    return PlayerCombatHooks.#WithAttack({ player, target: npc, checked: true }, () => original(player, npc, damage, knockback, direction, crit, source));
                });
        });
        want(cls, ['ModifyHitNPC', 'ModifyHitNPCWithItem', 'ModifyHitNPCWithProj', 'OnHitNPC', 'OnHitNPCWithItem', 'OnHitNPCWithProj'],
            'player.StrikeNPC', PlayerCombatHooks.#StrikeNPC);
        if (Hooks.Overrides(cls, ModPlayer, 'CanHitPvp')) {
            ItemCombatHooks.All('player.PvpAttack');
            PlayerCombatHooks.InstallPvp();
        }
        want(cls, ['OnHitAnything'], 'player.OnHitAnything', () => {
            Terraria.Player['void OnHit(float x, float y, Entity victim)'].hook((original, player, x, y, victim) => {
                original(player, x, y, victim);
                PlayerLoader.Call(player, 'OnHitAnything', x, y, victim);
            });
        });
        if (Hooks.Overrides(cls, ModPlayer, 'MeleeEffects')) {
            ItemCombatHooks.All('player.MeleeEffects');
            PlayerCombatHooks.InstallMeleeEffects();
        }
        want(cls, ['EmitEnchantmentVisualsAt'], 'player.Enchantments', () => {
            Terraria.Projectile['void EmitEnchantmentVisualsAt(Vector2 boxPosition, int boxWidth, int boxHeight)'].hook((original, projectile, position, width, height) => {
                original(projectile, position, width, height);
                const player = PlayerCombatHooks.Owner(projectile);
                if (player) PlayerLoader.Call(player, 'EmitEnchantmentVisualsAt', projectile, position, width, height);
            });
        });
    }

    static InstallNPCItems() {
        ItemCombatHooks.All('player.ItemAttack');
        PlayerCombatHooks.InstallItemCombat();
    }

    static InstallItemCombat() {
        Hooks.Once('player.ItemAttack', PlayerCombatHooks.#ItemAttack);
        Hooks.Once('player.CanHitNPC', PlayerCombatHooks.#CanHitNPC);
        Hooks.Once('player.StrikeNPC', PlayerCombatHooks.#StrikeNPC);
    }

    static InstallCollision() { Hooks.Once('player.MeleeCollision', PlayerCombatHooks.#MeleeCollision); }

    static InstallMeleeEffects() {
        Hooks.Once('player.MeleeEffects', () => {
            Terraria.Player['void ItemCheck_EmitUseVisuals(Item sItem, Rectangle itemRectangle)'].hook((original, player, item, rect) => {
                original(player, item, rect);
                ItemCombatHooks.Call(item, 'MeleeEffects', player, rect);
                PlayerLoader.Call(player, 'MeleeEffects', item, rect);
            }, ItemCombatHooks.Filter('player.MeleeEffects'));
        });
    }

    static InstallPvp() {
        Hooks.Once('player.PvpAttack', () => {
            Terraria.Player['void ItemCheck_MeleeHitPVP(Item sItem, Rectangle itemRectangle, int damage, float knockBack)'].hook((original, player, item, rect, damage, knockback) => {
                const outer = PlayerCombatHooks.PvpAttack;
                PlayerCombatHooks.PvpAttack = { player, item };
                PlayerLoader.SetPvpActive(true);
                try { return original(player, item, rect, damage, knockback); }
                finally {
                    PlayerCombatHooks.PvpAttack = outer;
                    PlayerLoader.SetPvpActive(!!outer);
                }
            }, ItemCombatHooks.Filter('player.PvpAttack'));
        });
    }

    static InstallNPCProjectiles() {
        Hooks.Once('player.ProjectileAttack', PlayerCombatHooks.#ProjectileAttack);
        Hooks.Once('player.StrikeNPC', PlayerCombatHooks.#StrikeNPC);
    }

    static RecordIncoming(npc, damage, knockback, direction, crit) {
        const current = PlayerCombatHooks.#hit;
        if (!current || bl.addressOf(current.npc) !== bl.addressOf(npc)) return;
        Object.assign(current.hit, { SourceDamage: damage, Knockback: knockback, HitDirection: direction, Crit: crit });
    }

    static #CanHitNPC() {
        Terraria.Player['bool CanNPCBeHitByPlayerOrPlayerProjectile(NPC npc, Projectile projectile)'].hook((original, player, npc, projectile) => {
            const attack = PlayerCombatHooks.#attack;
            const checked = attack && attack.checked && bl.addressOf(attack.player) === bl.addressOf(player)
                && bl.addressOf(attack.target) === bl.addressOf(npc);
            if (!checked && PlayerLoader.Veto(player, 'CanHitNPC', npc)) return false;
            const decision = projectile
                ? PlayerLoader.Nullable(player, 'CanHitNPCWithProj', projectile, npc)
                : attack && attack.item ? PlayerLoader.Nullable(player, 'CanHitNPCWithItem', attack.item, npc) : null;
            const npcDecision = !projectile && checked ? attack.npcCanHit : null;
            const itemDecision = !projectile && checked ? attack.itemCanHit : null;
            if (decision === false || npcDecision === false || itemDecision === false) return false;
            if (decision === true || npcDecision === true || itemDecision === true) {
                if (!projectile && checked && npc.friendly) {
                    attack.friendly = npc.friendly;
                    attack.restoreFriendly = true;
                    npc.friendly = false;
                }
                return true;
            }
            return original(player, npc, projectile);
        }, { flag: 'player.CanHitNPCActive' });
    }

    static #ProjectileAttack() {
        Terraria.Projectile['void Damage_PVE(ref Rectangle projRectangle, float projectileSpecificDamageMultiplier)'].hook((original, projectile, rect, multiplier) => {
            const player = PlayerCombatHooks.Owner(projectile);
            return PlayerCombatHooks.#WithAttack({ player, projectile }, () => original(projectile, rect, multiplier));
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
        if (!PlayerCombatHooks.#canHitAlways) bl.hookFlags.set('player.CanHitNPCActive', !!attack);
        if (PlayerCombatHooks.#collision) bl.hookFlags.set('player.MeleeCollisionActive', attack?.collision === true);
        try { return run(); }
        finally {
            if (attack?.restoreFriendly) attack.target.friendly = attack.friendly;
            PlayerCombatHooks.#attack = outer;
            if (PlayerCombatHooks.#strike) bl.hookFlags.set('player.Attack', !!outer);
            if (!PlayerCombatHooks.#canHitAlways) bl.hookFlags.set('player.CanHitNPCActive', !!outer);
            if (PlayerCombatHooks.#collision) bl.hookFlags.set('player.MeleeCollisionActive', outer?.collision === true);
        }
    }

    static #ItemAttack() {
        Terraria.Player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'].hook(
            (original, player, item, rect, damage, knockback, index) => {
                const target = Terraria.Main.npc[index];
                if (!target || PlayerLoader.Veto(player, 'CanHitNPC', target)) return;
                const itemCanHit = ItemCombatHooks.Call(item, 'CanHitNPC', player, target);
                if (itemCanHit === false) return;
                const npcCanHit = NPCLoader.Call(target, 'CanBeHitByItem', player, item);
                if (npcCanHit === false) return;
                const itemCollision = ItemCombatHooks.Call(item, 'CanMeleeAttackCollideWithNPC', player, rect, target);
                const playerCollision = PlayerLoader.Nullable(player, 'CanMeleeAttackCollideWithNPC', item, rect, target);
                if (itemCollision === false || playerCollision === false) return;
                const collision = itemCollision === true || playerCollision === true ? true : null;
                return PlayerCombatHooks.#WithAttack({ player, item, target, rect, collision, npcCanHit, itemCanHit, checked: true }, () => original(player, item, rect, damage, knockback, index));
            }, ItemCombatHooks.Filter('player.ItemAttack'));
    }

    static #MeleeCollision() {
        PlayerCombatHooks.#collision = true;
        const gate = Terraria.Player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'];
        Microsoft.Xna.Framework.Rectangle['bool Intersects(Rectangle rect)'].hook((original, rect, value) => {
            const attack = PlayerCombatHooks.#attack;
            if (attack && attack.collision === true && PlayerCombatHooks.#SameRect(rect, attack.rect) && PlayerCombatHooks.#SameRect(value, attack.target.Hitbox)) return true;
            return original(rect, value);
        }, { whileIn: gate, flag: 'player.MeleeCollisionActive' });
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
                if (attack.restoreFriendly) npc.friendly = attack.friendly;
                return PlayerCombatHooks.#WithAttack(null, () => {
                    const player = attack.player;
                    const modifiers = { damage, knockBack: knockback, hitDirection: direction, crit,
                        SourceDamage: StatModifier.Default, Knockback: StatModifier.Default,
                        SetCrit() { this.crit = true; }, DisableCrit() { this.crit = false; } };
                    if (player) {
                        if (attack.item) ItemCombatHooks.Call(attack.item, 'ModifyHitNPC', player, npc, modifiers);
                        PlayerLoader.Call(player, 'ModifyHitNPC', npc, modifiers);
                        if (attack.item) PlayerLoader.Call(player, 'ModifyHitNPCWithItem', attack.item, npc, modifiers);
                        if (attack.projectile) PlayerLoader.Call(player, 'ModifyHitNPCWithProj', attack.projectile, npc, modifiers);
                    }
                    const modifiedDamage = modifiers.SourceDamage.ApplyTo(modifiers.damage);
                    const modifiedKnockback = modifiers.Knockback.ApplyTo(modifiers.knockBack);
                    const amount = Number.isFinite(modifiedDamage) ? Math.max(0, Math.floor(modifiedDamage)) : 0;
                    const kb = Number.isFinite(modifiedKnockback) ? Math.max(0, modifiedKnockback) : 0;
                    const hit = { Damage: 0, SourceDamage: amount, Knockback: kb, HitDirection: modifiers.hitDirection, Crit: modifiers.crit };
                    const outer = PlayerCombatHooks.#hit;
                    PlayerCombatHooks.#hit = { npc, hit };
                    let done;
                    try { done = original(npc, amount, kb, modifiers.hitDirection, modifiers.crit, fromNet, owner); }
                    finally { PlayerCombatHooks.#hit = outer; }
                    if (done <= 0) return done;
                    hit.Damage = done;
                    if (attack.item) NPCLoader.Call(npc, 'OnHitByItem', player, attack.item, hit, done);
                    if (attack.projectile) NPCLoader.Call(npc, 'OnHitByProjectile', attack.projectile, hit, done);
                    if (player) {
                        PlayerLoader.Call(player, 'OnHitNPC', npc, hit, done);
                        if (attack.item) PlayerLoader.Call(player, 'OnHitNPCWithItem', attack.item, npc, hit, done);
                        if (attack.projectile) PlayerLoader.Call(player, 'OnHitNPCWithProj', attack.projectile, npc, hit, done);
                    }
                    return done;
                });
            }, { flag: 'player.Attack' });
    }
}
