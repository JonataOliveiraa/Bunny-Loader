class ItemCombatHooks {
    static #plans = new WeakMap();
    static #all = new Set();
    static #methods = ['ModifyWeaponDamage', 'ModifyWeaponCrit', 'ModifyWeaponKnockback', 'ModifyItemScale',
        'CanHitNPC', 'ModifyHitNPC', 'CanHitPvp', 'ModifyHitPvp', 'OnHitPvp',
        'CanMeleeAttackCollideWithNPC', 'MeleeEffects', 'UseItemHitbox', 'UseAnimation', 'UseItemFrame', 'HoldItemFrame',
        'NeedsAmmo', 'CanChooseAmmo', 'CanBeChosenAsAmmo', 'CanConsumeAmmo', 'CanBeConsumedAsAmmo',
        'OnConsumeAmmo', 'OnConsumedAsAmmo', 'PickAmmo'];

    static #Plan(cls) {
        let plan = ItemCombatHooks.#plans.get(cls);
        if (plan) return plan;
        plan = Object.create(null);
        for (const name of ItemCombatHooks.#methods) {
            if (Hooks.Overrides(cls, ModItem, name)) plan[name] = cls.name + '.' + name;
        }
        ItemCombatHooks.#plans.set(cls, plan);
        return plan;
    }

    static Call(item, name, a, b, c, d, e, f) {
        const template = item && ItemLoader.ByType.get(item.type);
        if (!template) return undefined;
        const label = ItemCombatHooks.#Plan(template.constructor)[name];
        if (!label) return undefined;
        const m = ItemLoader.Of(item);
        if (!m) return undefined;
        try {
            switch (arguments.length) {
                case 2: return m[name](item);
                case 3: return m[name](item, a);
                case 4: return m[name](item, a, b);
                case 5: return m[name](item, a, b, c);
                case 6: return m[name](item, a, b, c, d);
                case 7: return m[name](item, a, b, c, d, e);
                default: return m[name](item, a, b, c, d, e, f);
            }
        } catch (e) { Safe.Report(label, e); }
    }

    static Has(item, name) {
        const template = item && ItemLoader.ByType.get(item.type);
        return !!template && !!ItemCombatHooks.#Plan(template.constructor)[name];
    }

    static Filter(key) { return { minType: 0, on: 0, marks: key }; }

    static All(key) {
        if (ItemCombatHooks.#all.has(key)) return;
        ItemCombatHooks.#all.add(key);
        for (let type = 0; type < FIRST_ITEM; type++) bl.hookMarks.set(key, type, true);
        for (const type of ItemLoader.ByType.keys()) bl.hookMarks.set(key, type, true);
    }

    static Install(cls, type) {
        const plan = ItemCombatHooks.#Plan(cls);
        for (const key of ItemCombatHooks.#all) bl.hookMarks.set(key, type, true);
        const marked = (key, names) => {
            if (!names.some(name => plan[name])) return false;
            bl.hookMarks.set(key, type, true);
            return true;
        };
        if (marked('player.WeaponDamage', ['ModifyWeaponDamage'])) PlayerItemHooks.InstallDamage();
        if (marked('player.WeaponCrit', ['ModifyWeaponCrit'])) PlayerItemHooks.InstallCrit();
        if (marked('player.WeaponKnockback', ['ModifyWeaponKnockback'])) PlayerItemHooks.InstallKnockback();
        const scale = marked('player.ItemScale', ['ModifyItemScale']);
        if (marked('player.ItemHitbox', ['ModifyItemScale', 'UseItemHitbox'])) PlayerItemHooks.InstallHitbox();
        if (scale) PlayerItemHooks.InstallScale();
        const attack = marked('player.ItemAttack', ['CanHitNPC', 'ModifyHitNPC', 'CanMeleeAttackCollideWithNPC']);
        if (attack) PlayerCombatHooks.InstallItemCombat();
        if (plan.CanMeleeAttackCollideWithNPC) PlayerCombatHooks.InstallCollision();
        if (marked('player.MeleeEffects', ['MeleeEffects'])) PlayerCombatHooks.InstallMeleeEffects();
        if (marked('player.PvpAttack', ['CanHitPvp', 'ModifyHitPvp', 'OnHitPvp'])) {
            PlayerCombatHooks.InstallPvp();
            PlayerLoader.InstallItemPvp();
        }
        ItemUseHooks.Install(type, plan);
    }
}
