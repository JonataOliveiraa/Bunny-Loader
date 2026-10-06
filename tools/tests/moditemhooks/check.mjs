import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { runUseCases } from './use-cases.mjs';
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
        if (!installed.has(id)) installed.set(id, { owner: name, key, hooks: [], entries: 0, active: 0, vanilla() {} });
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
    const indexed = filter.field?.match(/^(\w+)\[([\w.]+)\]\.(\w+)$/);
    const type = filter.arg >= 0 ? args[filter.arg] : indexed ? selected?.[indexed[1]]?.[indexed[2].split('.').reduce((value, key) => value?.[key], selected)]?.[indexed[3]] : selected?.[filter.field || 'type'];
    if (filter.marks && !marks.get(filter.marks)?.has(type) || filter.minType && type < filter.minType ||
        filter.flag && !flags.get(filter.flag) || filter.whileIn && !filter.whileIn.entry.active) return original();
    entry.entries++;
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
    Terraria, Microsoft, assert, state: {}, events: [], SceneEffectPriority: { BossLow: 0 }, FIRST_NPC: 697, FIRST_ITEM: 6145,
    Templates: { Adopt() {} },
    Ref: class { constructor(value) { this.value = value; } },
    Vector2: { new: vector }, Color: { new: color },
    Entities: { InstanceOf: (entity, field, templates) => {
        const template = templates.get(entity.type);
        if (!template) return undefined;
        if (!entity[field] || entity[field].constructor !== template.constructor) entity[field] = Object.assign(Object.create(Object.getPrototypeOf(template)), template);
        return entity[field];
    }, Define() {} },
    ModNet: { InstallEntity() {} }, Ready: { Add: f => ready.push(f) }, SpawnLoader: { InstallPool() {} },
    TownNPCLoader: { Hook() {}, TalkingTo: () => talking },
    Safe: { Run(label, fn) { try { return fn(); } catch (e) { errors.push([label, e]); } }, Report(label, e) { errors.push([label, e]); } },
    bl: { addressOf: p => p?.__address, mod: { uuid: 'item-hooks' }, log() {},
        hookFlags: { set: (k, v) => flags.set(k, v) },
        hookMarks: { set(k, t) { if (!marks.has(k)) marks.set(k, new Set()); marks.get(k).add(t); } } },
};
const context = vm.createContext(sandbox);
for (const file of ['Core/Hooks.js', 'StatModifier.js', 'ModItem.js', 'ModPlayer.js', 'ModNPC.js',
    'Core/GlobalType.js', 'Core/GlobalRegistry.js', 'GlobalItem.js', 'Loaders/ItemLoader.js', 'Loaders/ItemCombatHooks.js', 'Loaders/ItemUseHooks.js',
    'Loaders/GlobalItemLoader.js', 'Loaders/PlayerItemHooks.js', 'Loaders/PlayerCombatHooks.js', 'Loaders/PlayerLoader.js', 'Loaders/NPCLoader.js']) {
    vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
}
vm.runInContext(`
const globalItems = new GlobalRegistry(GlobalItem, () => Terraria.Item, '__globalItems', 'GetGlobalItem');
const record = (name, ...args) => {
    events.push(name);
    const value = state[name];
    return typeof value === 'function' ? value(...args) : value;
};
class Local extends ModItem {}
const names = ['ModifyWeaponDamage', 'ModifyWeaponCrit', 'ModifyWeaponKnockback', 'ModifyItemScale', 'CanHitNPC',
    'ModifyHitNPC', 'CanHitPvp', 'ModifyHitPvp', 'OnHitPvp', 'CanMeleeAttackCollideWithNPC', 'MeleeEffects', 'UseItemHitbox'];
for (const name of names) Local.prototype[name] = function (...args) { return record('item.' + name, ...args); };
class Plain extends ModItem {}
class Late extends Local {}
for (const [cls, type] of [[Local, 6145], [Plain, 6146], [Late, 6147]]) {
    ItemLoader.ByType.set(type, new cls()); ItemLoader.Hook(cls, type);
}
class PlayerMod extends ModPlayer {
    ModifyWeaponDamage(...args) { return record('player.ModifyWeaponDamage', ...args); }
    ModifyWeaponCrit(...args) { return record('player.ModifyWeaponCrit', ...args); }
    ModifyWeaponKnockback(...args) { return record('player.ModifyWeaponKnockback', ...args); }
    ModifyItemScale(...args) { return record('player.ModifyItemScale', ...args); }
    CanHitNPC() { return record('player.CanHitNPC'); }
    CanHitNPCWithItem() { return record('player.CanHitNPCWithItem'); }
    CanMeleeAttackCollideWithNPC() { return record('player.Collision'); }
    ModifyHitNPC(...args) { return record('player.ModifyHitNPC', ...args); }
    ModifyHitNPCWithItem(...args) { return record('player.ModifyHitNPCWithItem', ...args); }
    OnHitNPC(...args) { return record('player.OnHitNPC', ...args); }
    CanHitPvp() { return record('player.CanHitPvp'); }
    MeleeEffects(...args) { return record('player.MeleeEffects', ...args); }
    ModifyHurt(...args) { return record('player.ModifyHurt', ...args); }
    ConsumableDodge() { return record('player.ConsumableDodge'); }
}
class GlobalMod extends GlobalItem {
    ModifyWeaponDamage(...args) { return record('global.ModifyWeaponDamage', ...args); }
}
class NPCMod extends ModNPC {
    CanBeHitByItem() { return record('npc.CanBeHitByItem'); }
    OnHitByItem(...args) { return record('npc.OnHitByItem', ...args); }
}
NPCLoader.ByType.set(697, new NPCMod());
globalThis.api = { Local, Plain, Late, PlayerMod, GlobalMod, NPCMod, ItemLoader, ItemCombatHooks,
    PlayerItemHooks, PlayerCombatHooks, PlayerLoader, GlobalItemLoader, NPCLoader, globalItems };
`, context);
const { Local, Plain, Late, PlayerMod, GlobalMod, NPCMod, ItemLoader, ItemCombatHooks,
    PlayerItemHooks, PlayerCombatHooks, PlayerLoader, GlobalItemLoader, NPCLoader, globalItems } = sandbox.api;
function entity(type) { return { __address: address++, type, stack: 1, prefix: 0, scale: 1, melee: true }; }
const weapon = entity(6145), plain = entity(6146), late = entity(6147), vanillaItem = entity(1);
const player = (index) => {
    const p = { __address: address++, whoAmI: index, inventory: [weapon], selectedItem: 0, statLife: 100, dead: false };
    for (const entry of installed.values()) if (entry.owner === 'Terraria.Player') p[entry.key] = (...args) => invoke(entry, [p, ...args]);
    p['void ApplyMeleeScale(ref float scale)'] = ref => { if (p.meleeScaleGlove) ref.value *= 1.1; };
    Main.player[index] = p;
    return p;
};
const p = player(0), targetPlayer = player(1);
Object.defineProperty(p, 'selectedItemState', { get: () => ({ selected: p.selectedItem }) });
const rect = { X: 0, Y: 0, Width: 10, Height: 10 };
const npc = Object.assign(entity(697), { whoAmI: 0, Hitbox: rect }); Main.npc[0] = npc;
let nativePermission = true, nativeDamage = 20, nativeKnockback = 3, nativeCrit = false, hurtDamage = 0, lastStrike;
const setVanilla = (owner, name, fn) => vanilla(owner, name, fn);
setVanilla('Terraria.Player', 'GetWeaponDamage', () => 20);
setVanilla('Terraria.Player', 'GetWeaponCrit', () => 4);
setVanilla('Terraria.Player', 'GetWeaponKnockback', (_, item, value) => value);
setVanilla('Terraria.Player', 'GetAdjustedItemScale', (player, item) => item.scale * (item.melee && player.meleeScaleGlove ? 1.1 : 1));
setVanilla('Terraria.Player', 'ItemCheck_GetMeleeHitbox', (player, item, frame, noHitbox, box) => {
    noHitbox.value = false;
    box.value = { X: 0, Y: 0, Width: item.scale * (player.meleeScaleGlove ? 1.1 : 1) * 10, Height: 10 };
});
setVanilla('Terraria.Player', 'CanNPCBeHitByPlayerOrPlayerProjectile', () => nativePermission);
setVanilla('Microsoft.Xna.Framework.Rectangle', 'Intersects', () => nativePermission);
setVanilla('Terraria.Player', 'ProcessHitAgainstNPC', (player, item, box, damage, kb, index) => {
    const victim = Main.npc[index];
    if (!call('Terraria.Player', 'CanNPCBeHitByPlayerOrPlayerProjectile', player, victim, null)) return 0;
    if (victim.friendly) return 0;
    if (!call('Microsoft.Xna.Framework.Rectangle', 'Intersects', box, victim.Hitbox)) return 0;
    return call('Terraria.NPC', 'StrikeNPC', victim, damage, kb, 1, nativeCrit, false, player.whoAmI);
});
setVanilla('Terraria.NPC', 'StrikeNPC', (npc, damage, kb, direction, crit, fromNet) => {
    lastStrike = { damage, kb, direction, crit, fromNet };
    return nativeDamage;
});
setVanilla('Terraria.Player', 'Hurt', (player, source, damage, direction, pvp, quiet, crit) => { hurtDamage = damage; return damage; });
setVanilla('Terraria.Player', 'ItemCheck_MeleeHitPVP', (player, item, box, damage, kb) =>
    call('Terraria.Player', 'Hurt', targetPlayer, { _sourceNPCIndex: -1, _sourceProjectileLocalIndex: -1 }, damage, 1, true, false, false, 0, true));
setVanilla('Terraria.Player', 'ItemCheck_EmitUseVisuals', () => sandbox.events.push('native.MeleeEffects'));
let checks = 0;
function test(name, run) {
    for (const key of Object.keys(sandbox.state)) delete sandbox.state[key];
    sandbox.events.length = 0; nativePermission = true; nativeDamage = 20; nativeCrit = false; p.meleeScaleGlove = false;
    const before = errors.length;
    run();
    assert.equal(errors.length, before, name + ': unexpected callback error');
    checks++; console.log(name + ': ok');
}
const hit = (item = weapon, box = rect) => call('Terraria.Player', 'ProcessHitAgainstNPC', p, item, box, 20, 3, 0);
const pvp = (item = weapon) => call('Terraria.Player', 'ItemCheck_MeleeHitPVP', p, item, rect, 20, 3);
test('all requested combat methods have neutral defaults', () => {
    const m = new Plain();
    assert.equal(m.CanHitNPC(), null); assert.equal(m.CanMeleeAttackCollideWithNPC(), null); assert.equal(m.CanHitPvp(), true);
});
for (const [owner, name, args] of [
    ['Terraria.Player', 'GetWeaponDamage', [20]], ['Terraria.Player', 'GetWeaponCrit', [4]],
    ['Terraria.Player', 'GetWeaponKnockback', [3]], ['Terraria.Player', 'GetAdjustedItemScale', []],
    ['Terraria.Player', 'ProcessHitAgainstNPC', [rect, 20, 3, 0]],
    ['Terraria.Player', 'ItemCheck_MeleeHitPVP', [rect, 20, 3]], ['Terraria.Player', 'ItemCheck_EmitUseVisuals', [rect]]]) {
    test(name + ' excludes vanilla and plain mod items before JS', () => {
        const entry = method(owner, name), before = entry.entries;
        for (const item of [vanillaItem, plain]) call(owner, name, p, item, ...args);
        assert.equal(entry.entries, before);
        assert.equal(sandbox.events.filter(e => e.startsWith('item.')).length, 0);
    });
}
test('hitbox native filter excludes items without overrides', () => {
    const entry = method('Terraria.Player', 'ItemCheck_GetMeleeHitbox'), before = entry.entries;
    call('Terraria.Player', 'ItemCheck_GetMeleeHitbox', p, plain, rect, new sandbox.Ref(false), new sandbox.Ref(rect));
    assert.equal(entry.entries, before);
});
test('inherited overrides registered later have marks and a single native hook', () => {
    sandbox.state['item.ModifyWeaponDamage'] = (item, player, damage) => { assert.equal(item, late); damage.Flat = 2; };
    assert.equal(call('Terraria.Player', 'GetWeaponDamage', p, late), 22);
    assert.equal(method('Terraria.Player', 'GetWeaponDamage').hooks.length, 1);
});
test('numeric damage return is compatible and rounds once', () => {
    sandbox.state['item.ModifyWeaponDamage'] = () => 31.9;
    assert.equal(call('Terraria.Player', 'GetWeaponDamage', p, weapon), 31);
});
test('damage StatModifier fields apply in the documented order', () => {
    sandbox.state['item.ModifyWeaponDamage'] = (_, player, m) => { m.Base = 2; m.Additive = 1.5; m.Multiplicative = 2; m.Flat = 3; };
    assert.equal(call('Terraria.Player', 'GetWeaponDamage', p, weapon), 69);
});
for (const value of [-1, NaN, Infinity]) test('invalid damage ' + value + ' is bounded', () => {
    sandbox.state['item.ModifyWeaponDamage'] = () => value;
    assert.equal(call('Terraria.Player', 'GetWeaponDamage', p, weapon), 0);
});
test('critical chance mutates its Ref and returns an integer', () => {
    sandbox.state['item.ModifyWeaponCrit'] = (item, player, crit) => { assert.equal(item, weapon); assert.equal(player, p); crit.value += 6.9; };
    assert.equal(call('Terraria.Player', 'GetWeaponCrit', p, weapon), 10);
});
test('knockback uses StatModifier and clamps negative results', () => {
    sandbox.state['item.ModifyWeaponKnockback'] = (_, player, m) => { m.Multiplicative = 2; m.Flat = 1; };
    assert.equal(call('Terraria.Player', 'GetWeaponKnockback', p, weapon, 3), 7);
    sandbox.state['item.ModifyWeaponKnockback'] = (_, player, m) => { m.Flat = -100; };
    assert.equal(call('Terraria.Player', 'GetWeaponKnockback', p, weapon, 3), 0);
});
test('adjusted scale receives a multiplier independent from Item.scale', () => {
    weapon.scale = 2;
    sandbox.state['item.ModifyItemScale'] = (_, player, scale) => { assert.equal(scale.value, 1); scale.value = 1.5; };
    assert.equal(call('Terraria.Player', 'GetAdjustedItemScale', p, weapon), 3);
    assert.equal(weapon.scale, 2); weapon.scale = 1;
});
test('scale changes native hitbox and preserves the glove multiplier', () => {
    p.meleeScaleGlove = true;
    sandbox.state['item.ModifyItemScale'] = (_, player, scale) => { assert.equal(scale.value, 1.1); scale.value *= 2; };
    const box = new sandbox.Ref(rect);
    call('Terraria.Player', 'ItemCheck_GetMeleeHitbox', p, weapon, rect, new sandbox.Ref(false), box);
    assert.equal(box.value.Width, 22); assert.equal(weapon.scale, 1);
    assert.equal(sandbox.events.filter(e => e === 'item.ModifyItemScale').length, 1);
});
test('zero item scale remains zero without NaN', () => {
    weapon.scale = 0;
    sandbox.state['item.ModifyItemScale'] = (_, player, scale) => { scale.value *= 2; };
    assert.equal(call('Terraria.Player', 'GetAdjustedItemScale', p, weapon), 0); weapon.scale = 1;
});
test('UseItemHitbox can replace the rectangle and disable the attack', () => {
    const box = new sandbox.Ref(rect), noHitbox = new sandbox.Ref(false);
    sandbox.state['item.UseItemHitbox'] = (item, player, hitbox, disabled) => {
        assert.equal(item.scale, 1); assert.equal(player, p); hitbox.value = { X: 5, Y: 6, Width: 50, Height: 60 }; disabled.value = true;
    };
    call('Terraria.Player', 'ItemCheck_GetMeleeHitbox', p, weapon, rect, noHitbox, box);
    assert.equal(box.value.Width, 50); assert.equal(noHitbox.value, true);
});
test('hitbox scale restores after a native exception', () => {
    const entry = method('Terraria.Player', 'ItemCheck_GetMeleeHitbox'), original = entry.vanilla;
    sandbox.state['item.ModifyItemScale'] = (_, player, scale) => { scale.value = 2; };
    entry.vanilla = () => { throw Error('native failure'); };
    try { assert.throws(() => call('Terraria.Player', 'ItemCheck_GetMeleeHitbox', p, weapon, rect, new sandbox.Ref(false), new sandbox.Ref(rect))); }
    finally { entry.vanilla = original; }
    assert.equal(weapon.scale, 1);
});
test('item NPC veto prevents native damage', () => { sandbox.state['item.CanHitNPC'] = false; assert.equal(hit(), undefined); });
test('nullable permission preserves the native decision', () => { nativePermission = false; assert.equal(hit(), 0); });
test('true eligibility still requires a native collision', () => { nativePermission = false; sandbox.state['item.CanHitNPC'] = true; assert.equal(hit(), 0); });
test('item permission and collision can override both native checks', () => {
    nativePermission = false; sandbox.state['item.CanHitNPC'] = true; sandbox.state['item.CanMeleeAttackCollideWithNPC'] = true; assert.equal(hit(), 20);
});
test('explicit item permission passes the friendly barrier and callbacks see the original flag', () => {
    npc.friendly = true; sandbox.state['item.CanHitNPC'] = true;
    sandbox.state['item.ModifyHitNPC'] = (_, player, target) => assert.equal(target.friendly, true);
    try { assert.equal(hit(), 20); assert.equal(npc.friendly, true); }
    finally { npc.friendly = false; }
});
test('friendly flag restores when native collision fails after permission', () => {
    npc.friendly = true; nativePermission = false; sandbox.state['item.CanHitNPC'] = true;
    try { assert.equal(hit(), 0); assert.equal(npc.friendly, true); }
    finally { npc.friendly = false; }
});
test('collision veto prevents native damage', () => { sandbox.state['item.CanMeleeAttackCollideWithNPC'] = false; assert.equal(hit(), undefined); });
test('nullable collision skips the Rectangle JS hook', () => { const entry = method('Microsoft.Xna.Framework.Rectangle', 'Intersects'), before = entry.entries; hit(); assert.equal(entry.entries, before); });
test('forced collision affects only the attack and target rectangles', () => {
    nativePermission = false; sandbox.state['item.CanHitNPC'] = true; sandbox.state['item.CanMeleeAttackCollideWithNPC'] = true;
    const entry = method('Terraria.Player', 'ProcessHitAgainstNPC'), original = entry.vanilla;
    entry.vanilla = (...args) => { assert.equal(call('Microsoft.Xna.Framework.Rectangle', 'Intersects', { ...rect, X: 99 }, rect), false); return original(...args); };
    try { assert.equal(hit(), 20); } finally { entry.vanilla = original; }
    assert.equal(flags.get('player.MeleeCollisionActive'), false);
});
test('ModifyHitNPC changes strike fields and modifiers', () => {
    sandbox.state['item.ModifyHitNPC'] = (item, player, target, m) => { assert.equal(target, npc); m.SourceDamage.Multiplicative = 2; m.Knockback.Flat = 4; m.hitDirection = -1; m.SetCrit(); };
    hit(); assert.deepEqual(lastStrike, { damage: 40, kb: 7, direction: -1, crit: true, fromNet: false });
});
test('network strikes are not modified again', () => {
    const entry = method('Terraria.Player', 'ProcessHitAgainstNPC'), original = entry.vanilla;
    entry.vanilla = () => call('Terraria.NPC', 'StrikeNPC', npc, 20, 3, 1, false, true, 0);
    try { hit(); } finally { entry.vanilla = original; }
    assert.equal(sandbox.events.includes('item.ModifyHitNPC'), false);
});
test('native exception restores attack flags', () => {
    const entry = method('Terraria.Player', 'ProcessHitAgainstNPC'), original = entry.vanilla;
    entry.vanilla = () => { throw Error('native failure'); };
    try { assert.throws(hit); } finally { entry.vanilla = original; }
    assert.equal(flags.get('player.Attack'), false); assert.equal(flags.get('player.CanHitNPCActive'), false);
});
test('nested item strikes restore the outer source and notification target', () => {
    const seen = [];
    sandbox.state['item.ModifyHitNPC'] = (item, player, target, m) => {
        seen.push(item);
        if (item === weapon) { hit(late); m.damage = 40; }
        else m.damage = 5;
    };
    hit(); assert.deepEqual(seen, [weapon, late]); assert.equal(lastStrike.damage, 40);
    assert.equal(flags.get('player.Attack'), false);
});
test('nonfinite hit modifiers do not reach the native strike', () => {
    sandbox.state['item.ModifyHitNPC'] = (_, player, target, m) => { m.damage = NaN; m.knockBack = Infinity; };
    hit(); assert.equal(lastStrike.damage, 0); assert.equal(lastStrike.kb, 0);
});
test('item MeleeEffects receives the actual hitbox after native visuals', () => {
    sandbox.state['item.MeleeEffects'] = (item, player, box) => { assert.equal(item, weapon); assert.equal(player, p); assert.equal(box, rect); };
    call('Terraria.Player', 'ItemCheck_EmitUseVisuals', p, weapon, rect);
    assert.deepEqual(sandbox.events, ['native.MeleeEffects', 'item.MeleeEffects']);
});
test('PvP veto prevents Hurt and notification', () => { sandbox.state['item.CanHitPvp'] = false; assert.equal(pvp(), 0); assert.equal(sandbox.events.includes('item.OnHitPvp'), false); });
test('PvP modifiers reach native Hurt and actual damage notification', () => {
    sandbox.state['item.ModifyHitPvp'] = (item, player, target, m) => { assert.equal(item, weapon); assert.equal(player, p); assert.equal(target, targetPlayer); m.damage = 35; m.crit = true; };
    sandbox.state['item.OnHitPvp'] = (_, player, target, info) => { assert.equal(info.Damage, 35); assert.equal(info.Crit, true); };
    assert.equal(pvp(), 35); assert.equal(hurtDamage, 35);
});
test('failed PvP strike does not notify', () => {
    const entry = method('Terraria.Player', 'Hurt'), original = entry.vanilla; entry.vanilla = () => 0;
    try { assert.equal(pvp(), 0); } finally { entry.vanilla = original; }
    assert.equal(sandbox.events.includes('item.OnHitPvp'), false);
});
test('PvP context and flags restore after native failure', () => {
    const entry = method('Terraria.Player', 'ItemCheck_MeleeHitPVP'), original = entry.vanilla; entry.vanilla = () => { throw Error('native failure'); };
    try { assert.throws(pvp); } finally { entry.vanilla = original; }
    assert.equal(PlayerCombatHooks.PvpAttack, null); assert.equal(flags.get('player.HurtActive'), false);
});
test('nested PvP scopes restore their outer weapon', () => {
    const entry = method('Terraria.Player', 'ItemCheck_MeleeHitPVP'), original = entry.vanilla;
    entry.vanilla = (player, item, ...args) => {
        if (item === weapon) {
            pvp(late); assert.equal(PlayerCombatHooks.PvpAttack.item, weapon);
        }
        return original(player, item, ...args);
    };
    try { pvp(); } finally { entry.vanilla = original; }
    assert.equal(PlayerCombatHooks.PvpAttack, null);
});
test('callback failure is isolated and attributed to the class and method', () => {
    sandbox.state['item.ModifyWeaponDamage'] = () => { throw Error('callback failure'); };
    assert.equal(call('Terraria.Player', 'GetWeaponDamage', p, weapon), 20);
    const failure = errors.pop(); assert.equal(failure[0], 'Local.ModifyWeaponDamage');
});
test('item copies have independent callback state', () => {
    const other = entity(6145);
    assert.notEqual(ItemLoader.Of(weapon), ItemLoader.Of(other));
    ItemLoader.Of(weapon).counter = 5; assert.equal(ItemLoader.Of(other).counter, undefined);
});
globalItems.list.push(new GlobalMod()); GlobalItemLoader.Hook(GlobalMod);
PlayerLoader.Add(PlayerMod);
for (const key of ['player.WeaponDamage', 'player.WeaponCrit', 'player.WeaponKnockback', 'player.ItemScale', 'player.ItemHitbox']) ItemCombatHooks.All(key);
PlayerItemHooks.Install(PlayerMod); PlayerCombatHooks.Install(PlayerMod); PlayerLoader.InstallNPCContact();
test('weapon damage composes item, global and player in one native hook', () => {
    sandbox.state['item.ModifyWeaponDamage'] = (_, player, m) => { m.Flat = 2; };
    sandbox.state['global.ModifyWeaponDamage'] = (_, player, m) => { m.Multiplicative = 2; };
    sandbox.state['player.ModifyWeaponDamage'] = (_, item, m) => { m.Flat = 3; };
    assert.equal(call('Terraria.Player', 'GetWeaponDamage', p, weapon), 47);
    assert.deepEqual(sandbox.events, ['item.ModifyWeaponDamage', 'global.ModifyWeaponDamage', 'player.ModifyWeaponDamage']);
    assert.equal(method('Terraria.Player', 'GetWeaponDamage').hooks.length, 1);
});
test('legacy ModPlayer WeaponDamage assignment remains supported', () => {
    PlayerMod.prototype.ModifyWeaponDamage = function () { this.WeaponDamage = 25; };
    assert.equal(call('Terraria.Player', 'GetWeaponDamage', p, weapon), 25);
    PlayerMod.prototype.ModifyWeaponDamage = (...args) => sandbox.state['player.ModifyWeaponDamage']?.(...args);
});
test('a global observer widens marks for vanilla and later mod types', () => {
    assert.equal(call('Terraria.Player', 'GetWeaponDamage', p, vanillaItem), 20);
    class Another extends Plain {}
    ItemLoader.ByType.set(6148, new Another()); ItemLoader.Hook(Another, 6148);
    const another = entity(6148); sandbox.state['global.ModifyWeaponDamage'] = () => 50;
    assert.equal(call('Terraria.Player', 'GetWeaponDamage', p, another), 50);
});
test('critical and knockback modifiers compose before ModPlayer', () => {
    sandbox.state['item.ModifyWeaponCrit'] = (_, player, crit) => { crit.value += 2; };
    sandbox.state['player.ModifyWeaponCrit'] = (_, item, crit) => { crit.value *= 2; };
    assert.equal(call('Terraria.Player', 'GetWeaponCrit', p, weapon), 12);
    sandbox.state['item.ModifyWeaponKnockback'] = (_, player, m) => { m.Flat = 2; };
    sandbox.state['player.ModifyWeaponKnockback'] = (_, item, m) => { m.Multiplicative = 2; };
    assert.equal(call('Terraria.Player', 'GetWeaponKnockback', p, weapon, 3), 8);
});
for (const name of ['player.CanHitNPC', 'player.CanHitNPCWithItem', 'npc.CanBeHitByItem']) test(name + ' veto beats item permission', () => {
    sandbox.state['item.CanHitNPC'] = true; sandbox.state[name] = false; assert.ok(!hit());
});
test('player collision veto beats item collision permission', () => {
    sandbox.state['item.CanMeleeAttackCollideWithNPC'] = true; sandbox.state['player.Collision'] = false; assert.equal(hit(), undefined);
});
test('hit modifiers compose item then general player then item player', () => {
    sandbox.state['item.ModifyHitNPC'] = (_, player, target, m) => { m.damage = 25; };
    sandbox.state['player.ModifyHitNPC'] = (_, target, m) => { assert.equal(m.damage, 25); m.damage += 5; };
    sandbox.state['player.ModifyHitNPCWithItem'] = (_, item, target, m) => { assert.equal(m.damage, 30); m.damage *= 2; };
    hit(); assert.equal(lastStrike.damage, 60);
});
test('player PvP veto wins before modifiers', () => {
    sandbox.state['player.CanHitPvp'] = false; assert.equal(pvp(), 0); assert.equal(sandbox.events.includes('item.ModifyHitPvp'), false);
});
test('PvP item modifiers precede receiver modifiers', () => {
    sandbox.state['item.ModifyHitPvp'] = (_, player, target, m) => { m.damage = 30; };
    sandbox.state['player.ModifyHurt'] = (_, m) => { assert.equal(m.damage, 30); m.damage += 5; };
    assert.equal(pvp(), 35);
});
test('dodge suppresses PvP item notification', () => {
    const prior = Main.myPlayer; Main.myPlayer = targetPlayer.whoAmI; sandbox.state['player.ConsumableDodge'] = true;
    try { assert.equal(pvp(), 0); } finally { Main.myPlayer = prior; }
    assert.equal(sandbox.events.includes('item.OnHitPvp'), false);
});
test('MeleeEffects order remains native then item then ModPlayer', () => {
    call('Terraria.Player', 'ItemCheck_EmitUseVisuals', p, weapon, rect);
    assert.deepEqual(sandbox.events, ['native.MeleeEffects', 'item.MeleeEffects', 'player.MeleeEffects']);
});
for (const order of ['item global player', 'global player item', 'player item global']) test('registration order ' + order + ' preserves one damage pipeline', () => {
    const callbacks = [], marked = new Map(), issues = [];
    const damageMethod = { hook(callback) { callbacks.push(callback); } };
    const other = { Install() {} };
    const local = vm.createContext({
        Terraria: { Player: { 'int GetWeaponDamage(Item sItem)': damageMethod } },
        FIRST_ITEM: 6145, SceneEffectPriority: { BossLow: 0 },
        PlayerUpdateHooks: other, PlayerJumpHooks: other, PlayerDrawHooks: other, PlayerWorldHooks: other, PlayerNetworkHooks: other,
        Entities: { InstanceOf: (item, field, templates) => item[field] || (item[field] = Object.assign(Object.create(Object.getPrototypeOf(templates.get(item.type))), templates.get(item.type))) },
        Safe: { Report: (name, e) => issues.push([name, e]), Run: (name, fn) => fn() },
        bl: { mod: { uuid: 'order' }, addressOf: value => value.__address,
            hookMarks: { set(key, type) { if (!marked.has(key)) marked.set(key, new Set()); marked.get(key).add(type); } } },
    });
    for (const file of ['Core/Hooks.js', 'StatModifier.js', 'ModItem.js', 'ModPlayer.js', 'Core/GlobalType.js', 'Core/GlobalRegistry.js', 'GlobalItem.js',
        'Loaders/ItemLoader.js', 'Loaders/ItemCombatHooks.js', 'Loaders/ItemUseHooks.js', 'Loaders/PlayerItemHooks.js', 'Loaders/PlayerCombatHooks.js', 'Loaders/PlayerLoader.js', 'Loaders/GlobalItemLoader.js']) {
        vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), local, { filename: file });
    }
    vm.runInContext(`
        const globalItems = new GlobalRegistry(GlobalItem, () => Terraria.Item, '__globalItems', 'GetGlobalItem');
        class Weapon extends ModItem { ModifyWeaponDamage(item, player, m) { m.Flat += 2; } }
        class Global extends GlobalItem { ModifyWeaponDamage(item, player, m) { m.Multiplicative *= 2; } }
        class Player extends ModPlayer { ModifyWeaponDamage(player, item, m) { m.Flat += 3; } }
        const install = {
            item() { ItemLoader.ByType.set(6145, new Weapon()); ItemLoader.Hook(Weapon, 6145); },
            global() { globalItems.list.push(new Global()); GlobalItemLoader.Hook(Global); },
            player() { PlayerLoader.Add(Player); PlayerLoader.Hook(Player); }
        };
        for (const name of ${JSON.stringify(order.split(' '))}) install[name]();
    `, local);
    assert.equal(callbacks.length, 1);
    const result = callbacks[0](() => 20, { __address: 99 }, entity(6145));
    assert.equal(result, 47); assert.deepEqual(issues, []);
    assert.equal(marked.get('player.WeaponDamage').has(6145), true);
    assert.equal(marked.get('player.WeaponDamage').has(1), true);
});
runUseCases({ vm, fs, path, source, context, sandbox, entity, p, plain, vanillaItem, call, method, setVanilla, test, assert, errors });
assert.deepEqual(missing, [], 'Native signatures and parameter names must match the mobile dump');
console.log(checks + ' behavior checks passed; ' + [...installed.values()].filter(e => e.hooks.length).length + ' native signatures verified.');
