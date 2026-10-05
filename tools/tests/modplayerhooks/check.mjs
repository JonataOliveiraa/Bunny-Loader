import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'app/src/main/cpp/script/js/mod');
const dump = fs.readFileSync(path.join(root, 'refs/dump.cs'), 'utf8');
const methods = new Map();
const parameterNames = new Map();
let namespace = '', owner = '';
function parts(text) {
    const result = [], start = [];
    let depth = 0;
    for (const c of text) {
        if (c === ',' && depth === 0) { result.push(start.join('').trim()); start.length = 0; continue; }
        start.push(c);
        if (c === '<' || c === '[') depth++;
        if (c === '>' || c === ']') depth--;
    }
    if (start.length) result.push(start.join('').trim());
    return result;
}
function type(value) {
    return value.replace(/<.*>/g, '').replace(/`\d/g, '').replace(/.*\./g, '');
}
function signature(value) {
    const match = value.match(/(\S+)\s+(\.?\w+)\((.*)\)/);
    if (!match) throw Error('Invalid signature: ' + value);
    const args = parts(match[3]).map((p) => {
        p = p.split('=')[0].trim();
        const words = p.split(/\s+/);
        return (words[0] === 'ref' || words[0] === 'out' ? '&' + type(words[1]) : type(words[0]));
    });
    return type(match[1]) + ' ' + match[2] + '(' + args.join(',') + ')';
}
function names(value) { return parts(value.match(/\((.*)\)/)[1]).map((parameter) => parameter.split('=')[0].trim().split(/\s+/).at(-1)); }
for (const line of dump.split(/\r?\n/)) {
    if (line.startsWith('// Namespace:')) namespace = line.slice(13).trim();
    const cls = line.match(/^(?:public|private|internal|protected).*?\b(?:class|struct)\s+([^\s:<]+)/);
    if (cls) { owner = (namespace ? namespace + '.' : '') + cls[1]; if (!methods.has(owner)) methods.set(owner, new Set()); }
    if (/^\s+(?:public|private|internal|protected).*?\([^)]*\).*\{ \}/.test(line) && owner) {
        try { methods.get(owner).add(signature(line)); parameterNames.set(owner + ':' + signature(line), names(line)); } catch {}
    }
}
const failures = [], installed = new Map(), stages = new Map(), natives = new Map(), warnings = [];
let address = 1;
function native(name) {
    if (natives.has(name)) return natives.get(name);
    const fields = {};
    const value = new Proxy(fields, {
        get(target, key) {
            if (key in target) return target[key];
            if (typeof key !== 'string') return undefined;
            if (key.includes('(')) {
                const normalized = signature(key), id = name + ':' + normalized;
                if (!installed.has(id)) installed.set(id, { name, signature: key, callbacks: [], vanilla: () => undefined });
                const entry = installed.get(id);
                const fn = (...args) => invoke(entry, args);
                fn.hook = (callback) => {
                    const known = methods.get(name) || methods.get(name.replace(/^Terraria\.(Player\.)/, '$1'));
                    if (!known?.has(normalized)) failures.push(name + ' ' + key);
                    const expected = parameterNames.get(id) || parameterNames.get(name.replace(/^Terraria\.(Player\.)/, '$1') + ':' + normalized);
                    if (expected && JSON.stringify(expected) !== JSON.stringify(names(key))) failures.push(name + ' ' + key + ' => ' + expected.join(', '));
                    entry.callbacks.push(callback);
                };
                return fn;
            }
            if (key === 'new') return () => ({ __address: address++ });
            return native(name + '.' + key);
        },
    });
    natives.set(name, value);
    return value;
}
function invoke(entry, args, index = 0) {
    return index < entry.callbacks.length ? entry.callbacks[index]((...next) => invoke(entry, next, index + 1), ...args) : entry.vanilla(...args);
}
function method(owner, name) {
    const matches = [...installed.values()].filter((entry) => entry.name === owner && entry.signature.split('(')[0].endsWith(' ' + name));
    assert.equal(matches.length, 1, owner + '.' + name + ' must resolve uniquely');
    return matches[0];
}
function call(owner, name, ...args) { return invoke(method(owner, name), args); }
function vanilla(owner, name, fn) { method(owner, name).vanilla = fn; }
const Terraria = native('Terraria'), Microsoft = native('Microsoft');
const Main = Terraria.Main;
Main.player = []; Main.npc = []; Main.projectile = []; Main.myPlayer = 0; Main.gameMenu = false; Main.netMode = 0;
Main.npcChatText = ''; Main.mouseItem = {}; Main.shop = [{ item: [] }]; Main.npcShop = 0;
const errors = [], once = new Set(), files = new Map();
const bl = {
    mod: { uuid: 'test' }, addressOf: (value) => {
        if (!value || typeof value !== 'object' && typeof value !== 'function') return undefined;
        if (value.__address === undefined) value.__address = address++;
        return value.__address;
    },
    classOf: (ns, cls) => native((ns ? ns + '.' : '') + cls),
    defineMethod: () => {}, installPlayerStage: (name, callback) => stages.set(name, callback),
    error: (error) => errors.push(error), log: () => {},
    file: { read: (file) => files.get(file), write: (file, data) => files.set(file, data), delete: (file) => files.delete(file) },
};
const sandbox = {
    assert, Terraria, Microsoft, bl,
    Ref: class { constructor(value) { this.value = value; } },
    Vector2: { new: (X = 0, Y = 0) => ({ X, Y }) },
    Color: { new: (R, G, B, A = 255) => ({ R, G, B, A }) },
    Entities: { Define: () => {}, Of: (inst) => Main.player.find((player) => player.__address === inst.__entity) },
    Hooks: {
        Once: (key, fn) => { if (!once.has(key)) { fn(); once.add(key); } },
        Overrides: (cls, base, key) => cls.prototype[key] !== base.prototype[key],
    },
    Safe: {
        Run: (name, fn) => { try { return fn(); } catch (error) { errors.push(name + ': ' + error.message); } },
        Once: (key, message) => warnings.push(message),
    },
    ArmorSetLoader: { WantFrame: () => {} }, HitLoader: {},
    ModRegistry: { Find: () => null }, ModLoader: {},
};
const context = vm.createContext(sandbox);
const filesToLoad = ['TagCompound.js', 'StatModifier.js', 'ModPlayer.js', 'Loaders/PlayerCombatHooks.js',
    'Loaders/PlayerItemHooks.js', 'Loaders/PlayerUpdateHooks.js', 'Loaders/PlayerJumpHooks.js',
    'Loaders/PlayerDrawHooks.js', 'Loaders/PlayerWorldHooks.js', 'Loaders/PlayerNetworkHooks.js', 'Loaders/PlayerLoader.js'];
for (const file of filesToLoad) vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
vm.runInContext(`
    class All extends ModPlayer {}
    for (const name of Object.getOwnPropertyNames(ModPlayer.prototype)) {
        if (name === 'constructor' || name === 'Player') continue;
        All.prototype[name] = function (...args) { return ModPlayer.prototype[name].apply(this, args); };
    }
    ModPlayer.register(All);
    globalThis.api = { ModPlayer, All, PlayerLoader, PlayerItemHooks, ExtraJump, PlayerDrawLayer, PlayerDrawLayers };
`, context);
assert.deepEqual(failures, [], 'All native hook signatures must exist in the local Terraria dump');
const { All, ModPlayer, PlayerLoader, ExtraJump, PlayerDrawLayer, PlayerDrawLayers } = sandbox.api;
const defaults = new Map(Object.getOwnPropertyNames(All.prototype).filter((key) => key !== 'constructor').map((key) => [key, All.prototype[key]]));
const player = (index) => {
    const value = { __address: address++, whoAmI: index, inventory: Array.from({ length: 58 }, () => item()), selectedItem: 0,
        statLife: 50, statLifeMax2: 100, statMana: 30, manaCost: 1, talkNPC: -1, CurrentLoadoutIndex: 0 };
    for (const entry of installed.values()) if (entry.name === 'Terraria.Player') value[entry.signature] = (...args) => invoke(entry, [value, ...args]);
    Main.player[index] = value;
    return value;
};
const item = (type = 0, stack = 0) => ({ __address: address++, type, stack, consumable: true, autoReuse: false, healLife: 0, healMana: 0 });
const p = player(0), second = player(1), npc = { __address: address++, whoAmI: 0, Hitbox: { X: 0, Y: 0, Width: 10, Height: 10 } };
Main.npc[0] = npc;
const reset = () => {
    for (const [key, method] of defaults) All.prototype[key] = method;
};
let checks = 0;
function test(name, run) { reset(); const before = errors.length; run(); assert.equal(errors.length, before, name + ': callback error'); checks++; }
test('per-player state', () => {
    assert.notEqual(All.get(p), All.get(second));
    All.get(p).value = 7;
    assert.equal(All.get(second).value, undefined);
});
test('nullable decisions and safe mod failures', () => {
    class Allowed extends ModPlayer { CanHitNPCWithProj() { return true; } }
    class Blocked extends ModPlayer { CanHitNPCWithProj() { return false; } }
    PlayerLoader.Add(Allowed); PlayerLoader.Add(Blocked);
    assert.equal(PlayerLoader.Nullable(p, 'CanHitNPCWithProj', {}, npc), false);
    assert.equal(PlayerLoader.Nullable(second, 'CanHitNPCWithItem', {}, npc), null);
});
test('damage modifiers before final hit and origin-specific callbacks', () => {
    const events = [], weapon = item(1, 1);
    All.prototype.ModifyHitNPC = (_, target, mods) => { assert.equal(target, npc); mods.SourceDamage.Multiplicative = 2; events.push('modify'); };
    All.prototype.OnHitNPC = (_, target, hit, done) => { assert.equal(hit.Damage, done); events.push('hit'); };
    All.prototype.OnHitNPCWithItem = (_, actual) => { assert.equal(actual, weapon); events.push('item'); };
    All.prototype.OnHitNPCWithProj = () => events.push('projectile');
    vanilla('Terraria.Player', 'ProcessHitAgainstNPC', (self, it, rect, damage, kb) => call('Terraria.NPC', 'StrikeNPC', npc, damage, kb, 1, false, false, self.whoAmI));
    vanilla('Terraria.NPC', 'StrikeNPC', (_, damage) => damage - 3);
    assert.equal(call('Terraria.Player', 'ProcessHitAgainstNPC', p, weapon, npc.Hitbox, 10, 1, 0), 17);
    assert.deepEqual(events, ['modify', 'hit', 'item']);
    events.length = 0;
    call('Terraria.NPC', 'StrikeNPC', npc, 10, 1, 1, false, true, 0);
    assert.deepEqual(events, []);
});
test('critical chance reference', () => {
    vanilla('Terraria.Player', 'GetWeaponCrit', () => 4);
    All.prototype.ModifyWeaponCrit = (_, weapon, crit) => { crit.value += 6; };
    assert.equal(call('Terraria.Player', 'GetWeaponCrit', p, item(1, 1)), 10);
});
test('PreItemCheck veto and PostItemCheck notification', () => {
    const events = [];
    vanilla('Terraria.Player', 'ItemCheck', () => events.push('vanilla'));
    All.prototype.PreItemCheck = () => false;
    All.prototype.PostItemCheck = () => events.push('post');
    call('Terraria.Player', 'ItemCheck', p);
    assert.deepEqual(events, ['post']);
});
test('ammo veto, last stack and dontConsume', () => {
    const weapon = item(1, 1), ammo = item(2, 1), events = [];
    vanilla('Terraria.Player', 'PickAmmo_PickAmmoItem', () => ammo);
    vanilla('Terraria.Player', 'PickAmmo', (self, it, ...args) => {
        const found = call('Terraria.Player', 'PickAmmo_PickAmmoItem', self, it);
        if (!args.at(-1) && found.consumable) found.stack--;
    });
    All.prototype.CanConsumeAmmo = () => false;
    All.prototype.OnConsumeAmmo = (_, weapon, actual) => events.push(actual);
    call('Terraria.Player', 'PickAmmo', p, weapon, {}, {}, {}, {}, {}, {}, false);
    assert.equal(ammo.stack, 1); assert.equal(ammo.consumable, true); assert.equal(events.length, 0);
    All.prototype.CanConsumeAmmo = () => true;
    call('Terraria.Player', 'PickAmmo', p, weapon, {}, {}, {}, {}, {}, {}, true);
    assert.equal(ammo.stack, 1); assert.equal(events.length, 0);
    call('Terraria.Player', 'PickAmmo', p, weapon, {}, {}, {}, {}, {}, {}, false);
    assert.equal(ammo.stack, 0); assert.equal(events.length, 1);
});
test('mana modifiers, missing mana and restoration', () => {
    const events = []; p.statMana = 30;
    All.prototype.ModifyManaCost = (_, weapon, reduce, mult) => { reduce.value = 0.5; mult.value = 2; };
    All.prototype.OnConsumeMana = (_, weapon, amount) => events.push(amount);
    All.prototype.OnMissingMana = (_, weapon, amount) => { events.push('missing:' + amount); p.statMana = 100; };
    vanilla('Terraria.Player', 'CheckMana', (self, amount, pay) => { if (pay) self.statMana -= Math.trunc(amount * self.manaCost); return true; });
    assert.equal(call('Terraria.Player', 'CheckMana', p, 40, true, false), true);
    assert.deepEqual(events, ['missing:40', 40]); assert.equal(p.statMana, 60); assert.equal(p.manaCost, 1);
});
test('healing preserves shared item defaults', () => {
    const potion = item(1, 1); potion.healLife = 50; potion.healMana = 20;
    All.prototype.GetHealLife = (_, it, quick, value) => { assert.equal(quick, false); value.value = 80; };
    vanilla('Terraria.Player', 'ApplyLifeAndOrMana', (_, it) => assert.equal(it.healLife, 80));
    call('Terraria.Player', 'ApplyLifeAndOrMana', p, potion);
    assert.equal(potion.healLife, 50);
});
test('incoming modifiers precede dodge, non-dodgeable damage cannot consume', () => {
    const events = [], reason = { _sourceNPCIndex: 0, _sourceProjectileLocalIndex: -1 };
    All.prototype.ModifyHitByNPC = (_, target, hit) => { hit.damage = 11; events.push('modify'); };
    All.prototype.FreeDodge = (_, source, damage) => { assert.equal(damage, 11); events.push('free'); return false; };
    All.prototype.ConsumableDodge = (_, info) => { assert.equal(info.Damage, 11); events.push('consume'); return true; };
    vanilla('Terraria.Player', 'Hurt', (_, source, damage) => { events.push('hurt'); return damage; });
    assert.equal(call('Terraria.Player', 'Hurt', p, reason, 20, 1, false, false, false, 0, true), 0);
    assert.deepEqual(events, ['modify', 'free', 'consume']);
    events.length = 0;
    assert.equal(call('Terraria.Player', 'Hurt', p, reason, 20, 1, false, false, false, 0, false), 11);
    assert.deepEqual(events, ['modify', 'hurt']);
});
test('fishing level and native bait stage', () => {
    const rod = item(5, 1), bait = item(6, 1); p.inventory[0] = rod; p.inventory[1] = bait;
    All.prototype.GetFishingLevel = (_, actualRod, actualBait, level) => { assert.equal(actualRod, rod); assert.equal(actualBait, bait); level.value = 75; };
    assert.equal(stages.get('GetFishingLevel')(p, { FinalFishingLevel: 40, PoleItemType: 5, BaitItemType: 6 }).FinalFishingLevel, 75);
    All.prototype.CanConsumeBait = () => false;
    assert.equal(stages.get('CanConsumeBait')(p, bait), false);
});
test('extra jump veto and duration modification', () => {
    const events = [];
    p.controlJump = true; p.releaseJump = true; p.canJumpAgain_Cloud = true; p.hasJumpOption_Cloud = true; p.isPerformingJump_Cloud = false;
    vanilla('Terraria.Player', 'JumpMovement', (self) => {
        if (self.canJumpAgain_Cloud) { self.canJumpAgain_Cloud = false; self.isPerformingJump_Cloud = true; self.jump = 10; }
    });
    All.prototype.CanStartExtraJump = () => false;
    call('Terraria.Player', 'JumpMovement', p);
    assert.equal(p.isPerformingJump_Cloud, false); assert.equal(p.canJumpAgain_Cloud, true);
    All.prototype.CanStartExtraJump = () => true;
    All.prototype.ModifyExtraJumpDurationMultiplier = (_, jump, duration) => { assert.equal(jump, ExtraJump.CloudInABottle); duration.value = 2; };
    All.prototype.OnExtraJumpStarted = () => events.push('started');
    call('Terraria.Player', 'JumpMovement', p);
    assert.equal(p.jump, 20); assert.deepEqual(events, ['started']);
});
test('client snapshot is separate and copied once across overloads', () => {
    const clone = player(2); let copies = 0;
    All.get(p).value = { count: 9 };
    All.prototype.CopyClientState = (_, target) => { copies++; target.value = { ...All.get(p).value }; };
    const one = [...installed.values()].find((entry) => entry.name === 'Terraria.Player' && entry.signature === 'object clientClone(Player clonePlayer)');
    const zero = [...installed.values()].find((entry) => entry.name === 'Terraria.Player' && entry.signature === 'Player clientClone()');
    one.vanilla = (_, target) => target;
    zero.vanilla = (self) => { invoke(one, [self, clone]); return clone; };
    assert.equal(invoke(zero, [p]), clone); assert.equal(copies, 1);
    All.get(p).value.count = 10;
    assert.equal(All.get(clone).value.count, 9);
});
test('save lifecycle and preservation of unloaded mods', () => {
    const events = [], file = { Player: p, Path: 'test.plr', IsCloudSave: false };
    files.set('test.plr.bl.json', JSON.stringify({ 'unloaded/Other': { count: 8 } }));
    All.prototype.PreSavePlayer = () => events.push('pre'); All.prototype.PostSavePlayer = () => events.push('post');
    All.prototype.PreSaveCustomData = () => events.push('custom'); All.prototype.SaveData = (data) => { data.count = 3; events.push('save'); };
    vanilla('Terraria.Player', 'InternalSavePlayerFile', () => events.push('vanilla'));
    call('Terraria.Player', 'InternalSavePlayerFile', file);
    assert.deepEqual(events, ['pre', 'vanilla', 'custom', 'save', 'post']);
    assert.equal(JSON.parse(files.get('test.plr.bl.json'))['unloaded/Other'].count, 8);
});
test('shoot references, cancellation and recursive projectile creation', () => {
    const weapon = item(1, 1), events = [];
    vanilla('Terraria.Projectile', 'NewProjectile', (...args) => { events.push(args); return 8; });
    vanilla('Terraria.Player', 'ItemCheck_Shoot', (self) => call('Terraria.Projectile', 'NewProjectile', null, 1, 2, 3, 4, 5, 6, 7, self.whoAmI, 0, 0, 0, null));
    All.prototype.ModifyShootStats = (_, it, position, velocity, type, damage) => {
        damage.value = 40;
        type.value = 20;
        position.value = { X: 10, Y: 15 };
        call('Terraria.Projectile', 'NewProjectile', null, 0, 0, 0, 0, 1, 2, 3, p.whoAmI, 0, 0, 0, null);
    };
    assert.equal(call('Terraria.Player', 'ItemCheck_Shoot', p, p.whoAmI, weapon, 5, true), 8);
    assert.equal(events.length, 2);
    assert.equal(events[1][1], 10); assert.equal(events[1][5], 20); assert.equal(events[1][6], 40);
    events.length = 0;
    All.prototype.Shoot = () => false;
    assert.equal(call('Terraria.Player', 'ItemCheck_Shoot', p, p.whoAmI, weapon, 5, true), 1000);
    assert.equal(events.length, 1);
});
test('item speed composes with DamageClass and overloaded item timing once', () => {
    const stats = sandbox.api.PlayerItemHooks;
    const weapon = item(1, 1); let factors = 0;
    const one = [...installed.values()].find((entry) => entry.name === 'Terraria.Player' && entry.signature === 'void ApplyItemTime(Item sItem)');
    const two = [...installed.values()].find((entry) => entry.name === 'Terraria.Player' && entry.signature === 'void ApplyItemTime(Item sItem, float multiplier)');
    two.vanilla = (self) => { self.itemTime = 30; self.itemTimeMax = 30; };
    one.vanilla = (self, it) => invoke(two, [self, it, 1]);
    stats.Stats = { time: (self) => { self.itemTime = 15; self.itemTimeMax = 15; } };
    All.prototype.UseSpeedMultiplier = () => { factors++; return 3; };
    All.prototype.UseTimeMultiplier = () => 2;
    invoke(one, [p, weapon]);
    assert.equal(p.itemTime, 10); assert.equal(factors, 1);
    stats.Stats = null;
});
test('potion veto receives actual randomized delay and restores state', () => {
    Terraria.ID.BuffID.PotionSickness = 21;
    const potion = item(1, 1), events = []; p.potionDelay = 11;
    vanilla('Terraria.Player', 'AddBuff', () => events.push('buff'));
    vanilla('Terraria.Player', 'ApplyPotionDelay', (self) => { self.potionDelay = 3145; call('Terraria.Player', 'AddBuff', self, 21, 3145, false); });
    All.prototype.ApplyPotionDelay = (_, it, delay) => { assert.equal(delay, 3145); return false; };
    call('Terraria.Player', 'ApplyPotionDelay', p, potion);
    assert.equal(p.potionDelay, 11); assert.equal(events.length, 0);
});
test('angler reward list can be removed, replaced and extended', () => {
    const fish = item(1, 2), extra = item(2, 3), deliveries = [], settings = {};
    vanilla('Terraria.Player', 'GetAnglerRewardRarityMultiplier', () => 0.25);
    vanilla('Terraria.Player', 'GetOrDropItem', (_, it) => deliveries.push(it));
    vanilla('Terraria.Player', 'GetAnglerReward', (self) => {
        call('Terraria.Player', 'GetAnglerRewardRarityMultiplier', 50);
        call('Terraria.Player', 'GetOrDropItem', self, fish, settings);
    });
    All.prototype.AnglerQuestReward = (_, multiplier, items) => { assert.equal(multiplier, 0.25); assert.equal(items[0], fish); items.splice(0, 1, extra); };
    call('Terraria.Player', 'GetAnglerReward', p, npc, 1);
    assert.deepEqual(deliveries, [extra]);
});
test('dye trader empty reward pool stays safe for native selection', () => {
    const pool = { values: [1, 2], ToArray() { return this.values; }, Clear() { this.values = []; }, Add(n) { this.values.push(n); }, get Count() { return this.values.length; } };
    All.prototype.GetDyeTraderReward = (_, rewards) => rewards.splice(0);
    stages.get('GetDyeTraderReward')(p, pool);
    assert.deepEqual(pool.values, [0]);
});
test('shop veto prevents payment and post notifications require success', () => {
    const events = [], merchandise = item(1, 1), inventory = [merchandise]; p.talkNPC = 0;
    Main.mouseItem = merchandise;
    vanilla('Terraria.Player', 'BuyItem', () => { events.push('paid'); return true; });
    vanilla('Terraria.UI.ItemSlot', 'HandleShopSlot', () => call('Terraria.Player', 'BuyItem', p, 100, -1));
    All.prototype.CanBuyItem = () => false;
    All.prototype.PostBuyItem = () => events.push('post');
    call('Terraria.UI.ItemSlot', 'HandleShopSlot', inventory, 0, false, true);
    assert.deepEqual(events, []);
    All.prototype.CanBuyItem = () => true;
    call('Terraria.UI.ItemSlot', 'HandleShopSlot', inventory, 0, false, true);
    assert.deepEqual(events, ['paid', 'post']);
    p.talkNPC = -1;
});
test('nurse partial and free healing preserves debuffs and shared flags', () => {
    Main.debuff = []; Main.debuff[20] = true; p.buffType = [20]; p.statLife = 50; p.statLifeMax2 = 100; p.talkNPC = 0;
    const events = [];
    All.prototype.ModifyNurseHeal = (_, nurse, health, remove) => { health.value = 10; remove.value = false; return true; };
    All.prototype.ModifyNursePrice = (_, nurse, health, remove, price) => { assert.equal(health, 10); assert.equal(remove, false); price.value = 0; };
    All.prototype.PostNurseHeal = (_, nurse, health, remove, price) => events.push(['post', health, remove, price]);
    vanilla('Terraria.Main', 'GetNurseHealCost', () => (p.statLifeMax2 - p.statLife) * 100);
    vanilla('Terraria.Player', 'BuyItem', (_, price) => { events.push(['paid', price]); return true; });
    vanilla('Terraria.Main', 'NPCChatText_DoNurseHeal', (price) => {
        assert.equal(price, 1);
        if (call('Terraria.Player', 'BuyItem', p, price, -1)) p.statLife = p.statLifeMax2;
        assert.equal(Main.debuff[20], false);
    });
    const cost = call('Terraria.Main', 'GetNurseHealCost');
    call('Terraria.Main', 'NPCChatText_DoNurseHeal', cost);
    assert.equal(cost, 0); assert.equal(p.statLife, 60); assert.equal(p.statLifeMax2, 100); assert.equal(Main.debuff[20], true);
    assert.deepEqual(events, [['paid', 0], ['post', 10, false, 0]]);
    p.talkNPC = -1;
});
test('draw visibility and ordering preserve cached draw data', () => {
    const cache = Array(10); cache.cloneResized = () => [...cache];
    const info = { __address: address++, drawPlayer: p, DrawDataCache: cache, DrawDataCacheCount: 0 };
    for (const name of ['Skin', 'Torso', 'Head']) {
        vanilla('Terraria.DataStructures.PlayerDrawLayers', PlayerDrawLayers[name].Method, (draw) => { draw.DrawDataCache[draw.DrawDataCacheCount++] = name; });
    }
    vanilla('Terraria.Graphics.Renderers.LegacyPlayerRenderer', 'DrawPlayer_UseNormalLayers', (draw) => {
        for (const name of ['Skin', 'Torso', 'Head']) call('Terraria.DataStructures.PlayerDrawLayers', PlayerDrawLayers[name].Method, draw);
    });
    All.prototype.HideDrawLayers = () => PlayerDrawLayers.Torso.Hide();
    call('Terraria.Graphics.Renderers.LegacyPlayerRenderer', 'DrawPlayer_UseNormalLayers', info);
    assert.deepEqual(cache.slice(0, info.DrawDataCacheCount), ['Skin', 'Head']);
    assert.equal(PlayerDrawLayers.Torso.IsHidden, false);
    delete All.prototype.HideDrawLayers;
    info.DrawDataCacheCount = 0;
    All.prototype.ModifyDrawLayerOrdering = (_, positions) => positions.set(PlayerDrawLayers.Skin, PlayerDrawLayer.AfterParent(PlayerDrawLayers.Head));
    call('Terraria.Graphics.Renderers.LegacyPlayerRenderer', 'DrawPlayer_UseNormalLayers', info);
    assert.deepEqual(cache.slice(0, 3), ['Torso', 'Head', 'Skin']);
    info.DrawDataCacheCount = 0;
    All.prototype.ModifyDrawLayerOrdering = (_, positions) => {
        positions.set(PlayerDrawLayers.Skin, PlayerDrawLayer.AfterParent(PlayerDrawLayers.Head));
        positions.set(PlayerDrawLayers.Head, PlayerDrawLayer.AfterParent(PlayerDrawLayers.Skin));
    };
    call('Terraria.Graphics.Renderers.LegacyPlayerRenderer', 'DrawPlayer_UseNormalLayers', info);
    assert.deepEqual(cache.slice(0, 3), ['Skin', 'Torso', 'Head']); assert.equal(warnings.length, 1);
});
test('crafting materials supply native sources and callback source indices', () => {
    const material = item(1, 3), empty = item(), consumed = [], list = { values: [], Add(value) { this.values.push(value); } };
    Terraria.GameContent.DestinationInventory.new = () => ({ __address: address++, 'void .ctor(InventoryStorage storage, Vector2 position)'(storage) { this.storage = storage; } });
    All.prototype.AddMaterialsForCrafting = (_, callback) => { callback.value = (actual, index) => consumed.push([actual, index]); return [empty, material, material]; };
    vanilla('Terraria.Recipe', 'BuildPlayerChestSourceList', () => {});
    vanilla('Terraria.GameContent.CraftingRequests', 'ConsumeItemsFrom', (inventory) => { inventory[0].stack--; });
    call('Terraria.Recipe', 'BuildPlayerChestSourceList', p, list);
    assert.equal(list.values.length, 1);
    const storage = list.values[0].storage;
    assert.equal(storage.maxItems, 1);
    call('Terraria.GameContent.CraftingRequests', 'ConsumeItemsFrom', storage.item, 1, {}, {}, {}, -1);
    assert.equal(material.stack, 2); assert.deepEqual(consumed, [[material, 1]]);
});
test('full crafting consumption clears original source and preserves consumed ingredient', () => {
    const material = item(1, 2), callbacks = [], list = { values: [], Add(value) { this.values.push(value); } }, consumed = [];
    Object.defineProperty(consumed, 'Count', { get: () => consumed.length });
    consumed.get_Item = (index) => consumed[index];
    consumed.set_Item = (index, value) => { consumed[index] = value; };
    material['Item Clone()'] = () => item(material.type, material.stack);
    material['void TurnToAir()'] = () => { material.type = 0; material.stack = 0; };
    All.prototype.AddMaterialsForCrafting = (_, callback) => { callback.value = (actual, index) => callbacks.push([actual, index]); return [material]; };
    vanilla('Terraria.GameContent.CraftingRequests', 'ConsumeItemsFrom', (inventory, max, req, remaining, used) => {
        used.push(inventory[0]); inventory[0] = item(); remaining.value = 0;
    });
    call('Terraria.Recipe', 'BuildPlayerChestSourceList', p, list);
    const storage = list.values[0].storage;
    call('Terraria.GameContent.CraftingRequests', 'ConsumeItemsFrom', storage.item, 1, {}, { value: 2 }, consumed, -1);
    assert.equal(material.type, 0); assert.equal(material.stack, 0); assert.equal(storage.item[0], material);
    assert.deepEqual(callbacks, [[material, 0]]); assert.notEqual(consumed[0], material);
    assert.equal(consumed[0].type, 1); assert.equal(consumed[0].stack, 2);
});
test('crafting clears full stacks without callbacks and retains other player sources', () => {
    const material = item(1, 2), list = { values: [], Add(value) { this.values.push(value); } };
    material['void TurnToAir()'] = () => { material.type = 0; material.stack = 0; };
    All.prototype.AddMaterialsForCrafting = (self) => self === p ? [material] : null;
    vanilla('Terraria.GameContent.CraftingRequests', 'ConsumeItemsFrom', (inventory, max, req, remaining) => { inventory[0] = item(); remaining.value = 0; });
    call('Terraria.Recipe', 'BuildPlayerChestSourceList', p, list);
    call('Terraria.Recipe', 'BuildPlayerChestSourceList', second, { Add() {} });
    const storage = list.values[0].storage;
    call('Terraria.GameContent.CraftingRequests', 'ConsumeItemsFrom', storage.item, 1, {}, { value: 2 }, null, -1);
    assert.equal(material.type, 0); assert.equal(material.stack, 0); assert.equal(storage.item[0], material);
});
assert.equal(errors.length, 0);
console.log(`${checks} behavior checks passed; ${[...installed.values()].filter((entry) => entry.callbacks.length).length} native signatures verified; ${stages.size} native stages registered.`);
