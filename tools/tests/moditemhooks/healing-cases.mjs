export function runHealingCases({ vm, fs, path, source, context, sandbox, entity, p, targetPlayer, plain, vanillaItem, call, method, setVanilla, test, assert, errors }) {
    vm.runInContext(`
        class HealProbe extends ModItem {}
        for (const name of ['GetHealLife', 'GetHealMana', 'ModifyPotionDelay', 'ApplyPotionDelay'])
            HealProbe.prototype[name] = function (...args) { return record('heal.' + name, ...args); };
        class HealPlayer extends ModPlayer {}
        for (const name of ['GetHealLife', 'GetHealMana', 'ApplyPotionDelay'])
            HealPlayer.prototype[name] = function (...args) { return record('healPlayer.' + name, ...args); };
        ItemLoader.ByType.set(6160, new HealProbe()); ItemLoader.Hook(HealProbe, 6160);
        globalThis.healApi = { HealProbe, HealPlayer, ModItem, ItemLoader, PlayerLoader, PlayerItemHooks };
    `, context);
    const { HealProbe, HealPlayer, ModItem, ItemLoader, PlayerLoader, PlayerItemHooks } = sandbox.healApi;
    sandbox.Terraria.ID.BuffID.PotionSickness = 21;
    const potion = Object.assign(entity(6160), { healLife: 50, healMana: 20, potion: true });
    const buffs = [];
    const prepare = () => {
        Object.assign(p, { statLife: 100, statMana: 20, statLifeMax2: 500, statManaMax2: 200, potionDelay: 7, potionDelayTime: 3600 });
        Object.assign(potion, { healLife: 50, healMana: 20, stack: 5, potion: true });
        p.inventory = [potion]; buffs.length = 0;
    };
    const apply = (player = p, item = potion) => call('Terraria.Player', 'ApplyLifeAndOrMana', player, item);
    const delay = (player = p, item = potion) => call('Terraria.Player', 'ApplyPotionDelay', player, item);
    const choose = (player = p) => call('Terraria.Player', 'QuickHeal_GetItemToUse', player);
    setVanilla('Terraria.Player', 'ApplyLifeAndOrMana', (player, item) => {
        sandbox.events.push('native.heal');
        player.statLife = Math.min(player.statLifeMax2, player.statLife + item.healLife);
        player.statMana = Math.min(player.statManaMax2, player.statMana + item.healMana);
    });
    setVanilla('Terraria.Player', 'AddBuff', (player, type, time) => { buffs.push([player, type, time]); sandbox.events.push('native.buff'); });
    setVanilla('Terraria.Player', 'TryToResetHungerToNeutral', () => sandbox.events.push('native.hunger'));
    setVanilla('Terraria.Player', 'ApplyPotionDelay', (player) => {
        player.potionDelay = player.potionDelayTime;
        call('Terraria.Player', 'AddBuff', player, 21, player.potionDelayTime, false);
        if (sandbox.state.hunger) call('Terraria.Player', 'TryToResetHungerToNeutral', player);
        if (sandbox.state.potionNativeError) throw Error('native potion');
    });
    setVanilla('Terraria.Player', 'QuickHeal_GetItemToUse_TryChoosingItem', (difference, best, bestDifference, item) => {
        if (!item || item.stack <= 0 || item.type <= 0 || !item.potion || item.healLife <= 0) return;
        const next = item.healLife - difference;
        if (bestDifference.value < 0 ? next > bestDifference.value : next >= 0 && next < bestDifference.value) {
            best.value = item; bestDifference.value = next;
        }
    });
    setVanilla('Terraria.Player', 'QuickHeal_GetItemToUse', player => {
        const best = { value: null }, difference = { value: -player.statLifeMax2 };
        for (const item of player.inventory) call('Terraria.Player', 'QuickHeal_GetItemToUse_TryChoosingItem', player.statLifeMax2 - player.statLife, best, difference, item);
        return best.value;
    });
    setVanilla('Terraria.Player', 'QuickHeal', player => { const item = choose(player); if (item) apply(player, item); });
    setVanilla('Terraria.Player', 'QuickMana', player => { const item = player.inventory.find(item => item.stack > 0 && item.healMana > 0); if (item) apply(player, item); });
    test('healing exposes refs and a neutral potion permission', () => {
        const item = new ModItem(), ref = { value: 50 };
        item.GetHealLife(potion, p, false, ref); item.GetHealMana(potion, p, false, ref); item.ModifyPotionDelay(potion, p, ref);
        assert.equal(ref.value, 50); assert.equal(item.ApplyPotionDelay(), true);
    });
    for (const name of ['ApplyLifeAndOrMana', 'ApplyPotionDelay']) test(name + ' filters vanilla and plain items before JS', () => {
        prepare(); const entry = method('Terraria.Player', name), before = entry.entries;
        for (const item of [plain, vanillaItem]) call('Terraria.Player', name, p, Object.assign(item, { healLife: 50, healMana: 20 }));
        assert.equal(entry.entries, before); assert.equal(sandbox.events.some(event => event.startsWith('heal.')), false);
    });
    test('healing selection marks read static parameter three', () => {
        prepare(); const entry = method('Terraria.Player', 'QuickHeal_GetItemToUse_TryChoosingItem');
        assert.equal(entry.instance, false); assert.equal(entry.hooks[0].filter.on, 3);
        p.inventory = [Object.assign(plain, { potion: true, healLife: 50 })]; const before = entry.entries;
        choose(); assert.equal(entry.entries, before);
        p.inventory = [potion]; choose(); assert.equal(entry.entries, before + 1);
    });
    test('normal healing changes actual life and mana then restores item fields', () => {
        prepare(); sandbox.state['heal.GetHealLife'] = (item, player, quick, value) => {
            assert.equal(item, potion); assert.equal(player, p); assert.equal(quick, false); value.value = 80;
        };
        sandbox.state['heal.GetHealMana'] = (_, player, quick, value) => { assert.equal(quick, false); value.value = 35; };
        apply(); assert.equal(p.statLife, 180); assert.equal(p.statMana, 55);
        assert.equal(potion.healLife, 50); assert.equal(potion.healMana, 20);
        assert.deepEqual(sandbox.events, ['heal.GetHealLife', 'heal.GetHealMana', 'native.heal']);
    });
    test('a zero base channel does not invoke a healing callback', () => {
        prepare(); potion.healLife = 0; apply(); assert.equal(sandbox.events.includes('heal.GetHealLife'), false);
        sandbox.events.length = 0; potion.healMana = 0; apply(); assert.equal(sandbox.events.includes('heal.GetHealMana'), false);
    });
    for (const [value, expected] of [[-5, 100], [0, 100], [22.9, 122], [NaN, 150], [Infinity, 150], ['80', 150]]) test('life value ' + value + ' remains a valid integer', () => {
        prepare(); sandbox.state['heal.GetHealLife'] = (_, player, quick, ref) => { ref.value = value; };
        apply(); assert.equal(p.statLife, expected); assert.equal(potion.healLife, 50);
    });
    test('large healing values cannot overflow native signed addition', () => {
        prepare(); sandbox.state['heal.GetHealLife'] = (_, player, quick, ref) => { ref.value = 1e30; };
        const entry = method('Terraria.Player', 'ApplyLifeAndOrMana'), original = entry.vanilla;
        entry.vanilla = (player, item) => { assert.equal(item.healLife, 2147483647 - player.statLife); original(player, item); };
        try { apply(); } finally { entry.vanilla = original; }
        assert.equal(p.statLife, p.statLifeMax2);
    });
    test('healing fields restore when the native helper fails', () => {
        prepare(); sandbox.state['heal.GetHealLife'] = (_, player, quick, ref) => { ref.value = 80; };
        const entry = method('Terraria.Player', 'ApplyLifeAndOrMana'), original = entry.vanilla;
        entry.vanilla = () => { throw Error('native heal'); };
        try { assert.throws(apply, /native heal/); } finally { entry.vanilla = original; }
        assert.equal(potion.healLife, 50); assert.equal(potion.healMana, 20);
    });
    for (const name of ['QuickHeal', 'QuickMana']) test(name + ' supplies quickHeal true', () => {
        prepare(); const modes = [];
        sandbox.state['heal.GetHealLife'] = (_, player, quick) => modes.push(quick);
        sandbox.state['heal.GetHealMana'] = (_, player, quick) => modes.push(quick);
        call('Terraria.Player', name, p); assert.ok(modes.length >= 2); assert.ok(modes.every(Boolean));
        sandbox.events.length = 0; modes.length = 0; apply(); assert.deepEqual(modes, [false, false]);
    });
    test('quick healing context does not leak to another player', () => {
        prepare(); Object.assign(targetPlayer, { statLife: 50, statLifeMax2: 500, statMana: 0, statManaMax2: 200 });
        const seen = []; sandbox.state['heal.GetHealLife'] = (_, player, quick) => seen.push([player, quick]);
        const entry = method('Terraria.Player', 'QuickHeal'), original = entry.vanilla;
        entry.vanilla = () => { apply(targetPlayer); apply(p); };
        try { call('Terraria.Player', 'QuickHeal', p); } finally { entry.vanilla = original; }
        assert.deepEqual(seen, [[targetPlayer, false], [p, true]]);
    });
    test('quick healing context restores after a native failure', () => {
        prepare(); const entry = method('Terraria.Player', 'QuickMana'), original = entry.vanilla;
        entry.vanilla = () => { throw Error('quick failure'); };
        try { assert.throws(() => call('Terraria.Player', 'QuickMana', p)); } finally { entry.vanilla = original; }
        sandbox.state['heal.GetHealMana'] = (_, player, quick) => assert.equal(quick, false); apply();
    });
    test('selection uses the requesting player instead of Main.myPlayer', () => {
        prepare(); Object.assign(targetPlayer, { inventory: [potion], statLife: 50, statLifeMax2: 100 });
        sandbox.state['heal.GetHealLife'] = (_, player, quick, value) => { assert.equal(player, targetPlayer); assert.equal(quick, true); value.value = 30; };
        assert.equal(choose(targetPlayer), potion); assert.equal(potion.healLife, 50);
    });
    test('modified healing participates in native candidate ranking', () => {
        prepare(); p.statLifeMax2 = 160;
        const other = Object.assign(entity(1), { stack: 1, healLife: 70, potion: true }); p.inventory = [potion, other];
        assert.equal(choose(), other);
        sandbox.state['heal.GetHealLife'] = (_, player, quick, value) => { value.value = 60; };
        assert.equal(choose(), potion); assert.equal(potion.healLife, 50);
    });
    test('empty, nonpotion and zero-heal candidates do not invoke callbacks', () => {
        prepare(); p.inventory = [Object.assign(entity(6160), { healLife: 50, potion: true, stack: 0 }),
            Object.assign(entity(6160), { healLife: 50, potion: false }), Object.assign(entity(6160), { healLife: 0, potion: true })];
        assert.equal(choose(), null); assert.equal(sandbox.events.includes('heal.GetHealLife'), false);
    });
    test('static selection outside the native gate does not enter JS', () => {
        prepare(); const entry = method('Terraria.Player', 'QuickHeal_GetItemToUse_TryChoosingItem'), before = entry.entries;
        call('Terraria.Player', 'QuickHeal_GetItemToUse_TryChoosingItem', 50, { value: null }, { value: -100 }, potion);
        assert.equal(entry.entries, before);
    });
    test('healing callback failure is attributed and other channels continue', () => {
        prepare(); sandbox.state['heal.GetHealLife'] = () => { throw Error('heal callback'); };
        apply(); assert.equal(p.statLife, 150); assert.equal(p.statMana, 40);
        assert.equal(errors.pop()[0], 'HealProbe.GetHealLife');
    });
    test('potion modifier changes the counter and buff duration together', () => {
        prepare(); sandbox.state['heal.ModifyPotionDelay'] = (item, player, value) => { assert.equal(item, potion); assert.equal(player, p); assert.equal(value.value, 3600); value.value = 1234.9; };
        sandbox.state['heal.ApplyPotionDelay'] = (_, player, value) => { assert.equal(value, 1234); assert.equal(player.potionDelay, 7); return true; };
        delay(); assert.equal(p.potionDelay, 1234); assert.deepEqual(buffs, [[p, 21, 1234]]);
        assert.deepEqual(sandbox.events, ['heal.ModifyPotionDelay', 'heal.ApplyPotionDelay', 'native.buff']);
    });
    for (const value of [0, -30]) test('potion duration ' + value + ' prevents counter, buff and hunger effects', () => {
        prepare(); sandbox.state.hunger = true; sandbox.state['heal.ModifyPotionDelay'] = (_, player, ref) => { ref.value = value; };
        delay(); assert.equal(p.potionDelay, 7); assert.equal(buffs.length, 0); assert.equal(sandbox.events.includes('native.hunger'), false);
    });
    for (const value of [NaN, Infinity, '1200']) test('invalid potion duration ' + value + ' falls back to native duration', () => {
        prepare(); sandbox.state['heal.ModifyPotionDelay'] = (_, player, ref) => { ref.value = value; };
        delay(); assert.equal(p.potionDelay, 3600); assert.equal(buffs[0][2], 3600);
    });
    test('item potion veto preserves previous counter and skips hunger normalization', () => {
        prepare(); sandbox.state.hunger = true; sandbox.state['heal.ApplyPotionDelay'] = false;
        delay(); assert.equal(p.potionDelay, 7); assert.equal(buffs.length, 0); assert.equal(sandbox.events.includes('native.hunger'), false);
    });
    test('standalone AddBuff and hunger calls skip the potion JS hooks', () => {
        prepare(); sandbox.state['heal.ApplyPotionDelay'] = false;
        const buff = method('Terraria.Player', 'AddBuff'), hunger = method('Terraria.Player', 'TryToResetHungerToNeutral');
        const before = [buff.entries, hunger.entries];
        call('Terraria.Player', 'AddBuff', p, 21, 50, false); call('Terraria.Player', 'TryToResetHungerToNeutral', p);
        assert.deepEqual([buff.entries, hunger.entries], before); assert.equal(buffs.length, 1);
    });
    test('native potion failure restores the counter and releases its scope', () => {
        prepare(); sandbox.state.potionNativeError = true;
        assert.throws(delay, /native potion/); assert.equal(p.potionDelay, 7);
        sandbox.state.potionNativeError = false; sandbox.state['heal.ApplyPotionDelay'] = false;
        const entry = method('Terraria.Player', 'AddBuff'), before = entry.entries;
        call('Terraria.Player', 'AddBuff', p, 21, 80, false); assert.equal(entry.entries, before);
    });
    test('item potion callback failure is isolated', () => {
        prepare(); sandbox.state['heal.ApplyPotionDelay'] = () => { throw Error('potion callback'); };
        delay(); assert.equal(p.potionDelay, 3600); assert.equal(errors.pop()[0], 'HealProbe.ApplyPotionDelay');
    });
    PlayerLoader.Add(HealPlayer); PlayerItemHooks.Install(HealPlayer);
    test('healing composes item then ModPlayer in one native hook', () => {
        prepare(); sandbox.state['heal.GetHealLife'] = (_, player, quick, ref) => { ref.value += 5; };
        sandbox.state['healPlayer.GetHealLife'] = (_, item, quick, ref) => { assert.equal(ref.value, 55); ref.value *= 2; };
        sandbox.state['heal.GetHealMana'] = (_, player, quick, ref) => { ref.value += 3; };
        sandbox.state['healPlayer.GetHealMana'] = (_, item, quick, ref) => { assert.equal(ref.value, 23); ref.value *= 2; };
        apply(); assert.equal(p.statLife, 210); assert.equal(p.statMana, 66);
        assert.deepEqual(sandbox.events, ['heal.GetHealLife', 'healPlayer.GetHealLife', 'heal.GetHealMana', 'healPlayer.GetHealMana', 'native.heal']);
        assert.equal(method('Terraria.Player', 'ApplyLifeAndOrMana').hooks.length, 1);
    });
    test('ModPlayer sees modified potion delay even after item veto', () => {
        prepare(); sandbox.state['heal.ModifyPotionDelay'] = (_, player, ref) => { ref.value = 240; };
        sandbox.state['heal.ApplyPotionDelay'] = false;
        sandbox.state['healPlayer.ApplyPotionDelay'] = (_, item, value) => { assert.equal(item, potion); assert.equal(value, 240); return true; };
        delay(); assert.equal(p.potionDelay, 7); assert.ok(sandbox.events.includes('healPlayer.ApplyPotionDelay'));
    });
    test('ModPlayer potion veto wins over item permission', () => {
        prepare(); sandbox.state['heal.ApplyPotionDelay'] = true; sandbox.state['healPlayer.ApplyPotionDelay'] = false;
        delay(); assert.equal(p.potionDelay, 7); assert.equal(buffs.length, 0);
        assert.equal(method('Terraria.Player', 'ApplyPotionDelay').hooks.length, 1);
    });
    test('global healing observers include vanilla and late registered items', () => {
        prepare(); sandbox.state['healPlayer.GetHealLife'] = (_, item, quick, ref) => { ref.value = 90; };
        apply(p, vanillaItem); assert.equal(p.statLife, 190);
        class LateHeal extends HealProbe {}
        ItemLoader.ByType.set(6161, new LateHeal()); ItemLoader.Hook(LateHeal, 6161);
        const late = Object.assign(entity(6161), { healLife: 50, healMana: 0, potion: true });
        apply(p, late); assert.equal(p.statLife, 280); assert.equal(late.healLife, 50);
        delay(p, late); assert.equal(p.potionDelay, 3600);
    });
    test('MP zero-heal channels also preserve tModLoader eligibility', () => {
        prepare(); potion.healMana = 0; apply(); assert.equal(sandbox.events.includes('healPlayer.GetHealMana'), false);
    });
    for (const order of ['item player', 'player item']) test('healing registration order ' + order + ' shares native pipelines', () => {
        const hooks = new Map(), marked = new Map(), events = [];
        const P = new Proxy({}, { get(_, key) { if (!hooks.has(key)) hooks.set(key, []); return { hook: callback => hooks.get(key).push(callback) }; } });
        const local = vm.createContext({ Terraria: { Player: P }, FIRST_ITEM: 6145, SceneEffectPriority: { BossLow: 0 },
            Ref: sandbox.Ref, Entities: sandbox.Entities, Safe: sandbox.Safe,
            bl: { mod: { uuid: 'healing-order' }, addressOf: value => value.__address,
                hookMarks: { set(key, type) { if (!marked.has(key)) marked.set(key, new Set()); marked.get(key).add(type); } } },
            events });
        for (const file of ['Core/Hooks.js', 'StatModifier.js', 'ModItem.js', 'ModPlayer.js', 'Loaders/ItemLoader.js',
            'Loaders/ItemCombatHooks.js', 'Loaders/ItemUseHooks.js', 'Loaders/ItemHealingHooks.js', 'Loaders/PlayerItemHooks.js', 'Loaders/PlayerCombatHooks.js', 'Loaders/PlayerLoader.js'])
            vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), local, { filename: file });
        vm.runInContext(`
            class Potion extends ModItem { GetHealLife(item, player, quick, value) { events.push('item'); value.value += 10; } ApplyPotionDelay() { return true; } }
            class Player extends ModPlayer { GetHealLife(player, item, quick, value) { events.push('player'); value.value *= 2; } ApplyPotionDelay() { return true; } }
            const install = { item() { ItemLoader.ByType.set(6145, new Potion()); ItemLoader.Hook(Potion, 6145); }, player() { PlayerLoader.Add(Player); PlayerItemHooks.Install(Player); } };
            for (const name of ${JSON.stringify(order.split(' '))}) install[name]();
        `, local);
        const player = { __address: 999, statLife: 100, statMana: 0 }, item = { type: 6145, healLife: 50, healMana: 0 };
        const heal = hooks.get('void ApplyLifeAndOrMana(Item item)'); assert.equal(heal.length, 1);
        heal[0]((p, item) => { assert.equal(item.healLife, 120); }, player, item);
        assert.deepEqual(events, ['item', 'player']); assert.equal(item.healLife, 50);
        assert.equal(hooks.get('void ApplyPotionDelay(Item sItem)').length, 1);
        assert.equal(marked.get('player.ItemHealing').has(1), true);
    });
}
