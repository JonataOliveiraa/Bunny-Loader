class PlayerItemHooks {
    static Stats = null;
    static #shot = null;
    static #animationTiming = false;
    static #quickHeal = false;
    static #timing = new Set();
    static #mana = new Set();
    static #potion = null;
    static #scaleAll = false;

    static Install(cls) {
        const want = PlayerLoader.Wants;
        want(cls, ['PreItemCheck', 'PostItemCheck'], 'player.ItemCheck', () => {
            Terraria.Player['void ItemCheck()'].hook((original, player) => {
                if (!PlayerLoader.Veto(player, 'PreItemCheck')) original(player);
                PlayerLoader.Call(player, 'PostItemCheck');
            });
        });
        want(cls, ['CanShoot', 'ModifyShootStats', 'Shoot'], 'player.Shoot', PlayerItemHooks.#Shoot);
        if (['CanConsumeAmmo', 'OnConsumeAmmo'].some(name => Hooks.Overrides(cls, ModPlayer, name))) {
            ItemCombatHooks.All('player.Ammo');
            ItemUseHooks.InstallAmmo();
        }
        if (Hooks.Overrides(cls, ModPlayer, 'ModifyWeaponCrit')) {
            ItemCombatHooks.All('player.WeaponCrit');
            PlayerItemHooks.InstallCrit();
        }
        if (Hooks.Overrides(cls, ModPlayer, 'ModifyWeaponKnockback')) {
            ItemCombatHooks.All('player.WeaponKnockback');
            PlayerItemHooks.InstallKnockback();
        }
        if (Hooks.Overrides(cls, ModPlayer, 'ModifyItemScale')) {
            PlayerItemHooks.#scaleAll = true;
            ItemCombatHooks.All('player.ItemScale');
            ItemCombatHooks.All('player.ItemHitbox');
            PlayerItemHooks.InstallScale();
            PlayerItemHooks.InstallHitbox();
        }
        want(cls, ['CanAutoReuseItem'], 'player.AutoReuse', () => {
            Terraria.Player['void ItemCheck_AutoReuseLogic(Item sItem)'].hook((original, player, item) => {
                const decision = PlayerLoader.Nullable(player, 'CanAutoReuseItem', item);
                if (decision === null) return original(player, item);
                const previous = item.autoReuse;
                const all = player.autoReuseAllWeapons, glove = player.autoReuseGlove;
                item.autoReuse = decision;
                if (!decision) { player.autoReuseAllWeapons = false; player.autoReuseGlove = false; }
                try { return original(player, item); }
                finally { item.autoReuse = previous; player.autoReuseAllWeapons = all; player.autoReuseGlove = glove; }
            });
        });
        want(cls, ['UseSpeedMultiplier', 'UseTimeMultiplier', 'UseAnimationMultiplier'], 'player.ItemTiming', PlayerItemHooks.#Timing);
        want(cls, ['ModifyManaCost', 'OnConsumeMana', 'OnMissingMana'], 'player.ItemMana', PlayerItemHooks.#Mana);
        want(cls, ['GetHealLife', 'GetHealMana'], 'player.ItemHealing', PlayerItemHooks.#Healing);
        want(cls, ['ApplyPotionDelay'], 'player.PotionDelay', PlayerItemHooks.#PotionDelay);
    }

    static InstallStats() {
        ItemCombatHooks.All('player.WeaponCrit');
        ItemCombatHooks.All('player.WeaponKnockback');
        PlayerItemHooks.InstallCrit();
        PlayerItemHooks.InstallKnockback();
        Hooks.Once('player.ItemTiming', PlayerItemHooks.#Timing);
    }

    static InstallDamage() { Hooks.Once('player.WeaponDamage', PlayerItemHooks.#Damage); }
    static InstallCrit() { Hooks.Once('player.WeaponCrit', PlayerItemHooks.#Crit); }
    static InstallKnockback() { Hooks.Once('player.WeaponKnockback', PlayerItemHooks.#Knockback); }
    static InstallScale() { Hooks.Once('player.ItemScale', PlayerItemHooks.#Scale); }
    static InstallHitbox() { Hooks.Once('player.ItemHitbox', PlayerItemHooks.#Hitbox); }
    static InstallAnimation() { Hooks.Once('player.ItemAnimation', PlayerItemHooks.#Animation); }

    static #Animation() {
        Terraria.Player['void ApplyItemAnimation(Item sItem)'].hook((original, player, item) => {
            ItemCombatHooks.Call(item, 'UseAnimation', player);
            original(player, item);
            if (!PlayerItemHooks.#animationTiming) return;
            if (PlayerItemHooks.Stats) PlayerItemHooks.Stats.animation(player, item);
            const factor = PlayerLoader.Factor(player, 'UseAnimationMultiplier', item) / PlayerLoader.Factor(player, 'UseSpeedMultiplier', item);
            const frames = Math.max(1, Math.trunc(player.itemAnimation * factor));
            player.itemAnimation = frames;
            player.itemAnimationMax = frames;
        }, ItemCombatHooks.Filter('player.ItemAnimation'));
    }

    static #Damage() {
        Terraria.Player['int GetWeaponDamage(Item sItem)'].hook((original, player, item) => {
            let damage = original(player, item);
            if (ItemCombatHooks.Has(item, 'ModifyWeaponDamage')) {
                const modifier = StatModifier.ForValue(damage);
                damage = StatModifier.Resolve(ItemCombatHooks.Call(item, 'ModifyWeaponDamage', player, modifier), modifier, damage);
            }
            globalItems.Each(item, 'ModifyWeaponDamage', g => {
                const next = StatModifier.ForValue(damage);
                damage = StatModifier.Resolve(g.ModifyWeaponDamage(item, player, next), next, damage);
            });
            PlayerLoader.Each(player, 'ModifyWeaponDamage', m => {
                m.WeaponDamage = damage;
                const next = StatModifier.ForValue(damage);
                const result = m.ModifyWeaponDamage(player, item, next);
                if (typeof result === 'number') damage = result;
                else if (m.WeaponDamage !== damage && typeof m.WeaponDamage === 'number') damage = m.WeaponDamage;
                else damage = next.ApplyTo(damage);
            });
            return Number.isFinite(damage) ? Math.max(0, Math.floor(damage)) : 0;
        }, ItemCombatHooks.Filter('player.WeaponDamage'));
    }

    static #Crit() {
        Terraria.Player['int GetWeaponCrit(Item sItem)'].hook((original, player, item) => {
            const value = original(player, item);
            const crit = new Ref(PlayerItemHooks.Stats ? PlayerItemHooks.Stats.crit(player, item, value) : value);
            ItemCombatHooks.Call(item, 'ModifyWeaponCrit', player, crit);
            PlayerLoader.Call(player, 'ModifyWeaponCrit', item, crit);
            return Number.isFinite(crit.value) ? Math.trunc(crit.value) : 0;
        }, ItemCombatHooks.Filter('player.WeaponCrit'));
    }

    static #Knockback() {
        Terraria.Player['float GetWeaponKnockback(Item sItem, float KnockBack)'].hook((original, player, item, baseKnockback) => {
            const vanilla = original(player, item, baseKnockback);
            const value = PlayerItemHooks.Stats ? PlayerItemHooks.Stats.knockback(player, item, vanilla) : vanilla;
            const modifier = StatModifier.ForValue(value);
            ItemCombatHooks.Call(item, 'ModifyWeaponKnockback', player, modifier);
            PlayerLoader.Call(player, 'ModifyWeaponKnockback', item, modifier);
            const result = modifier.ApplyTo(value);
            return Number.isFinite(result) ? Math.max(0, result) : 0;
        }, ItemCombatHooks.Filter('player.WeaponKnockback'));
    }

    static #ScaleFactor(player, item, value) {
        const scale = new Ref(value);
        ItemCombatHooks.Call(item, 'ModifyItemScale', player, scale);
        PlayerLoader.Call(player, 'ModifyItemScale', item, scale);
        return Number.isFinite(scale.value) ? Math.max(0, scale.value) : value;
    }

    static #VanillaScale(player, item) {
        const scale = new Ref(1);
        if (item.melee) player['void ApplyMeleeScale(ref float scale)'](scale);
        return scale.value;
    }

    static #Scale() {
        Terraria.Player['float GetAdjustedItemScale(Item item)'].hook((original, player, item) => {
            const value = original(player, item);
            const factor = item.scale !== 0 ? value / item.scale : PlayerItemHooks.#VanillaScale(player, item);
            return item.scale * PlayerItemHooks.#ScaleFactor(player, item, factor);
        }, ItemCombatHooks.Filter('player.ItemScale'));
    }

    static #Hitbox() {
        Terraria.Player['void ItemCheck_GetMeleeHitbox(Item sItem, Rectangle heldItemFrame, out bool dontAttack, out Rectangle itemRectangle)'].hook(
            (original, player, item, frame, dontAttack, hitbox) => {
                if (PlayerItemHooks.#scaleAll || ItemCombatHooks.Has(item, 'ModifyItemScale')) {
                    const previous = item.scale;
                    const vanilla = PlayerItemHooks.#VanillaScale(player, item);
                    const factor = PlayerItemHooks.#ScaleFactor(player, item, vanilla);
                    item.scale = previous * factor / vanilla;
                    try { original(player, item, frame, dontAttack, hitbox); }
                    finally { item.scale = previous; }
                } else original(player, item, frame, dontAttack, hitbox);
                ItemCombatHooks.Call(item, 'UseItemHitbox', player, hitbox, dontAttack);
            }, ItemCombatHooks.Filter('player.ItemHitbox'));
    }

    static #Shoot() {
        const gate = Terraria.Player['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'];
        gate.hook((original, player, index, item, damage, feedback) => {
            if (PlayerLoader.Veto(player, 'CanShoot', item)) {
                player['void ApplyItemTime(Item sItem)'](item);
                return;
            }
            const outer = PlayerItemHooks.#shot;
            PlayerItemHooks.#shot = { player, item };
            try { return original(player, index, item, damage, feedback); }
            finally { PlayerItemHooks.#shot = outer; }
        });
        Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'].hook(
            (original, source, x, y, sx, sy, type, damage, knockback, owner, ai0, ai1, ai2, modifier) => {
                const shot = PlayerItemHooks.#shot;
                if (!shot || shot.player.whoAmI !== owner) return original(source, x, y, sx, sy, type, damage, knockback, owner, ai0, ai1, ai2, modifier);
                PlayerItemHooks.#shot = null;
                try {
                    const position = new Ref(Vector2.new(x, y)), velocity = new Ref(Vector2.new(sx, sy));
                    const shotType = new Ref(type), shotDamage = new Ref(damage), shotKnockback = new Ref(knockback);
                    PlayerLoader.Call(shot.player, 'ModifyShootStats', shot.item, position, velocity, shotType, shotDamage, shotKnockback);
                    if (PlayerLoader.Veto(shot.player, 'Shoot', shot.item, source, position.value, velocity.value, shotType.value, shotDamage.value, shotKnockback.value)) return 1000;
                    return original(source, position.value.X, position.value.Y, velocity.value.X, velocity.value.Y,
                        shotType.value, shotDamage.value, shotKnockback.value, owner, ai0, ai1, ai2, modifier);
                } finally { PlayerItemHooks.#shot = shot; }
            }, { whileIn: gate });
    }

    static #Timing() {
        const P = Terraria.Player;
        const time = (original, player, item, multiplier) => {
            const key = bl.addressOf(player);
            if (PlayerItemHooks.#timing.has(key)) return multiplier === undefined ? original(player, item) : original(player, item, multiplier);
            PlayerItemHooks.#timing.add(key);
            try {
                if (multiplier === undefined) original(player, item);
                else original(player, item, multiplier);
                if (PlayerItemHooks.Stats && multiplier === undefined) PlayerItemHooks.Stats.time(player, item);
                const factor = PlayerLoader.Factor(player, 'UseTimeMultiplier', item) / PlayerLoader.Factor(player, 'UseSpeedMultiplier', item);
                const frames = Math.max(1, Math.trunc(player.itemTime * factor));
                player.itemTime = frames;
                player.itemTimeMax = frames;
            } finally { PlayerItemHooks.#timing.delete(key); }
        };
        P['void ApplyItemTime(Item sItem)'].hook(time);
        P['void ApplyItemTime(Item sItem, float multiplier)'].hook(time);
        PlayerItemHooks.#animationTiming = true;
        ItemCombatHooks.All('player.ItemAnimation');
        PlayerItemHooks.InstallAnimation();
    }

    static #Mana() {
        const P = Terraria.Player;
        const cost = (player, item) => {
            const reduce = new Ref(0), mult = new Ref(1);
            PlayerLoader.Call(player, 'ModifyManaCost', item, reduce, mult);
            const value = Math.max(0, player.manaCost * (1 - reduce.value) * mult.value);
            return Number.isFinite(value) ? value : player.manaCost;
        };
        P['bool CheckMana(int amount, bool pay, bool blockQuickMana)'].hook((original, player, amount, pay, blockQuickMana) => {
            const key = bl.addressOf(player);
            if (PlayerItemHooks.#mana.has(key)) return original(player, amount, pay, blockQuickMana);
            const item = player.inventory[player.selectedItem], previous = player.manaCost;
            PlayerItemHooks.#mana.add(key);
            try {
                player.manaCost = cost(player, item);
                const needed = Math.max(0, Math.trunc(amount * player.manaCost));
                if (player.statMana < needed && !blockQuickMana) PlayerLoader.Call(player, 'OnMissingMana', item, needed);
                const before = player.statMana;
                const result = original(player, amount, pay, blockQuickMana);
                if (result && pay && needed > 0) {
                    const consumed = player.slowMagicUse ? Math.max(0, before - player.statMana) : needed;
                    if (consumed > 0) PlayerLoader.Call(player, 'OnConsumeMana', item, consumed);
                }
                return result;
            } finally { player.manaCost = previous; PlayerItemHooks.#mana.delete(key); }
        });
        P['bool CheckManaPredictWithoutUse(int amountBeforeManaCost, bool allowQuickMana)'].hook((original, player, amount, allowQuickMana) => {
            const previous = player.manaCost;
            player.manaCost = cost(player, player.inventory[player.selectedItem]);
            try { return original(player, amount, allowQuickMana); }
            finally { player.manaCost = previous; }
        });
    }

    static #PotionDelay() {
        Terraria.Player['void ApplyPotionDelay(Item sItem)'].hook((original, player, item) => {
            const outer = PlayerItemHooks.#potion, scope = { player, item, previous: player.potionDelay, blocked: false };
            PlayerItemHooks.#potion = scope;
            try { return original(player, item); }
            finally {
                if (scope.blocked) player.potionDelay = scope.previous;
                PlayerItemHooks.#potion = outer;
            }
        });
        Terraria.Player['void AddBuff(int type, int time, bool fromNetPvP)'].hook((original, player, type, time, quiet) => {
            const scope = PlayerItemHooks.#potion;
            if (scope && bl.addressOf(scope.player) === bl.addressOf(player) && type === Terraria.ID.BuffID.PotionSickness) {
                scope.blocked = PlayerLoader.Veto(player, 'ApplyPotionDelay', scope.item, time);
                if (scope.blocked) return;
            }
            return original(player, type, time, quiet);
        });
    }

    static #Healing() {
        for (const signature of ['void QuickHeal()', 'void QuickMana()']) {
            Terraria.Player[signature].hook((original, player) => {
                const outer = PlayerItemHooks.#quickHeal;
                PlayerItemHooks.#quickHeal = true;
                try { return original(player); }
                finally { PlayerItemHooks.#quickHeal = outer; }
            });
        }
        Terraria.Player['void ApplyLifeAndOrMana(Item item)'].hook((original, player, item) => {
            const life = item.healLife, mana = item.healMana;
            const healLife = new Ref(life), healMana = new Ref(mana);
            PlayerLoader.Call(player, 'GetHealLife', item, PlayerItemHooks.#quickHeal, healLife);
            PlayerLoader.Call(player, 'GetHealMana', item, PlayerItemHooks.#quickHeal, healMana);
            item.healLife = Math.max(0, Math.trunc(healLife.value));
            item.healMana = Math.max(0, Math.trunc(healMana.value));
            try { return original(player, item); }
            finally { item.healLife = life; item.healMana = mana; }
        });
        Terraria.Player['void QuickHeal_GetItemToUse_TryChoosingItem(int lifeDifference, ref Item bestItem, ref int bestDifference, Item nextItem)'].hook(
            (original, difference, bestItem, bestDifference, item) => {
                const player = Terraria.Main.player[Terraria.Main.myPlayer], life = item.healLife, value = new Ref(life);
                PlayerLoader.Call(player, 'GetHealLife', item, true, value);
                item.healLife = Math.max(0, Math.trunc(value.value));
                try { return original(difference, bestItem, bestDifference, item); }
                finally { item.healLife = life; }
            });
    }
}
