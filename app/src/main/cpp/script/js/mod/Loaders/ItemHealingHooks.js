class ItemHealingHooks {
    static #quick = null;
    static #selection = null;
    static #potion = null;

    static Install(type, plan) {
        if (plan.GetHealLife || plan.GetHealMana) {
            bl.hookMarks.set('player.ItemHealing', type, true);
            ItemHealingHooks.InstallHealing();
        }
        if (plan.GetHealLife) bl.hookMarks.set('player.HealSelection', type, true);
        if (plan.ModifyPotionDelay || plan.ApplyPotionDelay) {
            bl.hookMarks.set('player.PotionDelay', type, true);
            ItemHealingHooks.InstallPotionDelay();
        }
    }

    static InstallPlayer(cls) {
        if (['GetHealLife', 'GetHealMana'].some(name => Hooks.Overrides(cls, ModPlayer, name))) {
            ItemCombatHooks.All('player.ItemHealing');
            if (Hooks.Overrides(cls, ModPlayer, 'GetHealLife')) ItemCombatHooks.All('player.HealSelection');
            ItemHealingHooks.InstallHealing();
        }
        if (Hooks.Overrides(cls, ModPlayer, 'ApplyPotionDelay')) {
            ItemCombatHooks.All('player.PotionDelay');
            ItemHealingHooks.InstallPotionDelay();
        }
    }

    static #Amount(value, fallback, current = 0) {
        const number = Number.isFinite(value) ? value : fallback;
        return Math.max(0, Math.min(2147483647 - Math.max(0, current), Math.trunc(number)));
    }

    static #Heal(player, item, name, base, quick, current) {
        if (base <= 0) return base;
        const value = new Ref(base);
        ItemCombatHooks.Call(item, name, player, quick, value);
        PlayerLoader.Call(player, name, item, quick, value);
        return ItemHealingHooks.#Amount(value.value, base, current);
    }

    static InstallHealing() { Hooks.Once('player.ItemHealing', ItemHealingHooks.#Healing); }
    static InstallPotionDelay() { Hooks.Once('player.PotionDelay', ItemHealingHooks.#PotionDelay); }

    static #Healing() {
        const P = Terraria.Player;
        for (const signature of ['void QuickHeal()', 'void QuickMana()']) {
            P[signature].hook((original, player) => {
                const outer = ItemHealingHooks.#quick;
                ItemHealingHooks.#quick = bl.addressOf(player);
                try { return original(player); }
                finally { ItemHealingHooks.#quick = outer; }
            });
        }
        P['void ApplyLifeAndOrMana(Item item)'].hook((original, player, item) => {
            const life = item.healLife, mana = item.healMana;
            const quick = ItemHealingHooks.#quick === bl.addressOf(player);
            const healLife = ItemHealingHooks.#Heal(player, item, 'GetHealLife', life, quick, player.statLife);
            const healMana = ItemHealingHooks.#Heal(player, item, 'GetHealMana', mana, quick, player.statMana);
            item.healLife = healLife;
            item.healMana = healMana;
            try { return original(player, item); }
            finally { item.healLife = life; item.healMana = mana; }
        }, ItemCombatHooks.Filter('player.ItemHealing'));
        const gate = P['Item QuickHeal_GetItemToUse()'];
        gate.hook((original, player) => {
            const outer = ItemHealingHooks.#selection;
            ItemHealingHooks.#selection = player;
            try { return original(player); }
            finally { ItemHealingHooks.#selection = outer; }
        });
        P['void QuickHeal_GetItemToUse_TryChoosingItem(int lifeDifference, ref Item bestItem, ref int bestDifference, Item nextItem)'].hook(
            (original, difference, bestItem, bestDifference, item) => {
                const player = ItemHealingHooks.#selection, life = item.healLife;
                if (!player || item.stack <= 0 || !item.potion || life <= 0) return original(difference, bestItem, bestDifference, item);
                item.healLife = ItemHealingHooks.#Heal(player, item, 'GetHealLife', life, true, player.statLife);
                try { return original(difference, bestItem, bestDifference, item); }
                finally { item.healLife = life; }
            }, { minType: 0, on: 3, marks: 'player.HealSelection', whileIn: gate });
    }

    static #PotionDelay() {
        const P = Terraria.Player, gate = P['void ApplyPotionDelay(Item sItem)'];
        gate.hook((original, player, item) => {
            const outer = ItemHealingHooks.#potion;
            const scope = { player, item, previous: player.potionDelay, blocked: false, completed: false };
            ItemHealingHooks.#potion = scope;
            try {
                const result = original(player, item);
                scope.completed = true;
                return result;
            } finally {
                if (scope.blocked || !scope.completed) player.potionDelay = scope.previous;
                ItemHealingHooks.#potion = outer;
            }
        }, ItemCombatHooks.Filter('player.PotionDelay'));
        P['void AddBuff(int type, int time, bool fromNetPvP)'].hook((original, player, type, time, quiet) => {
            const scope = ItemHealingHooks.#potion;
            if (!scope || bl.addressOf(scope.player) !== bl.addressOf(player) || type !== Terraria.ID.BuffID.PotionSickness)
                return original(player, type, time, quiet);
            player.potionDelay = scope.previous;
            const delay = new Ref(time);
            ItemCombatHooks.Call(scope.item, 'ModifyPotionDelay', player, delay);
            const value = ItemHealingHooks.#Amount(delay.value, time);
            const itemAllows = ItemCombatHooks.Call(scope.item, 'ApplyPotionDelay', player, value) !== false;
            const playerVeto = PlayerLoader.Veto(player, 'ApplyPotionDelay', scope.item, value);
            scope.blocked = value <= 0 || !itemAllows || playerVeto;
            if (scope.blocked) return;
            player.potionDelay = value;
            return original(player, type, value, quiet);
        }, { whileIn: gate });
        P['void TryToResetHungerToNeutral()'].hook((original, player) => {
            const scope = ItemHealingHooks.#potion;
            if (scope && scope.blocked && bl.addressOf(scope.player) === bl.addressOf(player)) return;
            return original(player);
        }, { whileIn: gate });
    }
}
