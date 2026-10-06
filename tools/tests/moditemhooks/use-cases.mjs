export function runUseCases({ vm, fs, path, source, context, sandbox, entity, p, plain, vanillaItem, call, method, setVanilla, test, assert, errors }) {
    vm.runInContext(`
        class UseProbe extends ModItem {}
        for (const name of ['UseAnimation', 'UseItemFrame', 'HoldItemFrame', 'NeedsAmmo', 'CanChooseAmmo', 'CanConsumeAmmo', 'OnConsumeAmmo'])
            UseProbe.prototype[name] = function (...args) { return record('use.' + name, ...args); };
        class AmmoProbe extends ModItem {}
        for (const name of ['CanBeChosenAsAmmo', 'CanBeConsumedAsAmmo', 'OnConsumedAsAmmo', 'PickAmmo'])
            AmmoProbe.prototype[name] = function (...args) { return record('ammo.' + name, ...args); };
        class AmmoPlayer extends ModPlayer {
            CanConsumeAmmo(...args) { return record('mp.CanConsumeAmmo', ...args); }
            OnConsumeAmmo(...args) { return record('mp.OnConsumeAmmo', ...args); }
        }
        globalThis.useApi = { UseProbe, AmmoProbe, AmmoPlayer, ItemUseHooks, ModItem, PlayerLoader, PlayerItemHooks, ItemLoader };
        ItemLoader.ByType.set(6150, new UseProbe()); ItemLoader.Hook(UseProbe, 6150);
    `, context);
    const { UseProbe, AmmoProbe, AmmoPlayer, ModItem, PlayerLoader, PlayerItemHooks, ItemLoader } = sandbox.useApi;
    for (const key of ['bool CanVisuallyHoldItem(Item item)', 'Item PickAmmo_PickAmmoItem(Item sItem)']) {
        const native = sandbox.Terraria.Player[key];
        p[key] = (...args) => native(p, ...args);
    }
    const weapon = entity(6150), ammo = entity(6151), fallback = entity(1);
    weapon.useAmmo = 1; weapon.shoot = 1; weapon.useAnimation = 20;
    const configureAmmo = (item, stack = 5) => Object.assign(item, { ammo: 1, stack, damage: 4, shoot: 2, shootSpeed: 3, knockBack: 2, consumable: true });
    configureAmmo(ammo); configureAmmo(fallback); fallback.consumable = false;
    const slots = () => { p.inventory = Array.from({ length: 58 }, () => entity(0)); p.inventory[0] = weapon; p.selectedItem = 0; p.ammoCyclingMode = 0; p.ammoCyclingOffset = 0; };
    const choose = () => call('Terraria.Player', 'PickAmmo_PickAmmoItem', p, weapon);
    const range = (order = [54, 55, 56, 57, 0, 1, 2, 3], cycle = true) => call('Terraria.Player', 'PickAmmo_IterateRange', p, weapon, p.inventory, order, cycle);
    setVanilla('Terraria.Player', 'PickAmmo_IterateRange', (_, actual, items, order) => order.map(index => items[index]).find(item => item?.stack > 0 && item.ammo === actual.useAmmo) || null);
    setVanilla('Terraria.Player', 'PickAmmo_PickAmmoItem', (_, actual) => {
        if (p.ammoCyclingMode === 1) { const selected = call('Terraria.Player', 'PickAmmo_IterateRange', p, actual, p.inventory, [54, 55, 56, 57], true); if (selected) return selected; }
        return call('Terraria.Player', 'PickAmmo_IterateRange', p, actual, p.inventory, [54, 55, 56, 57, ...Array.from({ length: 54 }, (_, i) => i)], p.ammoCyclingMode === 2 || p.ammoCyclingMode === 3);
    });
    setVanilla('Terraria.Player', 'HasAmmo', (_, actual, canUse) => canUse && (actual.useAmmo <= 0 || p.inventory.some(item => item.stack > 0 && item.ammo === actual.useAmmo)));
    setVanilla('Terraria.Player', 'ApplyItemAnimation', (player, item) => { sandbox.events.push('native.animation'); player.itemAnimation = player.itemAnimationMax = item.useAnimation; });
    setVanilla('Terraria.Player', 'PlayerFrame', player => { sandbox.events.push('native.frame'); player.bodyFrame = { Y: 56 }; });
    setVanilla('Terraria.Player', 'CanVisuallyHoldItem', () => sandbox.state.visible !== false);
    setVanilla('Terraria.Item', 'TurnToAir', item => { sandbox.events.push('native.clear'); item.type = 0; item.stack = 0; item.consumable = false; });
    sandbox.Terraria.Item.new = () => {
        const item = entity(0);
        item['void .ctor()'] = () => {};
        item['void SetDefaults(int Type, ItemVariant variant)'] = type => Object.assign(item, fallback, { type, __address: item.__address });
        item['void TurnToAir()'] = () => call('Terraria.Item', 'TurnToAir', item);
        return item;
    };
    ammo['void TurnToAir()'] = () => call('Terraria.Item', 'TurnToAir', ammo);
    setVanilla('Terraria.Player', 'PickAmmo', (player, actual, type, speed, canShoot, damage, knockback, used, dontConsume) => {
        const selected = call('Terraria.Player', 'PickAmmo_PickAmmoItem', player, actual);
        canShoot.value = !!selected; used.value = 0;
        if (!selected) return;
        sandbox.events.push('native.pick');
        used.value = selected.type; type.value = selected.shoot; speed.value += selected.shootSpeed;
        damage.value += selected.damage; knockback.value += selected.knockBack;
        if (sandbox.state.nativeError) throw Error('native');
        if (!dontConsume && selected.consumable && !sandbox.state.free) {
            selected.stack--;
            if (selected.stack <= 0) selected['void TurnToAir()']();
        }
    });
    const shoot = (dontConsume = false, actual = weapon) => {
        const refs = [1, 5, false, 20, 3, 0].map(value => ({ value }));
        call('Terraria.Player', 'PickAmmo', p, actual, ...refs, dontConsume);
        return refs.map(ref => ref.value);
    };
    const prepare = (stack = 5) => { slots(); ammo.type = 6151; configureAmmo(ammo, stack); p.inventory[54] = ammo; };
    test('use and ammo methods expose neutral defaults', () => {
        const item = new ModItem();
        for (const name of ['NeedsAmmo', 'CanConsumeAmmo', 'CanBeConsumedAsAmmo']) assert.equal(item[name](), true);
        for (const name of ['CanChooseAmmo', 'CanBeChosenAsAmmo']) assert.equal(item[name](), null);
    });
    test('frames exclude vanilla and plain items before JS', () => {
        for (const item of [plain, vanillaItem]) {
            p.inventory = [item]; p.selectedItem = 0;
            const before = method('Terraria.Player', 'PlayerFrame').entries;
            call('Terraria.Player', 'PlayerFrame', p);
            assert.equal(method('Terraria.Player', 'PlayerFrame').entries, before);
        }
    });
    test('frame filter skips absent inventory and invalid indices', () => {
        for (const [inventory, selectedItem] of [[null, 0], [[weapon], -1], [[weapon], 1], [[null], 0]]) {
            p.inventory = inventory; p.selectedItem = selectedItem;
            const before = method('Terraria.Player', 'PlayerFrame').entries;
            call('Terraria.Player', 'PlayerFrame', p);
            assert.equal(method('Terraria.Player', 'PlayerFrame').entries, before);
        }
    });
    test('UseAnimation runs before native duration is calculated', () => {
        sandbox.state['use.UseAnimation'] = (item, player) => { assert.equal(item, weapon); assert.equal(player, p); item.useAnimation = 31; };
        call('Terraria.Player', 'ApplyItemAnimation', p, weapon);
        assert.equal(p.itemAnimation, 31); assert.deepEqual(sandbox.events, ['use.UseAnimation', 'native.animation']); weapon.useAnimation = 20;
    });
    test('UseItemFrame edits the completed native body frame', () => {
        slots(); p.itemAnimation = 4;
        sandbox.state['use.UseItemFrame'] = (_, player) => { assert.equal(player.bodyFrame.Y, 56); player.bodyFrame.Y = 112; };
        call('Terraria.Player', 'PlayerFrame', p);
        assert.equal(p.bodyFrame.Y, 112); assert.deepEqual(sandbox.events, ['native.frame', 'use.UseItemFrame']);
    });
    test('HoldItemFrame runs only while idle and visually holding the item', () => {
        slots(); p.itemAnimation = 0;
        call('Terraria.Player', 'PlayerFrame', p); assert.deepEqual(sandbox.events, ['native.frame', 'use.HoldItemFrame']);
        sandbox.events.length = 0; sandbox.state.visible = false;
        call('Terraria.Player', 'PlayerFrame', p); assert.deepEqual(sandbox.events, ['native.frame']);
    });
    test('UseAnimation native hook excludes items without overrides', () => {
        const before = method('Terraria.Player', 'ApplyItemAnimation').entries;
        call('Terraria.Player', 'ApplyItemAnimation', p, plain);
        assert.equal(method('Terraria.Player', 'ApplyItemAnimation').entries, before);
    });
    test('weapon selection veto advances to the next eligible ammo slot', () => {
        prepare(); const other = configureAmmo(entity(2)); p.inventory[55] = other;
        sandbox.state['use.CanChooseAmmo'] = (_, actual) => actual === ammo ? false : null;
        assert.equal(choose(), other);
    });
    test('weapon selection true permits a different ammo category', () => {
        prepare(); ammo.ammo = 40; sandbox.state['use.CanChooseAmmo'] = (_, actual) => actual === ammo ? true : null;
        assert.equal(choose(), ammo); assert.equal(call('Terraria.Player', 'HasAmmo', p, weapon, true), true);
    });
    test('nullable selection preserves the category check', () => { prepare(); ammo.ammo = 40; assert.equal(choose(), null); });
    test('empty stacks and air never reach eligibility callbacks', () => {
        prepare(0); sandbox.state['use.CanChooseAmmo'] = (_, actual) => { assert.ok(actual.stack > 0 && actual.type > 0); };
        assert.equal(choose(), null);
    });
    vm.runInContext(`ItemLoader.ByType.set(6151, new AmmoProbe()); ItemLoader.Hook(AmmoProbe, 6151);`, context);
    test('ammo selection veto wins over weapon permission', () => {
        prepare(); sandbox.state['use.CanChooseAmmo'] = (_, actual) => actual === ammo ? true : null; sandbox.state['ammo.CanBeChosenAsAmmo'] = false;
        assert.equal(choose(), null);
    });
    test('ammo selection true permits a different category for a vanilla weapon', () => {
        prepare(); ammo.ammo = 40; sandbox.state['ammo.CanBeChosenAsAmmo'] = true;
        assert.equal(call('Terraria.Player', 'PickAmmo_PickAmmoItem', p, Object.assign(entity(1), { useAmmo: 1 })), ammo);
    });
    test('cycling counts accepted stacks and follows the native order', () => {
        prepare(); const other = configureAmmo(entity(2)), rejected = configureAmmo(entity(3)); p.inventory[55] = rejected; p.inventory[2] = other;
        p.ammoCyclingMode = 2; p.ammoCyclingOffset = 1;
        sandbox.state['use.CanChooseAmmo'] = (_, actual) => actual === rejected ? false : null;
        assert.equal(range(), other); p.ammoCyclingOffset = 2; assert.equal(range(), ammo);
        assert.equal(range(undefined, false), ammo);
    });
    for (const type of [5134, 779]) test('native weapon ' + type + ' retains first-slot selection during cycling', () => {
        prepare(); p.inventory[55] = configureAmmo(entity(2)); p.ammoCyclingMode = 2; p.ammoCyclingOffset = 1;
        assert.equal(call('Terraria.Player', 'PickAmmo_IterateRange', p, Object.assign(entity(type), { useAmmo: 1 }), p.inventory, [54, 55], true), ammo);
    });
    test('ammo-slot cycling falls back to the default native slot order', () => {
        prepare(); p.inventory[54] = entity(0); p.inventory[2] = ammo; p.ammoCyclingMode = 1;
        assert.equal(choose(), ammo);
    });
    test('HasAmmo honors selection veto and the canUse argument', () => {
        prepare(); sandbox.state['use.CanChooseAmmo'] = false;
        assert.equal(call('Terraria.Player', 'HasAmmo', p, weapon, true), false);
        sandbox.state['use.NeedsAmmo'] = false;
        assert.equal(call('Terraria.Player', 'HasAmmo', p, weapon, false), false);
    });
    test('NeedsAmmo false supplies default ammo without changing the inventory', () => {
        slots(); sandbox.state['use.NeedsAmmo'] = false;
        const result = shoot(); assert.deepEqual(result, [2, 8, true, 24, 5, 1]);
        assert.equal(p.inventory.some(item => item.type === 1), false);
        assert.equal(sandbox.events.includes('use.OnConsumeAmmo'), false);
    });
    test('NeedsAmmo default keeps missing ammo from shooting', () => { slots(); assert.equal(shoot()[2], false); });
    test('PickAmmo receives ammo first and edits the final projectile statistics', () => {
        prepare(); sandbox.state['ammo.PickAmmo'] = (actual, actualWeapon, player, type, speed, damage, kb) => {
            assert.equal(actual, ammo); assert.equal(actualWeapon, weapon); assert.equal(player, p);
            type.value = 10; speed.value = 12; damage.Flat += 6; kb.value = 9;
        };
        assert.deepEqual(shoot(), [10, 12, true, 30, 9, 6151]); assert.equal(ammo.stack, 4);
    });
    test('nonfinite projectile modifiers retain the native statistics', () => {
        prepare(); sandbox.state['ammo.PickAmmo'] = (_, weapon, player, type, speed, damage, kb) => { type.value = Infinity; speed.value = NaN; damage.Flat = Infinity; kb.value = NaN; };
        assert.deepEqual(shoot(), [2, 8, true, 24, 5, 6151]);
    });
    for (const hook of ['use.CanConsumeAmmo', 'ammo.CanBeConsumedAsAmmo']) test(hook + ' conserves ammo and suppresses notifications', () => {
        prepare(1); sandbox.state[hook] = false; assert.equal(shoot()[2], true);
        assert.equal(ammo.stack, 1); assert.equal(ammo.consumable, true);
        assert.equal(sandbox.events.includes('use.OnConsumeAmmo'), false); assert.equal(sandbox.events.includes('ammo.OnConsumedAsAmmo'), false);
    });
    test('last-unit callbacks see the original type and item instance before cleanup', () => {
        prepare(1); sandbox.state['ammo.OnConsumedAsAmmo'] = (actual, actualWeapon, player) => {
            assert.equal(actual, ammo); assert.equal(actual.type, 6151); assert.equal(actual.stack, 0); assert.equal(actualWeapon, weapon); assert.equal(player, p);
        };
        assert.equal(shoot()[5], 6151); assert.equal(ammo.type, 0);
        assert.deepEqual(sandbox.events.slice(-4), ['ammo.PickAmmo', 'use.OnConsumeAmmo', 'ammo.OnConsumedAsAmmo', 'native.clear']);
    });
    for (const mode of ['dontConsume', 'free', 'nonconsumable']) test(mode + ' suppresses consumption callbacks but still applies PickAmmo', () => {
        prepare(1); if (mode === 'free') sandbox.state.free = true; if (mode === 'nonconsumable') ammo.consumable = false;
        assert.equal(shoot(mode === 'dontConsume')[2], true); assert.equal(ammo.stack, 1);
        assert.ok(sandbox.events.includes('ammo.PickAmmo')); assert.equal(sandbox.events.includes('use.OnConsumeAmmo'), false);
    });
    test('selection veto suppresses consume and picked-ammo callbacks', () => {
        prepare(); sandbox.state['use.CanChooseAmmo'] = false; assert.equal(shoot()[2], false);
        assert.equal(sandbox.events.includes('use.CanConsumeAmmo'), false); assert.equal(sandbox.events.includes('ammo.PickAmmo'), false);
    });
    test('native exception restores a temporarily nonconsumable item', () => {
        prepare(); sandbox.state['use.CanConsumeAmmo'] = false; sandbox.state.nativeError = true;
        assert.throws(() => shoot(), /native/); assert.equal(ammo.consumable, true); assert.equal(ammo.stack, 5);
    });
    test('ammo callback failure is isolated and last-unit cleanup still runs', () => {
        prepare(1); const before = errors.length;
        sandbox.state['use.OnConsumeAmmo'] = () => { throw Error('consume callback'); };
        shoot(); assert.equal(ammo.type, 0); assert.ok(sandbox.events.includes('ammo.OnConsumedAsAmmo'));
        const removed = errors.splice(before); assert.equal(removed.length, 1); assert.equal(removed[0][0], 'UseProbe.OnConsumeAmmo');
    });
    test('same-player nested ammo queries restore the outer source', () => {
        prepare(2); const nested = Object.assign(entity(1), { useAmmo: 1 });
        sandbox.state['use.CanConsumeAmmo'] = () => { shoot(true, nested); return true; };
        shoot(); assert.equal(ammo.stack, 1); assert.equal(sandbox.events.filter(event => event === 'use.OnConsumeAmmo').length, 1);
    });
    vm.runInContext(`PlayerLoader.Add(AmmoPlayer); PlayerItemHooks.Install(AmmoPlayer);`, context);
    test('ModPlayer veto composes in the same ammo pipeline', () => {
        prepare(1); sandbox.state['mp.CanConsumeAmmo'] = false; shoot(); assert.equal(ammo.stack, 1);
        assert.equal(sandbox.events.includes('mp.OnConsumeAmmo'), false); assert.equal(method('Terraria.Player', 'PickAmmo').hooks.length, 1);
    });
    test('ammo notifications compose item, ammo and player once', () => {
        prepare(1); sandbox.state['mp.OnConsumeAmmo'] = (_, actualWeapon, actualAmmo) => { assert.equal(actualWeapon, weapon); assert.equal(actualAmmo.type, 6151); };
        shoot(); assert.deepEqual(sandbox.events.slice(-5), ['ammo.PickAmmo', 'use.OnConsumeAmmo', 'ammo.OnConsumedAsAmmo', 'mp.OnConsumeAmmo', 'native.clear']);
    });
    test('later inherited use overrides receive marks without duplicate hooks', () => {
        class Inherited extends UseProbe {}
        ItemLoader.ByType.set(6152, new Inherited()); ItemLoader.Hook(Inherited, 6152);
        const later = entity(6152); later.useAnimation = 17;
        call('Terraria.Player', 'ApplyItemAnimation', p, later); assert.equal(p.itemAnimation, 17);
        assert.ok(sandbox.events.includes('use.UseAnimation')); assert.equal(method('Terraria.Player', 'ApplyItemAnimation').hooks.length, 1);
    });
    for (const order of ['player weapon ammo', 'ammo player weapon', 'weapon ammo player']) test('use registration order ' + order + ' preserves shared pipelines', () => {
        const natives = new Map(), issues = [], events = [], marked = new Map();
        const native = new Proxy({}, { get(_, key) {
            if (!natives.has(key)) natives.set(key, { hook(callback) { this.callbacks.push(callback); }, callbacks: [] });
            return natives.get(key);
        } });
        const other = { Install() {} };
        const local = vm.createContext({ Terraria: { Player: native, Item: native }, FIRST_ITEM: 6145, SceneEffectPriority: { BossLow: 0 },
            PlayerUpdateHooks: other, PlayerJumpHooks: other, PlayerDrawHooks: other, PlayerWorldHooks: other, PlayerNetworkHooks: other, PlayerCombatHooks: other,
            Entities: { InstanceOf: (item, key, templates) => item[key] || (item[key] = Object.assign(Object.create(Object.getPrototypeOf(templates.get(item.type))), templates.get(item.type))) },
            Safe: { Run: (_, fn) => fn(), Report: (name, error) => issues.push([name, error]) }, events,
            bl: { mod: { uuid: 'use-order' }, addressOf: item => item.__address,
                hookMarks: { set(key, type) { if (!marked.has(key)) marked.set(key, new Set()); marked.get(key).add(type); } } },
        });
        for (const file of ['Core/Hooks.js', 'StatModifier.js', 'ModItem.js', 'ModPlayer.js', 'Loaders/ItemLoader.js', 'Loaders/ItemCombatHooks.js',
            'Loaders/ItemUseHooks.js', 'Loaders/PlayerItemHooks.js', 'Loaders/PlayerLoader.js']) vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), local);
        vm.runInContext(`
            class Weapon extends ModItem {
                UseAnimation(item, player) { events.push('animation'); item.useAnimation = 30; }
                OnConsumeAmmo(item, ammo, player) { events.push('weapon'); }
            }
            class Ammo extends ModItem { OnConsumedAsAmmo(item, weapon, player) { events.push('ammo'); } }
            class Player extends ModPlayer {
                UseAnimationMultiplier(player, item) { return 2; }
                CanConsumeAmmo(player, weapon, ammo) { return true; }
                OnConsumeAmmo(player, weapon, ammo) { events.push('player'); }
            }
            const install = {
                weapon() { ItemLoader.ByType.set(6145, new Weapon()); ItemLoader.Hook(Weapon, 6145); },
                ammo() { ItemLoader.ByType.set(6146, new Ammo()); ItemLoader.Hook(Ammo, 6146); },
                player() { PlayerLoader.Add(Player); PlayerLoader.Hook(Player); }
            };
            for (const name of ${JSON.stringify(order.split(' '))}) install[name]();
        `, local);
        const callbacks = name => [...natives].find(([key]) => key.includes(' ' + name + '('))[1].callbacks;
        const actualWeapon = Object.assign(entity(6145), { useAnimation: 20 }), actualAmmo = Object.assign(entity(6146), { consumable: true, stack: 2 });
        const player = { __address: 100000 };
        assert.equal(callbacks('ApplyItemAnimation').length, 1); assert.equal(callbacks('PickAmmo').length, 1);
        callbacks('ApplyItemAnimation')[0]((self, item) => { self.itemAnimation = self.itemAnimationMax = item.useAnimation; }, player, actualWeapon);
        assert.equal(player.itemAnimation, 60);
        const refs = [1, 5, false, 20, 3, 0].map(value => ({ value }));
        callbacks('PickAmmo')[0]((self, item, type, speed, canShoot) => {
            const chosen = callbacks('PickAmmo_PickAmmoItem')[0](() => actualAmmo, self, item);
            canShoot.value = true; chosen.stack--;
        }, player, actualWeapon, ...refs, false);
        assert.deepEqual(events, ['animation', 'weapon', 'ammo', 'player']); assert.deepEqual(issues, []);
        assert.ok(marked.get('player.Ammo').has(1)); assert.ok(marked.get('player.ItemAnimation').has(1));
    });
    p.inventory = [plain]; p.selectedItem = 0;
}
