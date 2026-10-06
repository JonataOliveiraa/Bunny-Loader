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
                if (!installed.has(id)) installed.set(id, { name, signature: key, callbacks: [], filters: [], active: 0, vanilla: () => undefined });
                const entry = installed.get(id);
                const fn = (...args) => invoke(entry, args);
                fn.entry = entry;
                fn.hook = (callback, filter = {}) => {
                    const known = methods.get(name) || methods.get(name.replace(/^Terraria\.(Player\.)/, '$1'));
                    if (!known?.has(normalized)) failures.push(name + ' ' + key);
                    const expected = parameterNames.get(id) || parameterNames.get(name.replace(/^Terraria\.(Player\.)/, '$1') + ':' + normalized);
                    if (expected && JSON.stringify(expected) !== JSON.stringify(names(key))) failures.push(name + ' ' + key + ' => ' + expected.join(', '));
                    entry.callbacks.push(callback);
                    entry.filters.push(filter);
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
    if (index >= entry.callbacks.length) return entry.vanilla(...args);
    const filter = entry.filters[index];
    if (filter.flag && !flags.get(filter.flag) || filter.whileIn && !filter.whileIn.entry.active) return invoke(entry, args, index + 1);
    entry.active++;
    try { return entry.callbacks[index]((...next) => invoke(entry, next, index + 1), ...args); }
    finally { entry.active--; }
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
const flags = new Map(), marks = new Map();
const bl = {
    hookMarks: { set(key, type) { if (!marks.has(key)) marks.set(key, new Set()); marks.get(key).add(type); } },
    hookFlags: { set: (key, value) => flags.set(key, value), get: key => flags.get(key) || false },
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
    Entities: { Define: () => {}, InstanceOf: (npc) => npc.ModNPC, Of: (inst) => Main.player.find((player) => player.__address === inst.__entity) },
    SceneEffectPriority: { BossLow: 0 }, FIRST_NPC: 697, FIRST_ITEM: 6145,
    globalItems: { Each() {} },
    Hooks: {
        Once: (key, fn) => { if (!once.has(key)) { fn(); once.add(key); } },
        Overrides: (cls, base, key) => cls.prototype[key] !== base.prototype[key],
    },
    Safe: {
        Run: (name, fn) => { try { return fn(); } catch (error) { errors.push(name + ': ' + error.message); } },
        Report: (name, error) => errors.push(name + ': ' + error.message),
        Once: (key, message) => warnings.push(message),
    },
    ArmorSetLoader: { WantFrame: () => {} }, HitLoader: {},
    ModRegistry: { Find: () => null }, ModLoader: {},
};
const context = vm.createContext(sandbox);
const filesToLoad = ['TagCompound.js', 'StatModifier.js', 'ModItem.js', 'Loaders/ItemLoader.js', 'Loaders/ItemCombatHooks.js', 'Loaders/ItemUseHooks.js', 'Loaders/ItemHealingHooks.js', 'ModPlayer.js', 'ModNPC.js', 'Loaders/NPCLoader.js', 'Loaders/PlayerCombatHooks.js',
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
        args[2].value = !!found;
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
test('UpdateClient compares the previous snapshot before copying current state', () => {
    const clone = player(2), changes = [], order = [];
    const one = [...installed.values()].find(entry => entry.signature === 'object clientClone(Player clonePlayer)');
    const zero = [...installed.values()].find(entry => entry.signature === 'Player clientClone()');
    one.vanilla = (_, target) => target;
    zero.vanilla = self => { invoke(one, [self, clone]); return clone; };
    All.get(p).payload = { revision: 0, bag: { value: 0 } };
    All.prototype.CopyClientState = (self, target) => {
        const current = All.get(self).payload;
        target.payload = { revision: current.revision, bag: { ...current.bag } };
        order.push('copy');
    };
    All.prototype.SendClientChanges = (self, previous) => {
        const current = All.get(self).payload;
        assert.notEqual(previous, All.get(self));
        assert.notEqual(previous.payload.bag, current.bag);
        if (current.bag.value !== previous.payload.bag.value) changes.push([previous.payload.revision, current.revision]);
        order.push('changes');
    };
    vanilla('Terraria.Main', 'UpdateClient', () => order.push('vanilla'));
    Main.gameMenu = true; call('Terraria.Main', 'UpdateClient');
    Main.gameMenu = false; Main.netMode = 1; Main.myPlayer = 0;
    call('Terraria.Main', 'UpdateClient');
    All.get(p).payload.revision = 11; All.get(p).payload.bag.value = 111;
    call('Terraria.Main', 'UpdateClient');
    call('Terraria.Main', 'UpdateClient');
    All.get(p).payload.revision = 12; All.get(p).payload.bag.value = 122;
    call('Terraria.Main', 'UpdateClient');
    assert.deepEqual(changes, [[0, 11], [11, 12]]);
    assert.deepEqual(order.slice(-3), ['changes', 'vanilla', 'copy']);
});

test('client snapshots reset outside multiplayer and when the local player changes', () => {
    const clone = player(2), changes = [];
    const one = [...installed.values()].find(entry => entry.signature === 'object clientClone(Player clonePlayer)');
    const zero = [...installed.values()].find(entry => entry.signature === 'Player clientClone()');
    one.vanilla = (_, target) => target; zero.vanilla = () => clone;
    All.prototype.CopyClientState = (self, target) => { target.value = self.whoAmI; };
    All.prototype.SendClientChanges = (self, previous) => { assert.equal(previous.value, self.whoAmI); changes.push(self.whoAmI); };
    Main.netMode = 0; call('Terraria.Main', 'UpdateClient');
    Main.netMode = 1; Main.myPlayer = 0;
    call('Terraria.Main', 'UpdateClient'); call('Terraria.Main', 'UpdateClient');
    Main.myPlayer = 1;
    call('Terraria.Main', 'UpdateClient'); call('Terraria.Main', 'UpdateClient');
    Main.gameMenu = true; call('Terraria.Main', 'UpdateClient');
    Main.gameMenu = false;
    call('Terraria.Main', 'UpdateClient'); call('Terraria.Main', 'UpdateClient');
    assert.deepEqual(changes, [0, 1, 1]);
    Main.myPlayer = 0; Main.netMode = 0;
});

test('connection lifecycle preserves order and marks a reconnect as new', () => {
    const events = [], flags = [];
    vanilla('Terraria.NetMessage', 'SyncOnePlayer', () => events.push('sync-native'));
    vanilla('Terraria.Player.Hooks', 'PlayerConnect', () => events.push('connect-native'));
    vanilla('Terraria.Player.Hooks', 'PlayerDisconnect', () => events.push('disconnect-native'));
    All.prototype.SyncPlayer = (self, to, from, fresh) => { assert.equal(self, second); assert.equal(to, 2); assert.equal(from, 1); flags.push(fresh); events.push('sync-mod'); };
    All.prototype.PlayerConnect = self => { assert.equal(self, second); events.push('connect-mod'); };
    All.prototype.PlayerDisconnect = self => { assert.equal(self, second); events.push('disconnect-mod'); };
    call('Terraria.NetMessage', 'SyncOnePlayer', 1, 2, 1);
    call('Terraria.Player.Hooks', 'PlayerConnect', 1);
    call('Terraria.NetMessage', 'SyncOnePlayer', 1, 2, 1);
    call('Terraria.Player.Hooks', 'PlayerDisconnect', 1);
    call('Terraria.NetMessage', 'SyncOnePlayer', 1, 2, 1);
    assert.deepEqual(flags, [true, false, true]);
    assert.deepEqual(events.slice(0, 4), ['sync-native', 'sync-mod', 'connect-native', 'connect-mod']);
    assert.deepEqual(events.slice(6, 8), ['disconnect-mod', 'disconnect-native']);
});

test('server disconnect synchronization emits one callback across both native paths', () => {
    const events = [];
    All.prototype.PlayerDisconnect = self => events.push(self.whoAmI);
    vanilla('Terraria.NetMessage', 'SyncDisconnectedPlayer', index => call('Terraria.Player.Hooks', 'PlayerDisconnect', index));
    call('Terraria.Player.Hooks', 'PlayerConnect', 1);
    call('Terraria.NetMessage', 'SyncDisconnectedPlayer', 1);
    call('Terraria.NetMessage', 'SyncDisconnectedPlayer', 1);
    assert.deepEqual(events, [1]);
    call('Terraria.Player.Hooks', 'PlayerConnect', 1);
    call('Terraria.Player.Hooks', 'PlayerDisconnect', 1);
    assert.deepEqual(events, [1, 1]);
});

test('server sync tracks players without requiring the client-side connect callback', () => {
    const remote = player(4), events = [];
    remote.active = true;
    All.prototype.PlayerDisconnect = self => events.push(self.whoAmI);
    call('Terraria.NetMessage', 'SyncOnePlayer', 4, 2, -1);
    remote.active = false;
    call('Terraria.NetMessage', 'SyncDisconnectedPlayer', 4);
    call('Terraria.Player.Hooks', 'PlayerDisconnect', 4);
    assert.deepEqual(events, [4]);
    remote.active = true;
    call('Terraria.NetMessage', 'SyncOnePlayer', 4, 2, -1);
    call('Terraria.NetMessage', 'SyncDisconnectedPlayer', 4);
    assert.deepEqual(events, [4, 4]);
});

test('inactive remote players disconnect once when mobile bypasses native disconnect helpers', () => {
    const events = [];
    Main.netMode = 3; second.active = true;
    All.prototype.PlayerDisconnect = self => events.push(self.whoAmI);
    vanilla('Terraria.Player', 'Update', () => {});
    call('Terraria.Player.Hooks', 'PlayerConnect', 1);
    call('Terraria.Player', 'Update', p, 0);
    assert.deepEqual(events, []);
    second.active = false;
    call('Terraria.Player', 'Update', second, 1);
    assert.deepEqual(events, []);
    Main.gameMenu = true;
    call('Terraria.Player', 'Update', p, 0);
    assert.deepEqual(events, []);
    Main.gameMenu = false;
    call('Terraria.Player', 'Update', p, 0);
    call('Terraria.Player', 'Update', p, 0);
    call('Terraria.Player.Hooks', 'PlayerDisconnect', 1);
    assert.deepEqual(events, [1]);
    second.active = true;
    call('Terraria.Player.Hooks', 'PlayerConnect', 1);
    second.active = false;
    call('Terraria.Player', 'Update', p, 0);
    assert.deepEqual(events, [1, 1]);
    Main.netMode = 0; Main.gameMenu = false;
});

test('disconnect preserves the old player and mod state when the native slot is replaced', () => {
    const previous = player(5), events = [];
    previous.active = true; All.get(previous).marker = 123;
    call('Terraria.Player.Hooks', 'PlayerConnect', 5);
    const replacement = player(5);
    replacement.active = false; replacement.whoAmI = 0;
    All.prototype.PlayerDisconnect = self => {
        assert.equal(self, previous); assert.equal(self.whoAmI, 5);
        events.push(All.get(self).marker);
    };
    Main.netMode = 3;
    call('Terraria.Player', 'Update', p, 0);
    call('Terraria.Player.Hooks', 'PlayerDisconnect', 5);
    assert.deepEqual(events, [123]);
    assert.equal(replacement.ModPlayers, undefined);
    Main.netMode = 0;
});

test('client clone reentrancy guard recovers after native failures', () => {
    const clone = player(2); let copies = 0;
    const zero = [...installed.values()].find(entry => entry.signature === 'Player clientClone()');
    All.prototype.CopyClientState = () => { copies++; };
    zero.vanilla = () => { throw Error('native clone failure'); };
    assert.throws(() => invoke(zero, [p]), /native clone failure/);
    zero.vanilla = () => clone;
    assert.equal(invoke(zero, [p]), clone);
    assert.equal(copies, 1);
});

test('ConsumableDodge consumes only for the local player and dodgeable hits', () => {
    const owners = [], source = { _sourceNPCIndex: -1, _sourceProjectileLocalIndex: -1 };
    Main.myPlayer = 0;
    All.prototype.ConsumableDodge = self => { owners.push(self.whoAmI); return true; };
    vanilla('Terraria.Player', 'Hurt', () => 1);
    const hurt = (self, dodgeable) => call('Terraria.Player', 'Hurt', self, source, 1, 0, false, true, false, -1, dodgeable);
    assert.equal(hurt(p, true), 0);
    assert.equal(hurt(second, true), 1);
    assert.equal(hurt(p, false), 1);
    assert.deepEqual(owners, [0]);
});

test('failed mana payments do not emit OnConsumeMana', () => {
    let missing = 0, consumed = 0;
    p.statMana = 0; p.manaCost = 1; p.slowMagicUse = false;
    All.prototype.OnMissingMana = () => { missing++; };
    All.prototype.OnConsumeMana = () => { consumed++; };
    vanilla('Terraria.Player', 'CheckMana', () => false);
    assert.equal(call('Terraria.Player', 'CheckMana', p, 20, true, true), false);
    assert.equal(missing, 0);
    assert.equal(consumed, 0);
    assert.equal(call('Terraria.Player', 'CheckMana', p, 20, true, false), false);
    assert.equal(missing, 1);
    assert.equal(consumed, 0);
});

function selective(methods, run) {
    const lengths = new Map([...installed.values()].map(entry => [entry, entry.callbacks.length]));
    const installedKeys = new Set(); let refs = 0;
    const fresh = vm.createContext({ ...sandbox, Ref: class { constructor(value) { this.value = value; refs++; } },
        Hooks: { ...sandbox.Hooks, Once: (key, install) => { if (!installedKeys.has(key)) { installedKeys.add(key); install(); } } } });
    try {
        for (const file of filesToLoad) vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), fresh);
        vm.runInContext('class Selective extends ModPlayer {' + methods + '} ModPlayer.register(Selective);', fresh);
        run({ installedKeys, refs: () => refs, hook: (owner, name) => method(owner, name).callbacks.at(-1) });
    } finally {
        for (const [entry, length] of lengths) { entry.callbacks.length = length; entry.filters.length = length; }
    }
}

test('a cleared-jump observer does not install movement or global sound hooks', () => {
    selective('OnExtraJumpCleared() {}', ({ installedKeys }) => {
        assert.equal(installedKeys.has('player.ClearJumps'), true);
        assert.equal(installedKeys.has('player.ExtraJumps'), false);
        assert.equal(installedKeys.has('player.ExtraJumpSounds'), false);
    });
});

test('jump veto without input avoids reading jump states and allocating references', () => {
    selective('CanStartExtraJump() { return true; }', ({ installedKeys, refs, hook }) => {
        assert.equal(installedKeys.has('player.ExtraJumpSounds'), false);
        const player = { controlJump: false };
        Object.defineProperty(player, 'isPerformingJump_Cloud', { get() { throw Error('unexpected state read'); } });
        let calls = 0;
        hook('Terraria.Player', 'JumpMovement')(() => calls++, player);
        assert.equal(calls, 1);
        assert.equal(refs(), 0);
    });
});

test('draw-info observer avoids unused DrawEffects references and combat avoids unused collision hooks', () => {
    selective('ModifyDrawInfo(player, info) { info.marker = 1; } ModifyHitNPCWithItem() {}', ({ installedKeys, refs, hook }) => {
        const player = { __address: address++ }, info = {};
        hook('Terraria.DataStructures.PlayerDrawSet', 'BoringSetup')(() => {}, info, player, {}, 0, 0, {}, null);
        assert.equal(info.marker, 1);
        assert.equal(refs(), 0);
        assert.equal(installedKeys.has('player.MeleeCollision'), false);
    });
});

test('dispatch continues after failures and preserves decisions, order and return values', () => {
    const events = [], before = errors.length;
    class Failure extends ModPlayer {
        CanHitPvp() { events.push('failure'); throw Error('dispatch failure'); }
        ModifyZoom() { throw Error('zoom failure'); }
    }
    class Denied extends ModPlayer { CanHitPvp() { events.push('denied'); return false; } }
    class Accepted extends ModPlayer { CanHitPvp() { events.push('accepted'); return true; } }
    PlayerLoader.Add(Failure); PlayerLoader.Add(Denied); PlayerLoader.Add(Accepted);
    All.prototype.CanHitPvp = () => { events.push('first'); return true; };
    assert.equal(PlayerLoader.Veto(p, 'CanHitPvp'), true);
    assert.deepEqual(events, ['first', 'failure', 'denied', 'accepted']);
    assert.equal(errors.length, before + 1);
    PlayerLoader.Call(p, 'ModifyZoom', { value: 1 });
    assert.equal(errors.length, before + 2);
    errors.splice(before);
});

test('empty dispatch does not initialize players and late registration invalidates cached methods', () => {
    const fresh = player(6);
    assert.equal(PlayerLoader.First(fresh, 'Unimplemented'), false);
    assert.equal(PlayerLoader.Veto(fresh, 'Unimplemented'), false);
    assert.equal(PlayerLoader.Nullable(fresh, 'Unimplemented'), null);
    assert.equal(PlayerLoader.Factor(fresh, 'Unimplemented', {}), 1);
    PlayerLoader.Call(fresh, 'Unimplemented');
    assert.equal(fresh.ModPlayers, undefined);
    assert.equal(PlayerLoader.Has('LateMethod'), false);
    class Late extends ModPlayer { LateMethod(self, value) { self.marker = value; } }
    PlayerLoader.Add(Late);
    assert.equal(PlayerLoader.Has('LateMethod'), true);
    PlayerLoader.Call(fresh, 'LateMethod', 42);
    assert.equal(fresh.marker, 42);
});

test('collision and sound callbacks stay outside JS when their native gates are inactive', () => {
    for (const entry of [method('Microsoft.Xna.Framework.Rectangle', 'Intersects'),
        [...installed.values()].find(row => row.name === 'Terraria.Audio.SoundEngine' && row.signature.startsWith('SoundEffectInstance PlaySound(int'))]) {
        const previous = entry.callbacks[0]; let calls = 0;
        entry.callbacks[0] = (...args) => { calls++; return previous(...args); };
        try { invoke(entry, [{}]); assert.equal(calls, 0); }
        finally { entry.callbacks[0] = previous; }
    }
});

test('attack gates recover after native failures', () => {
    vanilla('Terraria.Player', 'ProcessHitAgainstNPC', () => { assert.equal(flags.get('player.Attack'), true); throw Error('attack failure'); });
    assert.throws(() => call('Terraria.Player', 'ProcessHitAgainstNPC', p, item(1), npc.Hitbox, 20, 0, 0), /attack failure/);
    assert.equal(flags.get('player.Attack'), false);
});

test('jump veto restores availability after failure and sound resumes outside jumping', () => {
    p.controlJump = p.releaseJump = true; p.canJumpAgain_Cloud = true; p.isPerformingJump_Cloud = false;
    All.prototype.CanStartExtraJump = () => false;
    vanilla('Terraria.Player', 'JumpMovement', () => { assert.equal(p.canJumpAgain_Cloud, false); throw Error('jump failure'); });
    assert.throws(() => call('Terraria.Player', 'JumpMovement', p), /jump failure/);
    assert.equal(p.canJumpAgain_Cloud, true);
    const sound = [...installed.values()].find(row => row.name === 'Terraria.Audio.SoundEngine' && row.signature.startsWith('SoundEffectInstance PlaySound(int'));
    let played = 0; sound.vanilla = () => { played++; };
    invoke(sound, [1, 0, 0, 1, 1, 0]);
    assert.equal(played, 1);
});

test('unchanged draw layers avoid native cache reads and JS layer dispatch', () => {
    const info = { __address: address++, drawPlayer: p };
    Object.defineProperty(info, 'DrawDataCacheCount', { get: () => { throw Error('unexpected cache read'); } });
    All.prototype.HideDrawLayers = () => {};
    All.prototype.ModifyDrawLayerOrdering = () => {};
    vanilla('Terraria.DataStructures.PlayerDrawLayers', PlayerDrawLayers.Skin.Method, () => {});
    const layer = method('Terraria.DataStructures.PlayerDrawLayers', PlayerDrawLayers.Skin.Method);
    const previous = layer.callbacks[0]; let calls = 0;
    layer.callbacks[0] = (...args) => { calls++; return previous(...args); };
    vanilla('Terraria.Graphics.Renderers.LegacyPlayerRenderer', 'DrawPlayer_UseNormalLayers', draw => invoke(layer, [draw]));
    try { call('Terraria.Graphics.Renderers.LegacyPlayerRenderer', 'DrawPlayer_UseNormalLayers', info); }
    finally { layer.callbacks[0] = previous; }
    assert.equal(calls, 0);
    assert.equal(flags.get('player.DrawLayers'), false);
});

test('hidden layers restore visibility and filters after renderer failures', () => {
    All.prototype.HideDrawLayers = () => PlayerDrawLayers.Torso.Hide();
    vanilla('Terraria.Graphics.Renderers.LegacyPlayerRenderer', 'DrawPlayer_UseNormalLayers', () => {
        assert.equal(flags.get('player.DrawLayers'), true); throw Error('render failure');
    });
    assert.throws(() => call('Terraria.Graphics.Renderers.LegacyPlayerRenderer', 'DrawPlayer_UseNormalLayers', { __address: address++, drawPlayer: p }), /render failure/);
    assert.equal(PlayerDrawLayers.Torso.IsHidden, false);
    assert.equal(flags.get('player.DrawLayers'), false);
});

assert.equal(errors.length, 0);
console.log(`${checks} behavior checks passed; ${[...installed.values()].filter((entry) => entry.callbacks.length).length} native signatures verified; ${stages.size} native stages registered.`);
