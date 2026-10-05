class PlayerItemHooks {
    static Stats = null;
    static #shot = null;
    static #ammo = null;
    static #quickHeal = false;
    static #timing = new Set();
    static #mana = new Set();
    static #potion = null;

    static Install(cls) {
        const want = PlayerLoader.Wants;
        want(cls, ['PreItemCheck', 'PostItemCheck'], 'player.ItemCheck', () => {
            Terraria.Player['void ItemCheck()'].hook((original, player) => {
                if (!PlayerLoader.Veto(player, 'PreItemCheck')) original(player);
                PlayerLoader.Call(player, 'PostItemCheck');
            });
        });
        want(cls, ['CanShoot', 'ModifyShootStats', 'Shoot'], 'player.Shoot', PlayerItemHooks.#Shoot);
        want(cls, ['CanConsumeAmmo', 'OnConsumeAmmo'], 'player.Ammo', PlayerItemHooks.#Ammo);
        want(cls, ['ModifyWeaponCrit'], 'player.WeaponCrit', PlayerItemHooks.#Crit);
        want(cls, ['ModifyWeaponKnockback'], 'player.WeaponKnockback', PlayerItemHooks.#Knockback);
        want(cls, ['ModifyItemScale'], 'player.ItemScale', () => {
            Terraria.Player['void ApplyMeleeScale(ref float scale)'].hook((original, player, scale) => {
                original(player, scale);
                PlayerLoader.Call(player, 'ModifyItemScale', player.inventory[player.selectedItem], scale);
            });
        });
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
        Hooks.Once('player.WeaponCrit', PlayerItemHooks.#Crit);
        Hooks.Once('player.WeaponKnockback', PlayerItemHooks.#Knockback);
        Hooks.Once('player.ItemTiming', PlayerItemHooks.#Timing);
    }

    static #Crit() {
            Terraria.Player['int GetWeaponCrit(Item sItem)'].hook((original, player, item) => {
                const value = original(player, item);
                const crit = new Ref(PlayerItemHooks.Stats ? PlayerItemHooks.Stats.crit(player, item, value) : value);
                PlayerLoader.Call(player, 'ModifyWeaponCrit', item, crit);
                return Number.isFinite(crit.value) ? Math.trunc(crit.value) : 0;
            });
    }

    static #Knockback() {
            Terraria.Player['float GetWeaponKnockback(Item sItem, float KnockBack)'].hook((original, player, item, baseKnockback) => {
                const vanilla = original(player, item, baseKnockback);
                const value = PlayerItemHooks.Stats ? PlayerItemHooks.Stats.knockback(player, item, vanilla) : vanilla;
                const modifier = StatModifier.ForValue(value);
                PlayerLoader.Call(player, 'ModifyWeaponKnockback', item, modifier);
                return Math.max(0, modifier.ApplyTo(value));
            });
    }

    static #Shoot() {
        Terraria.Player['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'].hook((original, player, index, item, damage, feedback) => {
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
            });
    }

    static #Ammo() {
        Terraria.Player['void PickAmmo(Item sItem, ref int projToShoot, ref float speed, ref bool canShoot, ref int Damage, ref float KnockBack, out int usedAmmoItemId, bool dontConsume)'].hook(
            (original, player, weapon, projectile, speed, canShoot, damage, knockback, usedAmmo, dontConsume) => {
                const outer = PlayerItemHooks.#ammo;
                const scope = { player, weapon, dontConsume, item: null, stack: 0, consumable: false };
                PlayerItemHooks.#ammo = scope;
                try { return original(player, weapon, projectile, speed, canShoot, damage, knockback, usedAmmo, dontConsume); }
                finally {
                    PlayerItemHooks.#ammo = outer;
                    if (scope.item) {
                        if (scope.blocked) scope.item.consumable = scope.consumable;
                        if (!scope.notified && !scope.dontConsume && scope.item.stack < scope.stack) PlayerLoader.Call(player, 'OnConsumeAmmo', weapon, scope.item);
                    }
                }
            });
        Terraria.Player['Item PickAmmo_PickAmmoItem(Item sItem)'].hook((original, player, weapon) => {
            const item = original(player, weapon), scope = PlayerItemHooks.#ammo;
            if (scope && item && scope.player.whoAmI === player.whoAmI && !scope.dontConsume) {
                scope.item = item;
                scope.stack = item.stack;
                scope.consumable = item.consumable;
                scope.blocked = PlayerLoader.Veto(player, 'CanConsumeAmmo', weapon, item);
                if (scope.blocked) item.consumable = false;
            }
            return item;
        });
        Terraria.Item['void TurnToAir()'].hook((original, item) => {
            const scope = PlayerItemHooks.#ammo;
            if (scope && scope.item && !scope.blocked && !scope.notified && !scope.dontConsume
                && bl.addressOf(scope.item) === bl.addressOf(item) && item.stack < scope.stack) {
                scope.notified = true;
                PlayerLoader.Call(scope.player, 'OnConsumeAmmo', scope.weapon, item);
            }
            return original(item);
        });
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
        P['void ApplyItemAnimation(Item sItem)'].hook((original, player, item) => {
            original(player, item);
            if (PlayerItemHooks.Stats) PlayerItemHooks.Stats.animation(player, item);
            const factor = PlayerLoader.Factor(player, 'UseAnimationMultiplier', item) / PlayerLoader.Factor(player, 'UseSpeedMultiplier', item);
            const frames = Math.max(1, Math.trunc(player.itemAnimation * factor));
            player.itemAnimation = frames;
            player.itemAnimationMax = frames;
        });
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
                if (player.statMana < needed) PlayerLoader.Call(player, 'OnMissingMana', item, needed);
                const before = player.statMana;
                const result = original(player, amount, pay, blockQuickMana);
                if (pay && needed > 0) {
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
