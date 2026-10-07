class ItemUseHooks {
    static #ammo = null;

    static Install(type, plan) {
        const mark = (key, names) => {
            if (!names.some(name => plan[name])) return false;
            ItemCombatHooks.Mark(key, type);
            return true;
        };
        if (mark('player.ItemAnimation', ['UseAnimation'])) PlayerItemHooks.InstallAnimation();
        if (mark('player.ItemFrame', ['UseItemFrame', 'HoldItemFrame'])) Hooks.Once('player.ItemFrame', ItemUseHooks.#Frames);
        if (plan.CanBeChosenAsAmmo) ItemCombatHooks.All('player.AmmoSelection');
        if (plan.CanBeConsumedAsAmmo || plan.OnConsumedAsAmmo || plan.PickAmmo) ItemCombatHooks.All('player.Ammo');
        if (mark('player.AmmoSelection', ['CanChooseAmmo', 'CanBeChosenAsAmmo'])) ItemUseHooks.InstallSelection();
        if (mark('player.AmmoAvailability', ['NeedsAmmo', 'CanChooseAmmo', 'CanBeChosenAsAmmo'])) ItemUseHooks.InstallAvailability();
        if (plan.CanBeChosenAsAmmo) ItemCombatHooks.All('player.AmmoAvailability');
        if (mark('player.Ammo', ['NeedsAmmo', 'CanChooseAmmo', 'CanBeChosenAsAmmo', 'CanConsumeAmmo',
            'CanBeConsumedAsAmmo', 'OnConsumeAmmo', 'OnConsumedAsAmmo', 'PickAmmo'])) ItemUseHooks.InstallAmmo();
    }

    static InstallSelection() { Hooks.Once('player.AmmoSelection', ItemUseHooks.#Selection); }
    static InstallAvailability() { Hooks.Once('player.AmmoAvailability', ItemUseHooks.#Availability); }
    static InstallAmmo() { Hooks.Once('player.Ammo', ItemUseHooks.#Ammo); }

    static #Frames() {
        Terraria.Player['void PlayerFrame()'].hook((original, player) => {
            original(player);
            const item = player.inventory[player.selectedItem];
            if (!item || item.type <= 0 || item.stack <= 0) return;
            if (player.itemAnimation > 0) ItemCombatHooks.Call(item, 'UseItemFrame', player);
            else if (player['bool CanVisuallyHoldItem(Item item)'](item)) ItemCombatHooks.Call(item, 'HoldItemFrame', player);
        }, { minType: 0, field: 'inventory[selectedItemState.selected].type', marks: 'player.ItemFrame' });
    }

    static #Eligible(player, weapon, ammo) {
        if (!ammo || ammo.type <= 0 || ammo.stack <= 0) return false;
        const choice = ItemCombatHooks.Call(weapon, 'CanChooseAmmo', ammo, player);
        if (choice === false) return false;
        const accepted = ItemCombatHooks.Call(ammo, 'CanBeChosenAsAmmo', weapon, player);
        return accepted !== false && (choice === true || accepted === true || ammo.ammo === weapon.useAmmo);
    }

    static #Selection() {
        Terraria.Player['Item PickAmmo_IterateRange(Item sItem, Item[] items, int[] slotIterationOrder, bool allowAmmoCycling)'].hook(
            (original, player, weapon, items, order, allowCycling) => {
                const cycling = allowCycling && player.ammoCyclingMode !== 0 && weapon.type !== 5134 && weapon.type !== 779;
                const eligible = cycling ? [] : null;
                for (let i = 0; i < order.length; i++) {
                    const ammo = items[order[i]];
                    if (!ItemUseHooks.#Eligible(player, weapon, ammo)) continue;
                    if (!cycling) return ammo;
                    eligible.push(ammo);
                }
                return eligible && eligible.length ? eligible[player.ammoCyclingOffset % eligible.length] || null : null;
            }, ItemCombatHooks.Filter('player.AmmoSelection'));
    }

    static #Availability() {
        Terraria.Player['bool HasAmmo(Item sItem, bool canUse)'].hook((original, player, weapon, canUse) => {
            if (!canUse) return false;
            if (weapon.useAmmo <= 0) return true;
            return !!player['Item PickAmmo_PickAmmoItem(Item sItem)'](weapon)
                || ItemCombatHooks.Call(weapon, 'NeedsAmmo', player) === false;
        }, ItemCombatHooks.Filter('player.AmmoAvailability'));
    }

    static #DefaultAmmo(weapon) {
        if (weapon.useAmmo <= 0) return null;
        const item = Terraria.Item.new();
        item['void .ctor()']();
        item['void SetDefaults(int Type, ItemVariant variant)'](weapon.useAmmo, null);
        if (item.ammo !== weapon.useAmmo) return null;
        item.consumable = false;
        return item;
    }

    static #Finish(scope, projectile, speed, canShoot, damage, knockback) {
        if (!scope.item || !canShoot.value) return;
        const base = damage.value, previousType = projectile.value, previousSpeed = speed.value, previousKnockback = knockback.value;
        if (ItemCombatHooks.Has(scope.item, 'PickAmmo')) {
            const modifier = StatModifier.ForValue(base);
            ItemCombatHooks.Call(scope.item, 'PickAmmo', scope.weapon, scope.player, projectile, speed, modifier, knockback);
            const value = modifier.ApplyTo(base);
            damage.value = Number.isFinite(value) ? Math.max(0, Math.trunc(value)) : base;
            projectile.value = Number.isFinite(projectile.value) ? Math.max(0, Math.trunc(projectile.value)) : previousType;
            speed.value = Number.isFinite(speed.value) ? Math.max(0, speed.value) : previousSpeed;
            knockback.value = Number.isFinite(knockback.value) ? Math.max(0, knockback.value) : previousKnockback;
        }
        if (!scope.dontConsume && !scope.blocked && scope.item.stack < scope.stack) {
            ItemCombatHooks.Call(scope.weapon, 'OnConsumeAmmo', scope.item, scope.player);
            ItemCombatHooks.Call(scope.item, 'OnConsumedAsAmmo', scope.weapon, scope.player);
            PlayerLoader.Call(scope.player, 'OnConsumeAmmo', scope.weapon, scope.item);
        }
    }

    static #Ammo() {
        const gate = Terraria.Player['void PickAmmo(Item sItem, ref int projToShoot, ref float speed, ref bool canShoot, ref int Damage, ref float KnockBack, out int usedAmmoItemId, bool dontConsume)'];
        gate.hook((original, player, weapon, projectile, speed, canShoot, damage, knockback, usedAmmo, dontConsume) => {
            const outer = ItemUseHooks.#ammo;
            const scope = { player, weapon, dontConsume, item: null, clear: false };
            ItemUseHooks.#ammo = scope;
            try {
                original(player, weapon, projectile, speed, canShoot, damage, knockback, usedAmmo, dontConsume);
                ItemUseHooks.#Finish(scope, projectile, speed, canShoot, damage, knockback);
            } finally {
                if (scope.item && scope.blocked) scope.item.consumable = scope.consumable;
                ItemUseHooks.#ammo = null;
                try { if (scope.clear) scope.item['void TurnToAir()'](); }
                finally { ItemUseHooks.#ammo = outer; }
            }
        }, ItemCombatHooks.Filter('player.Ammo'));
        Terraria.Player['Item PickAmmo_PickAmmoItem(Item sItem)'].hook((original, player, weapon) => {
            let item = original(player, weapon);
            const scope = ItemUseHooks.#ammo;
            if (!scope || bl.addressOf(scope.player) !== bl.addressOf(player) || bl.addressOf(scope.weapon) !== bl.addressOf(weapon)) return item;
            if (!item && ItemCombatHooks.Call(weapon, 'NeedsAmmo', player) === false) item = ItemUseHooks.#DefaultAmmo(weapon);
            if (!item) return item;
            scope.item = item;
            scope.stack = item.stack;
            scope.consumable = item.consumable;
            if (!scope.dontConsume && item.consumable) {
                scope.blocked = ItemCombatHooks.Call(weapon, 'CanConsumeAmmo', item, player) === false
                    || ItemCombatHooks.Call(item, 'CanBeConsumedAsAmmo', weapon, player) === false
                    || PlayerLoader.Veto(player, 'CanConsumeAmmo', weapon, item);
                if (scope.blocked) item.consumable = false;
            }
            return item;
        }, { whileIn: gate });
        Terraria.Item['void TurnToAir()'].hook((original, item) => {
            const scope = ItemUseHooks.#ammo;
            if (scope && scope.item && !scope.dontConsume && !scope.blocked && item.stack < scope.stack
                && bl.addressOf(scope.item) === bl.addressOf(item)) { scope.clear = true; return; }
            return original(item);
        }, { whileIn: gate });
    }
}
