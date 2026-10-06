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
const installed = new Map(), natives = new Map(), marks = new Map(), flags = new Map(), errors = [], missing = [], ready = [];
let address = 1;
function native(name) {
    if (natives.has(name)) return natives.get(name);
    const target = {};
    const proxy = new Proxy(target, { get(fields, key) {
        if (key in fields) return fields[key];
        if (key === 'new') return () => entity(0);
        if (typeof key !== 'string') return undefined;
        if (!key.includes('(')) return native(name + '.' + key);
        const id = name + ':' + signature(key);
        if (!installed.has(id)) installed.set(id, { owner: name, key, hooks: [], active: 0, vanilla() {} });
        const entry = installed.get(id);
        const fn = (...args) => entry.active ? entry.vanilla(...args) : invoke(entry, args);
        fn.entry = entry;
        fn.hook = (callback, filter = {}) => {
            if (filter.marks && filter.minType === undefined && filter.arg === undefined && filter.tile === undefined && filter.tileAt === undefined) missing.push(name + ':' + key + ': missing native type reader');
            if (!methods.get(name)?.has(signature(key))) missing.push(name + ':' + key);
            const expected = parameterNames.get(id);
            if (expected && JSON.stringify(expected) !== JSON.stringify(names(key))) missing.push(name + ':' + key + ': parameter names');
            entry.hooks.push({ callback, filter });
        };
        return fn;
    } });
    natives.set(name, proxy);
    return proxy;
}
function invoke(entry, args, index = 0) {
    if (index >= entry.hooks.length) return entry.vanilla(...args);
    const { callback, filter } = entry.hooks[index];
    const original = (...next) => invoke(entry, next.length ? next : args, index + 1);
    const selected = args[filter.on >= 0 ? filter.on + 1 : 0];
    const type = filter.arg >= 0 ? args[filter.arg] : selected?.[filter.field || 'type'];
    if (filter.marks && !marks.get(filter.marks)?.has(type) || filter.minType && type < filter.minType ||
        filter.flag && !flags.get(filter.flag) || filter.whileIn && !filter.whileIn.entry.active) return original();
    entry.active++;
    try { return callback(original, ...args); }
    finally { entry.active--; }
}
function method(owner, name) {
    const matches = [...installed.values()].filter(e => e.owner === owner && e.key.split('(')[0].endsWith(' ' + name));
    assert.equal(matches.length, 1, owner + '.' + name);
    return matches[0];
}
function call(owner, name, ...args) { return invoke(method(owner, name), args); }
function vanilla(owner, name, fn) { method(owner, name).vanilla = fn; }
const vector = (X = 0, Y = 0) => ({ X, Y });
const color = (R = 20, G = 30, B = 40, A = 255) => ({ R, G, B, A, get PackedValue() { return this.R + this.G * 256 + this.B * 65536 + this.A * 16777216; } });
const Terraria = native('Terraria'), Microsoft = native('Microsoft'), Main = Terraria.Main;
Main.npc = []; Main.projectile = []; Main.player = []; Main.myPlayer = 0; Main.netMode = 0;
let talking = null;
const sandbox = {
    Terraria, Microsoft, assert, state: {}, events: [], SceneEffectPriority: { BossLow: 0 }, FIRST_NPC: 697,
    Ref: class { constructor(value) { this.value = value; } },
    Vector2: { new: vector }, Color: { new: color },
    Entities: { InstanceOf: npc => npc.ModNPC, Define() {} },
    ModNet: { InstallEntity() {} }, Ready: { Add: f => ready.push(f) }, SpawnLoader: { InstallPool() {} },
    TownNPCLoader: { Hook() {}, TalkingTo: () => talking },
    Safe: { Run(label, fn) { try { return fn(); } catch (e) { errors.push([label, e]); } }, Report(label, e) { errors.push([label, e]); } },
    bl: { addressOf: p => p?.__address, mod: { uuid: 'npc-hooks' }, log() {},
        hookFlags: { set: (k, v) => flags.set(k, v) },
        hookMarks: { set(k, t) { if (!marks.has(k)) marks.set(k, new Set()); marks.get(k).add(t); } } },
};
const context = vm.createContext(sandbox);
for (const file of ['Core/Hooks.js', 'StatModifier.js', 'ModPlayer.js', 'ModNPC.js', 'Loaders/PlayerCombatHooks.js',
    'Loaders/PlayerLoader.js', 'Loaders/NPCLoader.js', 'NPCShop.js']) {
    vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
}
vm.runInContext(`
const record = (name, ...args) => { events.push(name); const r = state[name]; return typeof r === 'function' ? r(...args) : r; };
class Local extends ModNPC {}
const names = ['OnSpawn', 'ResetEffects', 'OnHitByItem', 'OnHitByProjectile', 'CanHitPlayer', 'ModifyHitPlayer',
    'OnHitPlayer', 'CanBeHitByItem', 'ModifyIncomingHit', 'CheckDead', 'ApplyDifficultyAndPlayerScaling', 'CanChat', 'ModifyActiveShop', 'ModifyNPCHappiness', 'PreDraw',
    'PostDraw', 'DrawEffects', 'DrawBehind', 'GetAlpha', 'BossHeadSlot', 'BossHeadRotation', 'BossHeadSpriteEffects'];
for (const name of names) Local.prototype[name] = function (...args) { return record(name, ...args); };
NPCLoader.ByType.set(697, new Local()); NPCLoader.Hook(Local, 697);
class Plain extends ModNPC {}
NPCLoader.ByType.set(698, new Plain()); NPCLoader.Hook(Plain, 698);
class Late extends Local {}
NPCLoader.ByType.set(699, new Late()); NPCLoader.Hook(Late, 699);
class PlayerMod extends ModPlayer {
    CanHitNPC() { return record('player.CanHitNPC'); }
    CanHitNPCWithItem() { return record('player.CanHitNPCWithItem'); }
    CanBeHitByNPC() { return record('player.CanBeHitByNPC'); }
    ModifyHitByNPC(...args) { return record('player.ModifyHitByNPC', ...args); }
    OnHitByNPC(...args) { return record('player.OnHitByNPC', ...args); }
    OnHitNPC(...args) { return record('player.OnHitNPC', ...args); }
}
PlayerLoader.Add(PlayerMod);
globalThis.api = { Local, Plain, Late, NPCLoader, NPCDrawHooks, PlayerLoader, NPCShop };
`, context);
assert.deepEqual(missing, [], 'Native signatures and parameter names must match the mobile dump');
const { Local, Plain, Late, NPCLoader, NPCDrawHooks, NPCShop } = sandbox.api;
const npc = (type = 697, cls = Local, index = 0) => {
    const n = { __address: address++, type, active: true, life: 100, whoAmI: index, Center: vector(32, 48),
        Hitbox: { X: 0, Y: 0, Width: 10, Height: 10 }, ModNPC: cls ? new cls() : null };
    Main.npc[index] = n;
    return n;
};
const n = npc(), other = npc(697, Local, 1), plain = npc(698, Plain, 2), vanillaNPC = npc(1, null, 3), late = npc(699, Late, 4);
const player = { __address: address++, whoAmI: 0, dead: false, statLife: 100 }; Main.player[0] = player;
const item = { __address: address++, type: 1 }, projectile = { __address: address++, owner: 0 };
const events = sandbox.events, state = sandbox.state;
let checks = 0;
function test(name, fn) {
    events.length = 0; for (const key of Object.keys(state)) delete state[key];
    const before = errors.length;
    fn(); assert.equal(errors.length, before, name + ': unexpected callback error');
    checks++; console.log(name + ': ok');
}
const callNPC = (name, ...args) => call('Terraria.NPC', name, n, ...args);
test('OnSpawn calls native once before local and passes source', () => {
    const src = {};
    vanilla('Terraria.NPC', 'NewNPC', () => { events.push('native'); return n.whoAmI; });
    state.OnSpawn = (...args) => { assert.equal(args.length, 2); assert.equal(args[1], src); };
    call('Terraria.NPC', 'NewNPC', src, 0, 0, n.type, 0, 0, 0, 0, 0, 255); assert.deepEqual(events, ['native', 'OnSpawn']);
});
test('ResetEffects runs after flags reset and does not share entity state', () => {
    vanilla('Terraria.NPC', 'UpdateNPC_BuffSetFlags', () => events.push('native'));
    state.ResetEffects = target => { assert.equal(target.lifeRegen, 0); target.ModNPC.count = (target.ModNPC.count || 0) + 1; };
    n.lifeRegen = other.lifeRegen = 0;
    callNPC('UpdateNPC_BuffSetFlags', true); call('Terraria.NPC', 'UpdateNPC_BuffSetFlags', other, true);
    assert.equal(n.ModNPC.count, 1); assert.equal(other.ModNPC.count, 1);
});
test('native filters exclude vanilla and a mod without overrides', () => {
    call('Terraria.NPC', 'NewNPC', {}, 0, 0, vanillaNPC.type, 0, 0, 0, 0, 0, 255); call('Terraria.NPC', 'NewNPC', {}, 0, 0, plain.type, 0, 0, 0, 0, 0, 255);
    assert.deepEqual(events, ['native', 'native']);
});
test('late inherited overrides receive native marks without duplicate hooks', () => {
    call('Terraria.NPC', 'NewNPC', {}, 0, 0, late.type, 0, 0, 0, 0, 0, 255); assert.deepEqual(events, ['native', 'OnSpawn']);
    assert.equal(method('Terraria.NPC', 'NewNPC').hooks.length, 1);
});
test('invalid spawn slot and inactive result do not notify', () => {
    vanilla('Terraria.NPC', 'NewNPC', () => 200);
    assert.equal(call('Terraria.NPC', 'NewNPC', {}, 0, 0, n.type, 0, 0, 0, 0, 0, 255), 200);
    n.active = false; vanilla('Terraria.NPC', 'NewNPC', () => n.whoAmI);
    call('Terraria.NPC', 'NewNPC', {}, 0, 0, n.type, 0, 0, 0, 0, 0, 255); n.active = true;
    assert.deepEqual(events, []);
});
test('ResetEffects changes are visible when native buffs are applied', () => {
    n.lifeRegen = 0;
    state.ResetEffects = target => { target.lifeRegen = 5; };
    vanilla('Terraria.NPC', 'UpdateNPC_BuffSetFlags', target => { assert.equal(target.lifeRegen, 5); events.push('buffs'); });
    callNPC('UpdateNPC_BuffSetFlags', true); assert.deepEqual(events, ['ResetEffects', 'buffs']);
    vanilla('Terraria.NPC', 'UpdateNPC_BuffSetFlags', () => {});
});
test('CheckDead veto preserves active NPC and supports restoring life', () => {
    vanilla('Terraria.NPC', 'checkDead', target => { events.push('native'); target.active = false; });
    n.life = 0; state.CheckDead = target => { target.life = 20; return false; };
    callNPC('checkDead'); assert.equal(n.active, true); assert.equal(n.life, 20); assert.deepEqual(events, ['CheckDead']);
});
test('CheckDead does not run for living or inactive NPCs', () => {
    n.life = 100; callNPC('checkDead'); assert.deepEqual(events, ['native']);
    n.life = 0; callNPC('checkDead'); assert.deepEqual(events, ['native', 'native']); n.active = true;
});
test('CheckDead undefined permits native death', () => {
    n.life = 0; callNPC('checkDead'); assert.equal(n.active, false); assert.deepEqual(events, ['CheckDead', 'native']); n.active = true; n.life = 100;
});
for (const name of ['get_CanTalk', 'get_CanBeTalkedTo']) {
    vanilla('Terraria.NPC', name, () => false);
    for (const value of [true, false, undefined]) test(name + ' decision ' + value, () => {
        state.CanChat = value; assert.equal(callNPC(name), value === true);
    });
}
vanilla('Terraria.NPC', 'StrikeNPC_Inner', (_, damage, kb, direction, crit) => { events.push('strike'); state.args = [damage, kb, direction, crit]; return damage; });
vanilla('Terraria.NPC', 'StrikeNPC', (...args) => call('Terraria.NPC', 'StrikeNPC_Inner', ...args));
test('incoming modifiers change damage, knockback, direction and crit', () => {
    state.ModifyIncomingHit = (_, mod) => { mod.SourceDamage.Multiplicative = 2; mod.Knockback.Flat = 3; mod.hitDirection = -1; mod.SetCrit(); };
    assert.equal(callNPC('StrikeNPC_Inner', 10, 2, 1, false, false, 0), 20); assert.deepEqual(state.args, [20, 5, -1, true]);
});
test('network strike does not apply incoming modifiers again', () => {
    callNPC('StrikeNPC_Inner', 10, 2, 1, false, true, 0); assert.deepEqual(events, ['strike']);
});
test('incoming clamps negative damage and knockback and supports DisableCrit', () => {
    state.ModifyIncomingHit = (_, mod) => { mod.damage = -7; mod.knockBack = -3; mod.DisableCrit(); };
    assert.equal(callNPC('StrikeNPC_Inner', 10, 2, 1, true, false, 0), 0); assert.deepEqual(state.args, [0, 0, 1, false]);
});
vanilla('Terraria.Player', 'CanNPCBeHitByPlayerOrPlayerProjectile', () => false);
vanilla('Terraria.Player', 'ProcessHitAgainstNPC', (p, weapon, rect, damage, kb, index) => {
    const target = Main.npc[index];
    if (call('Terraria.Player', 'CanNPCBeHitByPlayerOrPlayerProjectile', p, target, null)) call('Terraria.NPC', 'StrikeNPC', target, damage, kb, 1, false, false, 0);
});
const itemHit = () => call('Terraria.Player', 'ProcessHitAgainstNPC', player, item, n.Hitbox, 10, 2, 0);
test('CanBeHitByItem false vetoes attack before damage', () => {
    state.CanBeHitByItem = false; itemHit(); assert.equal(events.includes('strike'), false); assert.equal(events.filter(e => e === 'CanBeHitByItem').length, 1);
});
test('CanBeHitByItem null preserves native eligibility', () => {
    state.CanBeHitByItem = null; itemHit(); assert.equal(events.includes('strike'), false);
});
test('NPC item permission bypasses native false and notification reflects incoming changes', () => {
    state.CanBeHitByItem = true;
    state.ModifyIncomingHit = (_, mod) => { mod.damage = 17; mod.hitDirection = -1; mod.SetCrit(); };
    state.OnHitByItem = (...args) => { assert.equal(args.length, 5); assert.equal(args[1], player); assert.equal(args[2], item); assert.equal(args[3].Crit, true); assert.equal(args[3].SourceDamage, 17); assert.equal(args[4], 17); };
    itemHit(); assert.ok(events.indexOf('OnHitByItem') < events.indexOf('player.OnHitNPC'));
});
test('player item veto beats NPC permission', () => {
    state.CanBeHitByItem = true; state['player.CanHitNPCWithItem'] = false;
    itemHit(); assert.equal(events.includes('strike'), false);
});
vanilla('Terraria.Projectile', 'Damage_PVE', p => call('Terraria.NPC', 'StrikeNPC', n, 12, 2, 1, false, false, p.owner));
test('projectile notification receives real target, projectile and damage', () => {
    state.OnHitByProjectile = (...args) => { assert.equal(args.length, 4); assert.equal(args[0], n); assert.equal(args[1], projectile); assert.equal(args[2].Damage, 12); assert.equal(args[3], 12); };
    call('Terraria.Projectile', 'Damage_PVE', projectile, n.Hitbox, 1); assert.equal(events.filter(e => e === 'OnHitByProjectile').length, 1);
});
test('projectile without player owner still notifies ModNPC without ModPlayer callbacks', () => {
    projectile.owner = 255; call('Terraria.Projectile', 'Damage_PVE', projectile, n.Hitbox, 1); projectile.owner = 0;
    assert.ok(events.includes('OnHitByProjectile')); assert.ok(!events.includes('player.OnHitNPC'));
});
test('failed strike suppresses hit notifications', () => {
    state.ModifyIncomingHit = (_, mod) => { mod.damage = 0; }; call('Terraria.Projectile', 'Damage_PVE', projectile, n.Hitbox, 1);
    assert.ok(!events.includes('OnHitByProjectile'));
});
test('nested projectile attacks restore outer source and hit information', () => {
    const nested = { __address: address++, owner: 255 }, sources = [];
    state.OnHitByProjectile = (_, p) => { sources.push(p); if (p === projectile) call('Terraria.Projectile', 'Damage_PVE', nested, n.Hitbox, 1); };
    call('Terraria.Projectile', 'Damage_PVE', projectile, n.Hitbox, 1); assert.deepEqual(sources, [projectile, nested]); assert.equal(flags.get('player.Attack'), false);
});
test('projectile attack context restores after native exception', () => {
    vanilla('Terraria.Projectile', 'Damage_PVE', () => { throw Error('native'); });
    assert.throws(() => call('Terraria.Projectile', 'Damage_PVE', projectile, n.Hitbox, 1), /native/);
    assert.equal(flags.get('player.Attack'), false);
    callNPC('StrikeNPC', 12, 2, 1, false, false, 0); assert.ok(!events.includes('OnHitByProjectile'));
    vanilla('Terraria.Projectile', 'Damage_PVE', p => call('Terraria.NPC', 'StrikeNPC', n, 12, 2, 1, false, false, p.owner));
});
vanilla('Terraria.Player', 'Hurt', (_, src, damage, direction, pvp, quiet, crit, slot) => { events.push('hurt'); state.hurt = [damage, slot]; return damage; });
const sourceNPC = { _sourceNPCIndex: 0, _sourceProjectileLocalIndex: -1 };
const hurt = () => call('Terraria.Player', 'Hurt', player, sourceNPC, 10, 1, false, true, false, -1, false);
test('CanHitPlayer veto prevents damage and notification', () => {
    state.CanHitPlayer = false; assert.equal(hurt(), 0); assert.deepEqual(events, ['CanHitPlayer']);
});
test('player contact veto beats NPC permission', () => {
    state.CanHitPlayer = true; state['player.CanBeHitByNPC'] = false; assert.equal(hurt(), 0); assert.ok(!events.includes('hurt'));
});
test('contact refs and damage modifiers reach native Hurt and notifications', () => {
    state.CanHitPlayer = (_, __, slot) => { slot.value = 1; return true; };
    state.ModifyHitPlayer = (_, __, mod) => { mod.damage = 7; };
    state['player.ModifyHitByNPC'] = (_, __, mod) => { mod.damage += 2; };
    state.OnHitPlayer = (_, p, info) => { assert.equal(p, player); assert.equal(info.Damage, 9); assert.equal(info.CooldownCounter, 1); };
    assert.equal(hurt(), 9); assert.deepEqual(state.hurt, [9, 1]); assert.ok(events.indexOf('OnHitPlayer') < events.indexOf('player.OnHitByNPC'));
});
test('negative contact damage is clamped before native Hurt and does not notify', () => {
    state.ModifyHitPlayer = (_, __, mod) => { mod.damage = -3; };
    assert.equal(hurt(), 0); assert.equal(state.hurt[0], 0); assert.ok(!events.includes('OnHitPlayer'));
});
Terraria.Lighting['Color GetColor(int x, int y)']; vanilla('Terraria.Lighting', 'GetColor', () => color());
vanilla('Terraria.NPC', 'GetNPCColorTintedByBuffs', (_, c) => { state.tint = c; return c; });
const draws = [...installed.values()].filter(e => e.owner === 'Terraria.Main' && e.key.includes(' DrawNPCDirect('));
for (const entry of draws) entry.vanilla = (_, batch, target) => { events.push('draw'); call('Terraria.NPC', 'GetNPCColorTintedByBuffs', target, color(99)); };
const draw = (entry = draws[0], target = n) => invoke(entry, [{}, {}, target, false, vector(), {}, { value: {} }].slice(0, entry.key.includes('LightMap') ? 7 : 5));
for (const entry of draws) test('draw veto still calls PostDraw for ' + entry.key, () => {
    state.PreDraw = false; draw(entry); assert.deepEqual(events, ['DrawEffects', 'PreDraw', 'PostDraw']);
});
test('draw effects ref reaches pre, native tint and post', () => {
    state.DrawEffects = (_, ref) => { ref.value = color(77); };
    state.PreDraw = (_, batch, screen, c) => { assert.equal(c.R, 77); return true; };
    state.PostDraw = (_, batch, screen, c) => assert.equal(c.R, 77);
    draw(draws[1]); assert.equal(state.tint.R, 77); assert.equal(flags.get('npc.DrawColor'), false);
});
test('unchanged draw effects preserve native lighting', () => {
    draw(); assert.equal(state.tint.R, 99);
});
test('draw context restores after native exception', () => {
    state.DrawEffects = (_, ref) => { ref.value = color(77); };
    const nativeDraw = draws[0].vanilla; draws[0].vanilla = () => { throw Error('native'); };
    assert.throws(() => draw(), /native/); draws[0].vanilla = nativeDraw; assert.equal(flags.get('npc.DrawColor'), false);
    assert.equal(callNPC('GetNPCColorTintedByBuffs', color(55)).R, 55);
});
test('GetAlpha permits fully transparent override and nullable fallback', () => {
    vanilla('Terraria.NPC', 'GetAlpha', (_, c) => c);
    state.GetAlpha = color(5, 6, 7, 0); assert.equal(callNPC('GetAlpha', color()).A, 0);
    state.GetAlpha = null; assert.equal(callNPC('GetAlpha', color(33)).R, 33);
});
for (const [callback, name, value] of [['BossHeadSlot', 'GetBossHeadTextureIndex', -1], ['BossHeadRotation', 'GetBossHeadRotation', 0.75], ['BossHeadSpriteEffects', 'GetBossHeadSpriteEffects', 1]]) {
    test(callback + ' mutable ref starts with native value', () => {
        vanilla('Terraria.NPC', name, () => 3); state[callback] = (_, ref) => { assert.equal(ref.value, 3); ref.value = value; };
        assert.equal(callNPC(name), value);
    });
}
test('DrawBehind only scans tracked active overriders and removes stale entries', () => {
    vanilla('Terraria.Main', 'CacheNPCDraws', () => events.push('cache'));
    NPCDrawHooks.Track(n); NPCDrawHooks.Track(other); NPCDrawHooks.Track(plain); other.active = false;
    call('Terraria.Main', 'CacheNPCDraws', {}); assert.deepEqual(events, ['cache', 'DrawBehind']); other.active = true;
});
test('shop changes run after native fill and use the talking NPC', () => {
    const shop = new NPCShop(697, 'Probe').Add(8).Register();
    const items = Array.from({ length: 4 }, () => ({ type: 0, ['void SetDefaults(int Type, ItemVariant variant)'](t) { this.type = t; } }));
    talking = { npc: n, m: n.ModNPC };
    state.ModifyActiveShop = (target, name, contents) => { assert.equal(target, n); assert.equal(name, 'Probe'); assert.equal(contents[0].type, 8); contents[0].shopCustomPrice = 123; };
    call('Terraria.InventoryStorage', 'SetupShop', { item: items }, shop.Index); assert.equal(items[0].shopCustomPrice, 123);
    talking = { npc: other, m: other.ModNPC }; other.type = 698; events.length = 0;
    call('Terraria.InventoryStorage', 'SetupShop', { item: items }, shop.Index); assert.deepEqual(events, []); other.type = 697; talking = null;
});
test('callback failures are reported and native default continues', () => {
    const count = errors.length; state.CanChat = () => { throw Error('callback'); };
    assert.equal(callNPC('get_CanTalk'), false); assert.equal(errors.length, count + 1); errors.pop();
});
test('scaling uses the actual player count, native balance and per NPC difficulty', () => {
    Terraria.DataStructures.GameDifficultyLevel.Master = 3;
    Terraria.NPC['void GetStatScalingFactors(int numPlayers, out float balance, out float boost)'];
    vanilla('Terraria.NPC', 'ScaleStats_ByPlayerCount', (target, count) => { events.push('scaled'); target.lifeMax = count * 100; });
    vanilla('Terraria.NPC', 'GetStatScalingFactors', (count, balance, boost) => { balance.value = 1.5; boost.value = 2; assert.equal(count, 4); });
    n.difficulty = 3;
    state.ApplyDifficultyAndPlayerScaling = (target, count, balance, adjustment) => { assert.equal(target.lifeMax, 400); assert.equal(count, 4); assert.equal(balance, 1.5); assert.equal(adjustment, .85); };
    callNPC('ScaleStats_ByPlayerCount', 4); assert.deepEqual(events, ['scaled', 'ApplyDifficultyAndPlayerScaling']);
    n.difficulty = 2; state.ApplyDifficultyAndPlayerScaling = (_, __, ___, adjustment) => assert.equal(adjustment, 1); callNPC('ScaleStats_ByPlayerCount', 4);
});
test('happiness receives native helper info after personality preferences', () => {
    const info = { npc: n, player: { ZoneSnow: true }, nearbyNPCsByType: [false, true] }, helper = { _currentPriceAdjustment: 1 };
    vanilla('Terraria.GameContent.Personalities.AllPersonalitiesModifier', 'ModifyShopPrice', (_, value, h) => { events.push('preferences'); h._currentPriceAdjustment = .9; });
    state.ModifyNPCHappiness = (...args) => { assert.equal(args.length, 5); assert.equal(args[0], n); assert.equal(args[1], info.player); assert.equal(args[2], 2); assert.equal(args[3]._currentPriceAdjustment, .9); assert.equal(args[4], info.nearbyNPCsByType); args[3]._currentPriceAdjustment = .8; };
    call('Terraria.GameContent.Personalities.AllPersonalitiesModifier', 'ModifyShopPrice', {}, info, helper);
    assert.equal(helper._currentPriceAdjustment, .8); assert.deepEqual(events, ['preferences', 'ModifyNPCHappiness']);
});
test('happiness skips vanilla and mod types without an override', () => {
    for (const target of [vanillaNPC, plain]) call('Terraria.GameContent.Personalities.AllPersonalitiesModifier', 'ModifyShopPrice', {}, { npc: target }, {});
    assert.deepEqual(events, ['preferences', 'preferences']);
});
assert.deepEqual(missing, []);
assert.deepEqual(errors, []);
console.log(checks + ' behavior checks passed; ' + [...installed.values()].filter(e => e.hooks.length).length + ' native signatures verified.');
