// Os hooks de item que o jogador usa (dano, crítico, munição, cura...), para
// o ModItem e o GlobalItem juntos: o Call chama o do item de mod e os Globais
// na ordem do tModLoader e junta as respostas como lá. O filtro nativo é por
// tipo de item: o item de mod que escreve o método, ou todos (um Global).
class ItemCombatHooks {
    static #plans = new WeakMap();
    static #all = new Set();
    static #methods = ['ModifyWeaponDamage', 'ModifyWeaponCrit', 'ModifyWeaponKnockback', 'ModifyItemScale',
        'CanHitNPC', 'ModifyHitNPC', 'OnHitNPC', 'CanHitPvp', 'ModifyHitPvp', 'OnHitPvp',
        'CanMeleeAttackCollideWithNPC', 'MeleeEffects', 'UseItemHitbox', 'UseAnimation', 'UseItemFrame', 'HoldItemFrame',
        'NeedsAmmo', 'CanChooseAmmo', 'CanBeChosenAsAmmo', 'CanConsumeAmmo', 'CanBeConsumedAsAmmo',
        'OnConsumeAmmo', 'OnConsumedAsAmmo', 'PickAmmo', 'GetHealLife', 'GetHealMana', 'ModifyPotionDelay', 'ApplyPotionDelay'];
    // Como as respostas se juntam: 'and', bool com padrão true (algum false
    // veta); 'nullable', bool? (algum false veta, senão algum true força).
    static #JOIN = {
        NeedsAmmo: 'and', CanConsumeAmmo: 'and', CanBeConsumedAsAmmo: 'and', ApplyPotionDelay: 'and', CanHitPvp: 'and',
        CanChooseAmmo: 'nullable', CanBeChosenAsAmmo: 'nullable', CanHitNPC: 'nullable', CanMeleeAttackCollideWithNPC: 'nullable',
    };
    // O ModifyWeaponDamage dos Globais tem o caminho dele (PlayerItemHooks).
    static #OWN_GLOBAL_PATH = new Set(['ModifyWeaponDamage']);

    static #Plan(cls, Base = ModItem) {
        let plan = ItemCombatHooks.#plans.get(cls);
        if (plan) return plan;
        plan = Object.create(null);
        for (const name of ItemCombatHooks.#methods) {
            if (Hooks.Overrides(cls, Base, name)) plan[name] = cls.name + '.' + name;
        }
        ItemCombatHooks.#plans.set(cls, plan);
        return plan;
    }

    // Sem alocação no caminho comum (sem Global): a seleção de munição chama
    // isto duas vezes por espaço do inventário, e o PlayerFrame todo quadro.
    // Com objeto de resposta, ...args e spread a cada chamada, a munição ficou
    // ~40% mais cara (hookperf, 2026-10-09).
    static Call(item, name, a, b, c, d, e, f) {
        if (!item) return undefined;
        const count = arguments.length - 2;
        const template = ItemLoader.ByType.get(item.type);
        const label = template ? ItemCombatHooks.#Plan(template.constructor)[name] : undefined;
        const m = label ? ItemLoader.Of(item) : null;
        let own;
        if (m) {
            try { own = ItemCombatHooks.#Invoke(m, name, item, count, a, b, c, d, e, f); }
            catch (err) { Safe.Report(label, err); }
        }
        const globals = ItemCombatHooks.#OWN_GLOBAL_PATH.has(name) ? null : globalItems.For(item, name);
        if (!globals || !globals.length) return own;

        const results = m ? [own] : [];
        // O PickAmmo é chamado na munição; o do Global recebe (arma, munição, ...).
        const pickAmmo = name === 'PickAmmo';
        for (const g of globals) {
            try {
                results.push(pickAmmo
                    ? ItemCombatHooks.#Invoke(g, name, a, count, item, b, c, d, e, f)
                    : ItemCombatHooks.#Invoke(g, name, item, count, a, b, c, d, e, f));
            } catch (err) { Safe.Report(g.constructor.name + '.' + name, err); }
        }
        switch (ItemCombatHooks.#JOIN[name]) {
            case 'and': return !results.includes(false);
            case 'nullable': return results.includes(false) ? false : results.includes(true) ? true : null;
            default: return own;
        }
    }

    // target[name](first, ...os count seguintes), sem montar array.
    static #Invoke(target, name, first, count, a, b, c, d, e, f) {
        switch (count) {
            case 0: return target[name](first);
            case 1: return target[name](first, a);
            case 2: return target[name](first, a, b);
            case 3: return target[name](first, a, b, c);
            case 4: return target[name](first, a, b, c, d);
            case 5: return target[name](first, a, b, c, d, e);
            default: return target[name](first, a, b, c, d, e, f);
        }
    }

    static Has(item, name) {
        const template = item && ItemLoader.ByType.get(item.type);
        if (template && ItemCombatHooks.#Plan(template.constructor)[name]) return true;
        return !!item && item.type > 0 && !ItemCombatHooks.#OWN_GLOBAL_PATH.has(name) && globalItems.For(item, name).length > 0;
    }

    static Filter(key) { return { minType: 0, on: 0, marks: key }; }

    static All(key) {
        if (ItemCombatHooks.#all.has(key)) return;
        ItemCombatHooks.#all.add(key);
        for (let type = 0; type < FIRST_ITEM; type++) bl.hookMarks.set(key, type, true);
        for (const type of ItemLoader.ByType.keys()) bl.hookMarks.set(key, type, true);
    }

    // type -1: todo item (um Global).
    static Mark(key, type) {
        if (type < 0) ItemCombatHooks.All(key);
        else bl.hookMarks.set(key, type, true);
    }

    static Install(cls, type) {
        for (const key of ItemCombatHooks.#all) bl.hookMarks.set(key, type, true);
        ItemCombatHooks.#Setup(ItemCombatHooks.#Plan(cls), type);
    }

    static InstallGlobal(cls) {
        ItemCombatHooks.#Setup(ItemCombatHooks.#Plan(cls, GlobalItem), -1);
    }

    static #Setup(plan, type) {
        const marked = (key, names) => {
            if (!names.some(name => plan[name])) return false;
            ItemCombatHooks.Mark(key, type);
            return true;
        };
        if (type >= 0 && marked('player.WeaponDamage', ['ModifyWeaponDamage'])) PlayerItemHooks.InstallDamage();
        if (marked('player.WeaponCrit', ['ModifyWeaponCrit'])) PlayerItemHooks.InstallCrit();
        if (marked('player.WeaponKnockback', ['ModifyWeaponKnockback'])) PlayerItemHooks.InstallKnockback();
        const scale = marked('player.ItemScale', ['ModifyItemScale']);
        if (marked('player.ItemHitbox', ['ModifyItemScale', 'UseItemHitbox'])) PlayerItemHooks.InstallHitbox();
        if (scale) PlayerItemHooks.InstallScale();
        if (type >= 0) {
            const attack = marked('player.ItemAttack', ['CanHitNPC', 'ModifyHitNPC', 'OnHitNPC', 'CanMeleeAttackCollideWithNPC']);
            if (attack) CombatLoader.InstallItemCombat();
            if (plan.CanMeleeAttackCollideWithNPC) CombatLoader.InstallCollision();
            if (marked('player.PvpAttack', ['CanHitPvp', 'ModifyHitPvp', 'OnHitPvp'])) CombatLoader.InstallPvp();
        }
        if (marked('player.MeleeEffects', ['MeleeEffects'])) CombatLoader.InstallMeleeEffects();
        ItemUseHooks.Install(type, plan);
        ItemHealingHooks.Install(type, plan);
    }
}
