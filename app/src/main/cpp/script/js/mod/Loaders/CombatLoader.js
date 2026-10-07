// Os golpes, num lugar só, como o CombinedHooks do tModLoader: quem bate e
// quem apanha (item, projétil, NPC, jogador) e os Globais de cada um passam
// pelos mesmos hooks do jogo, na ordem do tModLoader.
//
//   item e projétil no NPC: ProcessHitAgainstNPC / Damage_PVE -> StrikeNPC
//   NPC no NPC: BeHurtByOtherNPC -> StrikeNPCNoInteraction
//   NPC, projétil e PvP no jogador: Player.Hurt
//
// O StrikeNPC do celular recebe o dano bruto (antes da defesa) e o manda ao
// servidor (mensagem 28), que refaz a conta. O HitModifiers calcula o dano
// final como o tModLoader e vira o bruto equivalente, com a defesa somada de
// volta: o servidor chega ao mesmo número.
class CombatLoader {
    static #attack = null;      // o golpe do jogador (item ou projétil) em andamento
    static #npcAttack = null;   // NPC batendo em NPC
    static #strikeAlways = false;
    static #canHitAlways = false;
    static #collision = false;
    static #hurtAlways = false;
    static #hideText = false;
    static PvpAttack = null;

    // ---- quem participa ----

    static #overrides = new Map();
    static #Overrides(cls, Base, name) {
        let byName = CombatLoader.#overrides.get(cls);
        if (!byName) CombatLoader.#overrides.set(cls, byName = new Map());
        let v = byName.get(name);
        if (v === undefined) byName.set(name, v = Hooks.Overrides(cls, Base, name));
        return v;
    }

    // O resultado do de mod e de cada Global, na ordem.
    static #Results(kind, entity, name, args) {
        const out = [];
        if (!entity) return out;
        const side = CombatLoader.#Side(kind);
        const m = side.of(entity);
        if (m && CombatLoader.#Overrides(m.constructor, side.Base, name)) {
            try { out.push(m[name](entity, ...args)); }
            catch (e) { Safe.Report(m.constructor.name + '.' + name, e); }
        }
        const registry = side.registry;
        for (const g of registry.For(entity, name)) {
            try { out.push(g[name](entity, ...args)); }
            catch (e) { Safe.Report(g.constructor.name + '.' + name, e); }
        }
        return out;
    }

    static #Side(kind) {
        switch (kind) {
            case 'item': return { of: ItemLoader.Of, Base: ModItem, registry: globalItems };
            case 'npc': return { of: NPCLoader.Of, Base: ModNPC, registry: globalNPCs };
            default: return { of: ProjectileLoader.Of, Base: ModProjectile, registry: globalProjectiles };
        }
    }

    static Call(kind, entity, name, ...args) { CombatLoader.#Results(kind, entity, name, args); }

    // bool? do tModLoader: algum false veta; senão algum true força; senão null (o jogo decide).
    static Can(kind, entity, name, ...args) {
        let result = null;
        for (const value of CombatLoader.#Results(kind, entity, name, args)) {
            if (value === false) return false;
            if (value === true) result = true;
        }
        return result;
    }

    // bool com padrão true: algum false veta.
    static All(kind, entity, name, ...args) {
        return !CombatLoader.#Results(kind, entity, name, args).includes(false);
    }

    // ---- o que cada lado pede ----

    static #ITEM = ['CanHitNPC', 'ModifyHitNPC', 'OnHitNPC', 'CanMeleeAttackCollideWithNPC'];
    static #ITEM_PVP = ['CanHitPvp', 'ModifyHitPvp', 'OnHitPvp'];
    static #NPC_ITEM = ['CanBeHitByItem', 'ModifyHitByItem', 'OnHitByItem'];
    static #NPC_PROJ = ['CanBeHitByProjectile', 'ModifyHitByProjectile', 'OnHitByProjectile'];
    static #NPC_NPC = ['CanHitNPC', 'CanBeHitByNPC', 'ModifyHitNPC', 'OnHitNPC'];
    static #NPC_PLAYER = ['CanHitPlayer', 'ModifyHitPlayer', 'OnHitPlayer'];
    static #PROJ_NPC = ['CanHitNPC', 'ModifyHitNPC', 'OnHitNPC'];
    static #PROJ_PLAYER = ['CanHitPlayer', 'ModifyHitPlayer', 'OnHitPlayer', 'CanHitPvp'];

    // Global de item: os hooks de golpe por item entram para todo item.
    static WantGlobalItem(cls) {
        const has = (name) => Hooks.Overrides(cls, GlobalItem, name);
        if (CombatLoader.#ITEM.some(has)) {
            ItemCombatHooks.All('player.ItemAttack');
            CombatLoader.InstallItemCombat();
        }
        if (has('CanMeleeAttackCollideWithNPC')) CombatLoader.InstallCollision();
        if (CombatLoader.#ITEM_PVP.some(has)) {
            ItemCombatHooks.All('player.PvpAttack');
            CombatLoader.InstallPvp();
            CombatLoader.InstallHurt(false);
        }
    }

    static WantNPC(cls, Base) {
        const has = (name) => Hooks.Overrides(cls, Base, name);
        if (CombatLoader.#NPC_ITEM.some(has)) {
            ItemCombatHooks.All('player.ItemAttack');
            CombatLoader.InstallItemCombat();
        }
        if (CombatLoader.#NPC_PROJ.some(has)) CombatLoader.InstallProjectileCombat();
        if (CombatLoader.#NPC_NPC.some(has)) CombatLoader.#InstallNPCvsNPC();
        if (CombatLoader.#NPC_PLAYER.some(has)) CombatLoader.InstallHurt(true);
        if (has('ModifyIncomingHit')) {
            CombatLoader.#strikeAlways = true;
            CombatLoader.#InstallStrike();
        }
    }

    static WantProjectile(cls, Base) {
        const has = (name) => Hooks.Overrides(cls, Base, name);
        if (CombatLoader.#PROJ_NPC.some(has)) CombatLoader.InstallProjectileCombat();
        if (CombatLoader.#PROJ_PLAYER.some(has)) CombatLoader.InstallHurt(true);
    }

    static WantPlayer(cls) {
        const want = PlayerLoader.Wants;
        const has = (name) => Hooks.Overrides(cls, ModPlayer, name);
        if (['CanHitNPC', 'CanHitNPCWithItem', 'CanHitNPCWithProj'].some(has)) {
            CombatLoader.#canHitAlways = true;
            bl.hookFlags.set('combat.CanHitNPC', true);
            Hooks.Once('combat.CanHitNPC', CombatLoader.#HookCanHitNPC);
        }
        want(cls, ['CanHitNPCWithItem', 'CanMeleeAttackCollideWithNPC', 'ModifyHitNPC', 'ModifyHitNPCWithItem', 'OnHitNPC', 'OnHitNPCWithItem'],
            'combat.PlayerItem', () => {
                ItemCombatHooks.All('player.ItemAttack');
                CombatLoader.InstallItemCombat();
            });
        if (has('CanMeleeAttackCollideWithNPC')) CombatLoader.InstallCollision();
        want(cls, ['ModifyHitNPC', 'ModifyHitNPCWithProj', 'OnHitNPC', 'OnHitNPCWithProj', 'CanHitNPCWithProj'], 'combat.PlayerProjectile',
            CombatLoader.InstallProjectileCombat);
        want(cls, ['CanHitNPC', 'ModifyHitNPC', 'OnHitNPC'], 'combat.DirectAttack', CombatLoader.#HookDirectAttack);
        if (has('CanHitPvp')) {
            ItemCombatHooks.All('player.PvpAttack');
            CombatLoader.InstallPvp();
        }
        const hurt = ['ImmuneTo', 'FreeDodge', 'ConsumableDodge', 'ModifyHurt', 'OnHurt', 'PostHurt', 'CanBeHitByNPC',
            'CanBeHitByProjectile', 'ModifyHitByNPC', 'ModifyHitByProjectile', 'OnHitByNPC', 'OnHitByProjectile',
            'CanHitPvp', 'CanHitPvpWithProj'];
        if (hurt.some(has)) CombatLoader.InstallHurt(true);
        want(cls, ['OnHitAnything'], 'combat.OnHitAnything', () => {
            Terraria.Player['void OnHit(float x, float y, Entity victim)'].hook((original, player, x, y, victim) => {
                original(player, x, y, victim);
                PlayerLoader.Call(player, 'OnHitAnything', x, y, victim);
            });
        });
        if (has('MeleeEffects')) {
            ItemCombatHooks.All('player.MeleeEffects');
            CombatLoader.InstallMeleeEffects();
        }
        want(cls, ['EmitEnchantmentVisualsAt'], 'combat.Enchantments', () => {
            Terraria.Projectile['void EmitEnchantmentVisualsAt(Vector2 boxPosition, int boxWidth, int boxHeight)'].hook((original, projectile, position, width, height) => {
                original(projectile, position, width, height);
                const player = CombatLoader.Owner(projectile);
                if (player) PlayerLoader.Call(player, 'EmitEnchantmentVisualsAt', projectile, position, width, height);
            });
        });
    }

    // ---- instalação ----

    static InstallItemCombat() {
        Hooks.Once('combat.ItemAttack', CombatLoader.#HookItemAttack);
        Hooks.Once('combat.CanHitNPC', CombatLoader.#HookCanHitNPC);
        CombatLoader.#InstallStrike();
    }

    static InstallProjectileCombat() {
        Hooks.Once('combat.ProjectileAttack', CombatLoader.#HookProjectileAttack);
        Hooks.Once('combat.CanHitNPC', CombatLoader.#HookCanHitNPC);
        CombatLoader.#InstallStrike();
    }

    static InstallCollision() { Hooks.Once('combat.MeleeCollision', CombatLoader.#HookMeleeCollision); }

    static InstallMeleeEffects() {
        Hooks.Once('combat.MeleeEffects', () => {
            Terraria.Player['void ItemCheck_EmitUseVisuals(Item sItem, Rectangle itemRectangle)'].hook((original, player, item, rect) => {
                original(player, item, rect);
                ItemCombatHooks.Call(item, 'MeleeEffects', player, rect);
                PlayerLoader.Call(player, 'MeleeEffects', item, rect);
            }, ItemCombatHooks.Filter('player.MeleeEffects'));
        });
    }

    static InstallPvp() {
        CombatLoader.InstallHurt(false);
        Hooks.Once('combat.PvpAttack', () => {
            Terraria.Player['void ItemCheck_MeleeHitPVP(Item sItem, Rectangle itemRectangle, int damage, float knockBack)'].hook((original, player, item, rect, damage, knockback) => {
                const outer = CombatLoader.PvpAttack;
                CombatLoader.PvpAttack = { player, item };
                CombatLoader.#SetHurtActive(true);
                try { return original(player, item, rect, damage, knockback); }
                finally {
                    CombatLoader.PvpAttack = outer;
                    CombatLoader.#SetHurtActive(!!outer);
                }
            }, ItemCombatHooks.Filter('player.PvpAttack'));
        });
    }

    // always: o Player.Hurt entra no JS em todo golpe (NPC, projétil); sem
    // isso, só durante o golpe corpo a corpo PvP.
    static InstallHurt(always) {
        if (always && !CombatLoader.#hurtAlways) {
            CombatLoader.#hurtAlways = true;
            bl.hookFlags.set('combat.Hurt', true);
        }
        Hooks.Once('combat.Hurt', CombatLoader.#HookHurt);
    }

    static #SetHurtActive(active) {
        if (!CombatLoader.#hurtAlways) bl.hookFlags.set('combat.Hurt', active);
    }

    static #InstallStrike() {
        if (CombatLoader.#strikeAlways) bl.hookFlags.set('combat.Strike', true);
        Hooks.Once('combat.Strike', CombatLoader.#HookStrike);
    }

    static #InstallNPCvsNPC() {
        CombatLoader.#InstallStrike();
        Hooks.Once('combat.NPCvsNPC', CombatLoader.#HookNPCvsNPC);
    }

    // ---- ajudantes ----

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

    static #Same(a, b) { return !!a && !!b && bl.addressOf(a) === bl.addressOf(b); }

    static #WithAttack(attack, run) {
        const outer = CombatLoader.#attack;
        CombatLoader.#attack = attack;
        CombatLoader.#Flags(attack);
        try { return run(); }
        finally {
            if (attack?.restoreFriendly) attack.target.friendly = attack.friendly;
            CombatLoader.#attack = outer;
            CombatLoader.#Flags(outer);
        }
    }

    static #Flags(attack) {
        if (!CombatLoader.#strikeAlways) bl.hookFlags.set('combat.Strike', !!attack || !!CombatLoader.#npcAttack);
        if (!CombatLoader.#canHitAlways) bl.hookFlags.set('combat.CanHitNPC', !!attack);
        if (CombatLoader.#collision) bl.hookFlags.set('combat.MeleeCollisionActive', attack?.collision === true);
    }

    // ---- item corpo a corpo no NPC ----

    static #HookItemAttack() {
        Terraria.Player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'].hook(
            (original, player, item, rect, damage, knockback, index) => {
                const target = Terraria.Main.npc[index];
                if (!target || PlayerLoader.Veto(player, 'CanHitNPC', target)) return;
                const itemCanHit = CombatLoader.Can('item', item, 'CanHitNPC', player, target);
                if (itemCanHit === false) return;
                const npcCanHit = CombatLoader.Can('npc', target, 'CanBeHitByItem', player, item);
                if (npcCanHit === false) return;
                const itemCollision = CombatLoader.Can('item', item, 'CanMeleeAttackCollideWithNPC', player, rect, target);
                const playerCollision = PlayerLoader.Nullable(player, 'CanMeleeAttackCollideWithNPC', item, rect, target);
                if (itemCollision === false || playerCollision === false) return;
                const collision = itemCollision === true || playerCollision === true ? true : null;
                return CombatLoader.#WithAttack({ player, item, target, rect, collision, npcCanHit, itemCanHit, checked: true },
                    () => original(player, item, rect, damage, knockback, index));
            }, ItemCombatHooks.Filter('player.ItemAttack'));
    }

    static #HookMeleeCollision() {
        CombatLoader.#collision = true;
        const gate = Terraria.Player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'];
        Microsoft.Xna.Framework.Rectangle['bool Intersects(Rectangle rect)'].hook((original, rect, value) => {
            const attack = CombatLoader.#attack;
            if (attack && attack.collision === true && CombatLoader.#SameRect(rect, attack.rect) && CombatLoader.#SameRect(value, attack.target.Hitbox)) return true;
            return original(rect, value);
        }, { whileIn: gate, flag: 'combat.MeleeCollisionActive' });
    }

    static #SameRect(a, b) {
        return a && b && a.X === b.X && a.Y === b.Y && a.Width === b.Width && a.Height === b.Height;
    }

    // ---- projétil no NPC ----

    static #HookProjectileAttack() {
        Terraria.Projectile['void Damage_PVE(ref Rectangle projRectangle, float projectileSpecificDamageMultiplier)'].hook((original, projectile, rect, multiplier) => {
            const player = CombatLoader.Owner(projectile);
            return CombatLoader.#WithAttack({ player, projectile }, () => original(projectile, rect, multiplier));
        });
    }

    // O jogo pergunta, para o jogador e os projéteis dele, se este NPC pode
    // ser acertado: os vetos e as forças de quem participa.
    static #HookCanHitNPC() {
        Terraria.Player['bool CanNPCBeHitByPlayerOrPlayerProjectile(NPC npc, Projectile projectile)'].hook((original, player, npc, projectile) => {
            const attack = CombatLoader.#attack;
            const checked = attack && attack.checked && CombatLoader.#Same(attack.player, player) && CombatLoader.#Same(attack.target, npc);
            if (!checked && PlayerLoader.Veto(player, 'CanHitNPC', npc)) return false;
            const decisions = [];
            if (projectile) {
                decisions.push(CombatLoader.Can('proj', projectile, 'CanHitNPC', npc));
                if (decisions[0] === false) return false;
                decisions.push(CombatLoader.Can('npc', npc, 'CanBeHitByProjectile', projectile));
                decisions.push(PlayerLoader.Nullable(player, 'CanHitNPCWithProj', projectile, npc));
            } else {
                if (attack && attack.item) decisions.push(PlayerLoader.Nullable(player, 'CanHitNPCWithItem', attack.item, npc));
                if (checked) decisions.push(attack.npcCanHit, attack.itemCanHit);
            }
            if (decisions.includes(false)) return false;
            if (decisions.includes(true)) {
                if (!projectile && checked && npc.friendly) {
                    attack.friendly = npc.friendly;
                    attack.restoreFriendly = true;
                    npc.friendly = false;
                }
                return true;
            }
            return original(player, npc, projectile);
        }, { flag: 'combat.CanHitNPC' });
    }

    static #HookDirectAttack() {
        Terraria.Player['void ApplyDamageToNPC(NPC npc, int damage, float knockback, int direction, bool crit, PlayerNPCHitSource hitSource)'].hook(
            (original, player, npc, damage, knockback, direction, crit, source) => {
                if (PlayerLoader.Veto(player, 'CanHitNPC', npc)) return;
                return CombatLoader.#WithAttack({ player, target: npc, checked: true }, () => original(player, npc, damage, knockback, direction, crit, source));
            });
        CombatLoader.#InstallStrike();
    }

    // ---- NPC no NPC ----

    static #HookNPCvsNPC() {
        const N = Terraria.NPC;
        N['void BeHurtByOtherNPC(int npcIndex, NPC thatNPC)'].hook((original, victim, index, attacker) => {
            if (!CombatLoader.All('npc', attacker, 'CanHitNPC', victim)) return;
            if (!CombatLoader.All('npc', victim, 'CanBeHitByNPC', attacker)) return;
            const outer = CombatLoader.#npcAttack;
            CombatLoader.#npcAttack = { attacker, victim };
            CombatLoader.#Flags(CombatLoader.#attack);
            try { return original(victim, index, attacker); }
            finally {
                CombatLoader.#npcAttack = outer;
                CombatLoader.#Flags(CombatLoader.#attack);
            }
        });
    }

    // ---- o golpe ----

    static #HookStrike() {
        Terraria.NPC['int StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool fromNet, int owner)'].hook(
            (original, npc, damage, knockback, direction, crit, fromNet, owner) => {
                if (fromNet) return original(npc, damage, knockback, direction, crit, fromNet, owner);
                const attack = CombatLoader.#attack;
                const own = attack && (!attack.target || CombatLoader.#Same(attack.target, npc)) ? attack : null;
                const npcAttack = CombatLoader.#npcAttack;
                const byNPC = npcAttack && CombatLoader.#Same(npcAttack.victim, npc) ? npcAttack : null;
                if (own?.restoreFriendly) npc.friendly = own.friendly;
                if (!own && !byNPC && !CombatLoader.#strikeAlways) return original(npc, damage, knockback, direction, crit, fromNet, owner);

                // Golpe dentro do golpe (o da dríade, uma explosão): sem quem bateu.
                return CombatLoader.#WithAttack(null, () => {
                    const outerNPC = CombatLoader.#npcAttack;
                    CombatLoader.#npcAttack = null;
                    try { return CombatLoader.#Strike(original, npc, damage, knockback, direction, crit, owner, own, byNPC); }
                    finally { CombatLoader.#npcAttack = outerNPC; }
                });
            }, { flag: 'combat.Strike' });
    }

    static #Strike(original, npc, damage, knockback, direction, crit, owner, attack, byNPC) {
        const modifiers = new HitModifiers(damage, knockback, direction, crit, npc.defense);
        const player = attack?.player, item = attack?.item, projectile = attack?.projectile;
        if (item) {
            CombatLoader.Call('item', item, 'ModifyHitNPC', player, npc, modifiers);
            CombatLoader.Call('npc', npc, 'ModifyHitByItem', player, item, modifiers);
        }
        if (projectile) {
            CombatLoader.Call('proj', projectile, 'ModifyHitNPC', npc, modifiers);
            CombatLoader.Call('npc', npc, 'ModifyHitByProjectile', projectile, modifiers);
        }
        if (player) {
            if (item) PlayerLoader.Call(player, 'ModifyHitNPCWithItem', item, npc, modifiers);
            if (projectile) PlayerLoader.Call(player, 'ModifyHitNPCWithProj', projectile, npc, modifiers);
            PlayerLoader.Call(player, 'ModifyHitNPC', npc, modifiers);
        }
        if (byNPC) CombatLoader.Call('npc', byNPC.attacker, 'ModifyHitNPC', npc, modifiers);
        CombatLoader.Call('npc', npc, 'ModifyIncomingHit', modifiers);

        const hit = modifiers.ToHitInfo(modifiers.damage, crit, modifiers.knockBack);
        const raw = CombatLoader.#RawDamage(npc, hit);
        const hide = hit.HideCombatText;
        if (hide) CombatLoader.#HideText(true);
        let done;
        try { done = original(npc, raw, hit.Knockback, hit.HitDirection, hit.Crit && !hit.InstantKill, false, owner); }
        finally { if (hide) CombatLoader.#HideText(false); }
        if (!(done > 0)) return done;

        hit.Damage = done;
        if (item) {
            CombatLoader.Call('item', item, 'OnHitNPC', player, npc, hit, done);
            CombatLoader.Call('npc', npc, 'OnHitByItem', player, item, hit, done);
        }
        if (projectile) {
            CombatLoader.Call('proj', projectile, 'OnHitNPC', npc, hit, done);
            CombatLoader.Call('npc', npc, 'OnHitByProjectile', projectile, hit, done);
        }
        if (player) {
            if (item) PlayerLoader.Call(player, 'OnHitNPCWithItem', item, npc, hit, done);
            if (projectile) PlayerLoader.Call(player, 'OnHitNPCWithProj', projectile, npc, hit, done);
            PlayerLoader.Call(player, 'OnHitNPC', npc, hit, done);
        }
        if (byNPC) CombatLoader.Call('npc', byNPC.attacker, 'OnHitNPC', npc, hit);
        return done;
    }

    // O dano bruto que, pela conta do jogo (defesa pela metade, crítico
    // dobrado), dá o dano final do HitModifiers.
    static #RawDamage(npc, hit) {
        if (hit.InstantKill) return Math.max(1, npc.life) + Math.ceil(npc.defense / 2) + 1;
        const factor = hit.Crit ? 2 : 1;
        return Math.max(1, Math.ceil(hit.Damage / factor + npc.defense / 2 - 1e-9));
    }

    static #HideText(on) {
        if (!CombatLoader.#hideText) {
            CombatLoader.#hideText = true;
            Terraria.CombatText['int NewText(Rectangle location, Color color, int amount, bool dramatic, bool dot)'].hook(
                () => -1, { flag: 'combat.HideText' });
        }
        bl.hookFlags.set('combat.HideText', on);
    }

    // ---- golpe no jogador: NPC, projétil e PvP ----

    static #HookHurt() {
        const each = PlayerLoader.Each;
        const any = PlayerLoader.Any;
        const Main = Terraria.Main;

        Terraria.Player['double Hurt(PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet, bool Crit, int cooldownCounter, bool dodgeable)'].hook(
            (original, self, src, damage, dir, pvp, quiet, crit, cooldown, dodgeable) => {
                if (any(self, 'ImmuneTo', true, (m) => m.ImmuneTo(self, src, cooldown, dodgeable))) return 0;
                const cause = CombatLoader.Cause(src);
                const npc = cause.npc, projectile = cause.projectile;
                const slot = new Ref(cooldown);
                if (npc && !CombatLoader.All('npc', npc, 'CanHitPlayer', self, slot)) return 0;
                if (npc && PlayerLoader.Veto(self, 'CanBeHitByNPC', npc, slot)) return 0;
                if (projectile) {
                    const owner = pvp ? CombatLoader.Owner(projectile) : null;
                    if (pvp && !CombatLoader.All('proj', projectile, 'CanHitPvp', self)) return 0;
                    if (pvp && owner && PlayerLoader.Veto(owner, 'CanHitPvpWithProj', projectile, self)) return 0;
                    if (!pvp && !CombatLoader.All('proj', projectile, 'CanHitPlayer', self)) return 0;
                    if (PlayerLoader.Veto(self, 'CanBeHitByProjectile', projectile)) return 0;
                }
                const attack = pvp ? CombatLoader.PvpAttack : null;
                if (attack && (!CombatLoader.All('item', attack.item, 'CanHitPvp', attack.player, self)
                    || PlayerLoader.Veto(attack.player, 'CanHitPvp', attack.item, self))) return 0;

                const modifiers = new HurtModifiers(src, damage, dir, pvp, slot.value, dodgeable, quiet, crit);
                if (attack) CombatLoader.Call('item', attack.item, 'ModifyHitPvp', attack.player, self, modifiers);
                if (npc) {
                    CombatLoader.Call('npc', npc, 'ModifyHitPlayer', self, modifiers);
                    PlayerLoader.Call(self, 'ModifyHitByNPC', npc, modifiers);
                }
                if (projectile) {
                    CombatLoader.Call('proj', projectile, 'ModifyHitPlayer', self, modifiers);
                    PlayerLoader.Call(self, 'ModifyHitByProjectile', projectile, modifiers);
                }
                each(self, 'ModifyHurt', (m) => m.ModifyHurt(self, modifiers));
                if (modifiers.Cancelled) return 0;

                const pending = modifiers.ToHurtInfo(1);
                if (modifiers.Dodgeable && self.whoAmI === Main.myPlayer && PlayerLoader.First(self, 'FreeDodge',
                    src, modifiers.SourceValue(), modifiers.hitDirection, pvp, modifiers.quiet, modifiers.crit, slot.value, modifiers.Dodgeable)) return 0;
                if (modifiers.Dodgeable && self.whoAmI === Main.myPlayer && PlayerLoader.First(self, 'ConsumableDodge', pending)) return 0;

                const raw = CombatLoader.#HurtRaw(self, modifiers, pvp);
                const done = original(self, src, raw, modifiers.hitDirection, pvp, modifiers.quiet, modifiers.crit, slot.value, modifiers.Dodgeable);
                if (done <= 0) return done;

                const info = modifiers.ToHurtInfo(done);
                if (attack) CombatLoader.Call('item', attack.item, 'OnHitPvp', attack.player, self, info);
                if (npc) {
                    CombatLoader.Call('npc', npc, 'OnHitPlayer', self, info);
                    PlayerLoader.Call(self, 'OnHitByNPC', npc, info);
                }
                if (projectile) {
                    CombatLoader.Call('proj', projectile, 'OnHitPlayer', self, info);
                    PlayerLoader.Call(self, 'OnHitByProjectile', projectile, info);
                }

                const args = [src, done, modifiers.hitDirection, pvp, modifiers.quiet, modifiers.crit, cooldown, modifiers.Dodgeable];
                each(self, 'OnHurt', (m) => m.OnHurt(self, ...args));
                if (!self.dead && self.statLife > 0) each(self, 'PostHurt', (m) => m.PostHurt(self, ...args));
                return done;
            }, { flag: 'combat.Hurt' });
    }

    // O dano bruto do Hurt do jogo. Só a penetração, o final e o teto mexem
    // depois da defesa: sem eles, o de origem vai direto.
    static #HurtRaw(player, modifiers, pvp) {
        const raw = modifiers.SourceValue();
        const final = modifiers.FinalDamage;
        const plain = modifiers.ArmorPenetration === 0 && modifiers.ScalingArmorPenetration === 0 &&
            modifiers.MaxDamage === Infinity && final.Equals(StatModifier.Default);
        if (plain) return raw;
        const Main = Terraria.Main;
        const take = pvp ? Main['double CalculateDamagePlayersTakeInPVP(int Damage, int Defense)'] : Main['double CalculateDamagePlayersTake(int Damage, int Defense)'];
        const defense = player.statDefense | 0;
        const reduced = take(raw, Math.round(modifiers.EffectiveDefense(defense)));
        const target = modifiers.Final(reduced);
        // Quanto a defesa toma por ponto, medido longe do mínimo de 1.
        const perPoint = defense > 0 ? (100000 - take(100000, defense)) / defense : 0;
        return Math.max(1, Math.ceil(target + defense * perPoint - 1e-9));
    }
}
