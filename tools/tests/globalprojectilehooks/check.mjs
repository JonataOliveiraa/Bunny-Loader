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
    const type = selected?.[filter.field || 'type'];
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
const Terraria = native('Terraria'), Microsoft = native('Microsoft'), Main = Terraria.Main;
Main.player = [{ position: vector(), height: 40 }]; Main.projectile = []; Main.projPet = []; Main.projFrames = [];
Terraria.ID.ProjectileID.Sets.IsAComplexCollision = [];
Terraria.ID.ProjectileID.Sets.FallingBlockDoesNotFallThroughPlatforms = [];
const complex = Terraria.ID.ProjectileID.Sets.IsAComplexCollision;
const texture = { __address: 900 }, chain = { __address: 901 };
Terraria.GameContent.TextureAssets.Projectile = new Proxy({}, { get: () => ({ Value: texture }) });
Terraria.Lighting['Color GetColor(int x, int y)'];
vanilla('Terraria.Lighting', 'GetColor', () => ({ R: 10, G: 20, B: 30, A: 255 }));
function entity(type = 1, mod) {
    const p = { __address: address++, type, active: true, tileCollide: true, aiStyle: -1, ai: [0], rotation: 0,
        owner: 0, position: vector(32, 16), velocity: vector(0, 1), width: 8, height: 8, timeLeft: 60, Center: vector(36, 20), ModProjectile: mod };
    for (const entry of installed.values()) if (entry.owner === 'Terraria.Projectile') p[entry.key] = (...args) => call(entry.owner, entry.key.split('(')[0].split(' ').at(-1), p, ...args);
    p['void .ctor()'] = () => {};
    p['void SetDefaults(int Type)'] = t => { p.type = t; sandbox.registry.Attach(p, true); };
    return p;
}
const sandbox = {
    Terraria, Microsoft, Ref: class { constructor(value) { this.value = value; } },
    Vector2: { new: vector, Clone: v => vector(v.X, v.Y), Add: (a, b) => vector(a.X + b.X, a.Y + b.Y) },
    Rectangle: { new: (X, Y, Width, Height) => ({ X, Y, Width, Height }) }, Color: { White: { R: 255, G: 255, B: 255, A: 255 } },
    Entities: { InstanceOf: p => p.ModProjectile, Define() {}, Clone: g => Object.create(Object.getPrototypeOf(g), Object.getOwnPropertyDescriptors(g)) },
    FIRST_PROJECTILE: 1136, Templates: { Adopt() {} }, Ready: { Add: fn => ready.push(fn) },
    ModNet: { InstallEntity() {} }, HitLoader: { ProjectileHitsNPC() {} }, DamageClassLoader: { Defaulting: (_, fn) => fn() },
    bl: { defineMethod() {}, addressOf: p => p.__address, projectiles: { isModProjectile: type => type === 1136 || type === 1137 || type === 1200 },
        hookFlags: { set: (k, v) => flags.set(k, v) },
        hookMarks: { set(k, t) { if (!marks.has(k)) marks.set(k, new Set()); marks.get(k).add(t); } } },
    Safe: { Run(label, fn) { try { return fn(); } catch (e) { errors.push([label, e]); } }, Report(label, e) { errors.push([label, e]); } },
    state: {}, events: [], assert,
};
const context = vm.createContext(sandbox);
for (const file of ['Core/Hooks.js', 'Core/GlobalType.js', 'Core/GlobalRegistry.js', 'GlobalProjectile.js', 'ModProjectile.js', 'Loaders/ProjectileLoader.js', 'Loaders/GlobalProjectileLoader.js']) {
    vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
}
vm.runInContext(`
const globalProjectiles = new GlobalRegistry(GlobalProjectile, () => Terraria.Projectile, '__globals', 'GetGlobalProjectile');
globalThis.registry = globalProjectiles;
const record = (name, ...args) => { events.push(name); const result = state[name]; return typeof result === 'function' ? result(...args) : result; };
class Local extends ModProjectile {}
const methods = ['PreDraw', 'PostDraw', 'GetAlpha', 'Colliding', 'CanDamage', 'ModifyDamageHitbox', 'OnTileCollide', 'TileCollideStyle', 'MinionContactDamage', 'CanCutTiles', 'CutTiles', 'CanUseGrapple', 'UseGrapple', 'GrappleCanLatchOnTo', 'PreKill', 'OnKill'];
for (const name of methods) Local.prototype[name] = function (...args) { return record('m.' + name, ...args); };
ProjectileLoader.ByType.set(1136, new Local());
ProjectileLoader.Hook(Local, 1136);
class First extends GlobalProjectile {
    InstancePerEntity = true;
    count = 0;
    AppliesToEntity(p) { return p.type === 1 || p.type >= FIRST_PROJECTILE; }
}
class Second extends First {}
for (const name of methods) {
    First.prototype[name] = function (...args) { this.count++; return record('a.' + name, ...args); };
    Second.prototype[name] = function (...args) { this.count++; return record('b.' + name, ...args); };
}
GlobalProjectile.register(First);
GlobalProjectile.register(Second);
class Future extends ModProjectile {}
ProjectileLoader.ByType.set(1137, new Future());
ProjectileLoader.Hook(Future, 1137);
Object.assign(globalThis, { Local, First, Second, Future });
`, context);
let checks = 0;
function test(name, run) {
    sandbox.state = {}; sandbox.events = []; errors.length = 0;
    for (const entry of installed.values()) if (entry.owner !== 'Terraria.Lighting') entry.vanilla = () => {};
    const m = new sandbox.Local(), p = entity(1136, m);
    sandbox.registry.Attach(p, true);
    run(p, sandbox.state, sandbox.events);
    assert.deepEqual(errors, [], name + ': callback errors');
    checks++; console.log(name + ': ok');
}
const pr = (name, ...args) => call('Terraria.Projectile', name, ...args);
test('first defined global collision precedes local', (p, s, e) => {
    s['a.Colliding'] = false; s['b.Colliding'] = true; s['m.Colliding'] = true;
    assert.equal(pr('Colliding', p, {}, {}), false); assert.deepEqual(e, ['a.Colliding']);
});
test('nullable collisions reach local then native', (p, s, e) => {
    vanilla('Terraria.Projectile', 'Colliding', () => true);
    assert.equal(pr('Colliding', p, {}, {}), true); assert.deepEqual(e, ['a.Colliding', 'b.Colliding', 'm.Colliding']);
});
test('global alpha priority preserves transparent colors', (p, s, e) => {
    s['b.GetAlpha'] = { R: 0, G: 0, B: 0, A: 0 };
    assert.equal(pr('GetAlpha', p, {}), s['b.GetAlpha']); assert.deepEqual(e, ['a.GetAlpha', 'b.GetAlpha']);
});
test('global false vetoes damage before local and native', (p, s, e) => {
    s['a.CanDamage'] = false; vanilla('Terraria.Projectile', 'Damage', () => e.push('native'));
    pr('Damage', p); assert.deepEqual(e, ['a.CanDamage']);
});
test('later damage veto beats an earlier true', (p, s, e) => {
    s['a.CanDamage'] = true; s['b.CanDamage'] = false;
    pr('Damage', p); assert.deepEqual(e, ['a.CanDamage', 'b.CanDamage']);
});
test('global true overrides local damage veto and enables complex collision temporarily', (p, s, e) => {
    s['a.CanDamage'] = true; s['m.CanDamage'] = false; complex[p.type] = false;
    vanilla('Terraria.Projectile', 'Damage', actual => { assert.equal(complex[actual.type], true); e.push('native'); });
    pr('Damage', p); assert.deepEqual(e, ['a.CanDamage', 'b.CanDamage', 'native']); assert.equal(complex[p.type], false);
});
test('nullable global damage respects local veto', (p, s, e) => {
    s['m.CanDamage'] = false; pr('Damage', p); assert.deepEqual(e, ['a.CanDamage', 'b.CanDamage', 'm.CanDamage']);
});
test('dispatch preserves exact documented argument counts', (p, s, e) => {
    s['a.CanDamage'] = (...args) => { assert.equal(args.length, 1); };
    s['m.CanDamage'] = (...args) => { assert.equal(args.length, 1); return false; };
    pr('Damage', p);
    s['a.Colliding'] = (...args) => { assert.equal(args.length, 3); return true; };
    pr('Colliding', p, {}, {});
    s['a.UseGrapple'] = (...args) => { assert.equal(args.length, 2); };
    call('Terraria.Player', 'FireGrapple', {}, { shoot: 1 });
});
test('global minion contact restores shared tables after native error', (p, s, e) => {
    Main.projPet[p.type] = true; complex[p.type] = false; s['b.MinionContactDamage'] = true;
    vanilla('Terraria.Projectile', 'Damage', () => { assert.equal(Main.projPet[p.type], false); throw Error('native failure'); });
    assert.throws(() => pr('Damage', p), /native failure/);
    assert.equal(Main.projPet[p.type], true); assert.equal(complex[p.type], false); Main.projPet[p.type] = false;
});
test('damage hitbox runs local before globals and accepts replacement', (p, s, e) => {
    vanilla('Terraria.Projectile', 'Damage_GetHitbox', () => ({ X: 1, Y: 2, Width: 3, Height: 4 }));
    s['m.ModifyDamageHitbox'] = (_, box) => { box.Width = 8; };
    s['a.ModifyDamageHitbox'] = (_, ref) => { assert.equal(ref.value.Width, 8); ref.value.X = 10; };
    s['b.ModifyDamageHitbox'] = (_, ref) => { assert.equal(ref.value.X, 10); ref.value = { X: 20, Y: 30, Width: 40, Height: 50 }; };
    assert.deepEqual(pr('Damage_GetHitbox', p), { X: 20, Y: 30, Width: 40, Height: 50 });
    assert.deepEqual(e, ['m.ModifyDamageHitbox', 'a.ModifyDamageHitbox', 'b.ModifyDamageHitbox']);
});
test('first defined tile cutting decision wins', (p, s, e) => {
    s['a.CanCutTiles'] = true; s['b.CanCutTiles'] = false; s['m.CanCutTiles'] = false;
    assert.equal(pr('CanCutTiles', p), true); assert.deepEqual(e, ['a.CanCutTiles']);
});
test('custom cutting invokes globals before local', (p, s, e) => {
    s['a.CanCutTiles'] = true;
    vanilla('Terraria.Projectile', 'CutTiles', () => e.push('native'));
    pr('CutTiles', p); assert.deepEqual(e, ['a.CanCutTiles', 'a.CutTiles', 'b.CutTiles', 'm.CutTiles', 'native']);
});
test('tile cutting veto prevents native and every custom cut', (p, s, e) => {
    s['a.CanCutTiles'] = false;
    vanilla('Terraria.Projectile', 'CutTiles', () => e.push('native'));
    pr('CutTiles', p); assert.deepEqual(e, ['a.CanCutTiles']);
});
test('cutting permission runs once and context restores after native failure', (p, s, e) => {
    s['a.CanCutTiles'] = true;
    vanilla('Terraria.Projectile', 'CutTiles', actual => { assert.equal(pr('CanCutTiles', actual), true); throw Error('cut failure'); });
    assert.throws(() => pr('CutTiles', p), /cut failure/);
    assert.equal(e.filter(v => v === 'a.CanCutTiles').length, 1);
    s['a.CanCutTiles'] = false; assert.equal(pr('CanCutTiles', p), false);
});
function movement(p, s, e) {
    vanilla('Terraria.Projectile', 'GetCollisionParams', (_, anchor, width, height) => { anchor.value = vector(.5, .5); width.value = 8; height.value = 8; });
    vanilla('Terraria.Projectile', 'Kill', actual => { e.push('native.kill'); actual.active = false; });
    vanilla('Terraria.Projectile', 'HandleMovement', actual => { actual.velocity.Y = 0; pr('Kill', actual); pr('UpdatePosition', actual, vector()); });
}
test('global tile veto preserves vanilla projectile and timeout still kills', (p, s, e) => {
    p.ModProjectile = undefined; p.type = 1; sandbox.registry.Attach(p, true); movement(p, s, e); s['a.OnTileCollide'] = false;
    pr('HandleMovement', p, vector()); assert.equal(p.active, true);
    assert.deepEqual(e.filter(v => v.includes('OnTileCollide')), ['a.OnTileCollide', 'b.OnTileCollide']);
    p.timeLeft = 0; pr('Kill', p); assert.equal(p.active, false); assert.ok(e.includes('a.OnKill'));
});
test('local tile callback is skipped after global veto', (p, s, e) => {
    movement(p, s, e); s['a.OnTileCollide'] = false;
    pr('HandleMovement', p, vector()); assert.equal(p.active, true); assert.equal(e.includes('m.OnTileCollide'), false);
});
test('all permitted tile callbacks kill once in deterministic order', (p, s, e) => {
    movement(p, s, e); pr('HandleMovement', p, vector());
    assert.equal(p.active, false); assert.deepEqual(e.slice(-8), ['a.OnTileCollide', 'b.OnTileCollide', 'm.OnTileCollide', 'a.PreKill', 'b.PreKill', 'm.PreKill', 'm.OnKill', 'a.OnKill', 'b.OnKill', 'native.kill'].slice(-8));
});
test('style refs share changes and restore fields after native error', (p, s, e) => {
    movement(p, s, e); p.decidesManualFallThrough = false; p.shouldFallThrough = true;
    s['m.TileCollideStyle'] = (_, width) => { width.value = 11; };
    s['a.TileCollideStyle'] = (_, width, height, fall, anchor) => { assert.equal(width.value, 11); width.value = 12; height.value = 13; fall.value = false; anchor.value = vector(.5, 1); };
    vanilla('Terraria.Projectile', 'HandleMovement', actual => {
        const a = {}, w = {}, h = {}; pr('GetCollisionParams', actual, a, w, h);
        assert.deepEqual([w.value, h.value, a.value.Y], [12, 13, 1]); assert.equal(actual.shouldFallThrough, false); throw Error('native failure');
    });
    assert.throws(() => pr('HandleMovement', p, vector()), /native failure/);
    assert.equal(p.__projectileMovement, undefined); assert.equal(p.decidesManualFallThrough, false); assert.equal(p.shouldFallThrough, true);
});
test('style local veto skips globals and restores tileCollide', (p, s, e) => {
    movement(p, s, e); s['m.TileCollideStyle'] = false;
    vanilla('Terraria.Projectile', 'HandleMovement', actual => { assert.equal(actual.tileCollide, false); });
    pr('HandleMovement', p, vector()); assert.equal(p.tileCollide, true); assert.equal(e.includes('a.TileCollideStyle'), false);
});
test('movement with tileCollide disabled avoids style and vector state', (p, s, e) => {
    p.tileCollide = false; pr('HandleMovement', p, vector()); assert.deepEqual(e, []); assert.equal(p.__projectileMovement, undefined);
});
function draw(p, s, e) {
    vanilla('Terraria.Main', 'DrawProjDirect', (_, actual) => { e.push('native.draw'); pr('GetAlpha', actual, { R: 1 }); });
    vanilla('Terraria.Projectile', 'GetAlpha', (_, color) => { e.push('native.alpha'); return color; });
}
test('draw veto calls every global and post callbacks despite skipping local pre', (p, s, e) => {
    draw(p, s, e); s['a.PreDraw'] = false;
    call('Terraria.Main', 'DrawProjDirect', {}, p, null);
    assert.deepEqual(e, ['a.PreDraw', 'b.PreDraw', 'm.PostDraw', 'a.PostDraw', 'b.PostDraw']);
});
test('draw color ref propagates to local pre, native alpha and all post callbacks', (p, s, e) => {
    draw(p, s, e); const color = { R: 77, G: 88, B: 99, A: 100 };
    s['a.PreDraw'] = (_, ref) => { ref.value = color; };
    s['m.PreDraw'] = (_, actual) => assert.equal(actual, color);
    s['a.GetAlpha'] = (_, actual) => { assert.equal(actual, color); };
    s['b.PostDraw'] = (_, actual) => assert.equal(actual, color);
    call('Terraria.Main', 'DrawProjDirect', {}, p, null);
    assert.deepEqual(e.slice(-3), ['m.PostDraw', 'a.PostDraw', 'b.PostDraw']);
});
test('vetoing grapple sprite still draws chain with native flag restored', (p, s, e) => {
    p.aiStyle = 7; s['a.PreDraw'] = false;
    const key = 'void EntitySpriteDraw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float worthless)';
    Terraria.Main[key]; installed.get('Terraria.Main:' + signature(key)).vanilla = t => e.push(t === chain ? 'chain' : 'sprite');
    vanilla('Terraria.Main', 'DrawProjDirect', () => { Terraria.Main[key](chain); Terraria.Main[key](texture); });
    call('Terraria.Main', 'DrawProjDirect', {}, p, null);
    assert.equal(e.includes('chain'), true); assert.equal(e.includes('sprite'), false); assert.equal(flags.get('proj.split'), false);
});
test('draw context restores after native exception', (p, s, e) => {
    s['a.PreDraw'] = (_, ref) => { ref.value = { R: 42 }; };
    vanilla('Terraria.Main', 'DrawProjDirect', () => { throw Error('draw failure'); });
    assert.throws(() => call('Terraria.Main', 'DrawProjDirect', {}, p, null), /draw failure/);
    vanilla('Terraria.Projectile', 'GetAlpha', (_, color) => color);
    assert.deepEqual(pr('GetAlpha', p, { R: 3 }), { R: 3 });
});
test('grapple last defined global decision overrides local veto', (p, s, e) => {
    s['m.CanUseGrapple'] = false; s['a.CanUseGrapple'] = false; s['b.CanUseGrapple'] = true;
    vanilla('Terraria.Player', 'FireGrapple', () => e.push('native.grapple'));
    call('Terraria.Player', 'FireGrapple', {}, { shoot: 1136 }); assert.equal(e.includes('native.grapple'), true);
});
test('grapple global veto blocks creation', (p, s, e) => {
    s['a.CanUseGrapple'] = true; s['b.CanUseGrapple'] = false;
    vanilla('Terraria.Player', 'FireGrapple', () => e.push('native.grapple'));
    call('Terraria.Player', 'FireGrapple', {}, { shoot: 1 }); assert.equal(e.includes('native.grapple'), false);
});
test('grapple type ref runs in order and restores item on exception', (p, s, e) => {
    s['m.UseGrapple'] = () => 1137;
    s['a.UseGrapple'] = (_, ref) => { assert.equal(ref.value, 1137); ref.value = 2; };
    s['b.UseGrapple'] = (_, ref) => { assert.equal(ref.value, 2); ref.value = 3; };
    const item = { shoot: 1136 };
    vanilla('Terraria.Player', 'FireGrapple', (_, actual) => { assert.equal(actual.shoot, 3); throw Error('grapple failure'); });
    assert.throws(() => call('Terraria.Player', 'FireGrapple', {}, item), /grapple failure/); assert.equal(item.shoot, 1136);
});
test('invalid grapple type safely retains source', (p, s, e) => {
    s['a.UseGrapple'] = (_, ref) => { ref.value = Infinity; };
    vanilla('Terraria.Player', 'FireGrapple', (_, item) => assert.equal(item.shoot, 1));
    call('Terraria.Player', 'FireGrapple', {}, { shoot: 1 });
});
test('grapple can remap to a raw registered type without a ModProjectile class', (p, s, e) => {
    s['a.UseGrapple'] = (_, ref) => { ref.value = 1200; };
    const item = { shoot: 1 };
    vanilla('Terraria.Player', 'FireGrapple', (_, actual) => assert.equal(actual.shoot, 1200));
    call('Terraria.Player', 'FireGrapple', {}, item); assert.equal(item.shoot, 1);
});
test('unregistered grapple type safely retains source', (p, s, e) => {
    s['a.UseGrapple'] = (_, ref) => { ref.value = 999999; };
    vanilla('Terraria.Player', 'FireGrapple', (_, item) => assert.equal(item.shoot, 1));
    call('Terraria.Player', 'FireGrapple', {}, { shoot: 1 });
});
test('latch false vetoes earlier local and global true', (p, s, e) => {
    s['m.GrappleCanLatchOnTo'] = true; s['a.GrappleCanLatchOnTo'] = true; s['b.GrappleCanLatchOnTo'] = false;
    assert.equal(pr('AI_007_GrapplingHooks_CanTileBeLatchedOnTo', p, {}), false);
});
test('nullable latch falls back to native', (p, s, e) => {
    vanilla('Terraria.Projectile', 'AI_007_GrapplingHooks_CanTileBeLatchedOnTo', () => true);
    assert.equal(pr('AI_007_GrapplingHooks_CanTileBeLatchedOnTo', p, {}), true);
});
test('future mod registrations receive existing global hook marks', (p, s, e) => {
    p.type = 1137; p.ModProjectile = new sandbox.Future(); sandbox.registry.Attach(p, true); s['a.CanDamage'] = false;
    pr('Damage', p); assert.deepEqual(e, ['a.CanDamage']);
});
test('global hooks apply to a raw projectile type without a ModProjectile class', (p, s, e) => {
    p.type = 1200; p.ModProjectile = undefined; sandbox.registry.Attach(p, true); s['a.CanDamage'] = false;
    pr('Damage', p); assert.deepEqual(e, ['a.CanDamage']);
});
test('conditional subscribers exclude unrelated vanilla entities', (p, s, e) => {
    p.type = 2; p.ModProjectile = undefined; sandbox.registry.Attach(p, true); vanilla('Terraria.Projectile', 'Colliding', () => true);
    assert.equal(pr('Colliding', p, {}, {}), true); assert.deepEqual(e, []);
});
test('unrelated damage avoids pet tables and damage hitbox avoids copying', (p, s, e) => {
    p.type = 2; p.ModProjectile = undefined; sandbox.registry.Attach(p, true);
    const pets = Main.projPet, box = { X: 1, Y: 2, Width: 3, Height: 4 };
    Object.defineProperty(Main, 'projPet', { configurable: true, get() { throw Error('unnecessary table access'); } });
    try {
        vanilla('Terraria.Projectile', 'Damage', () => e.push('native'));
        pr('Damage', p); assert.deepEqual(e, ['native']);
        vanilla('Terraria.Projectile', 'Damage_GetHitbox', () => box);
        assert.equal(pr('Damage_GetHitbox', p), box);
    } finally { Object.defineProperty(Main, 'projPet', { configurable: true, writable: true, value: pets }); }
});
test('per entity state and subscriber arrays persist across hot calls', (p, s, e) => {
    const other = entity(1), r = sandbox.registry;
    const a = r.Find(p, sandbox.First), b = r.Find(other, sandbox.First);
    const countA = a.count, countB = b.count;
    const list = r.For(p, 'Colliding'); assert.equal(r.For(p, 'Colliding'), list); assert.notEqual(a, b);
    r.Call(p, 'Colliding', {}, {}); assert.equal(a.count, countA + 1); assert.equal(b.count, countB);
});
test('set defaults rebuilds subscribers and per entity state', (p, s, e) => {
    const r = sandbox.registry, a = r.Find(p, sandbox.First); a.count = 50;
    r.Attach(p, true); assert.notEqual(r.Find(p, sandbox.First), a); assert.notEqual(r.Find(p, sandbox.First).count, 50);
});
test('callback failure logs once and continues later subscribers', (p, s, e) => {
    s['a.Colliding'] = () => { throw Error('expected callback error'); }; s['b.Colliding'] = true;
    assert.equal(pr('Colliding', p, {}, {}), true); assert.equal(errors.length, 1); assert.equal(errors[0][0], 'First.Colliding'); errors.length = 0;
});
test('late registration preserves state and invalidates per method cache', (p, s, e) => {
    const r = sandbox.registry, inst = r.Find(p, sandbox.First); inst.count = 50; const prior = r.For(p, 'Colliding');
    vm.runInContext('class Late extends GlobalProjectile { Colliding() { return true; } } GlobalProjectile.register(Late);', context);
    assert.equal(r.Find(p, sandbox.First), inst); assert.equal(inst.count, 50); assert.equal(r.For(p, 'Colliding').length, prior.length + 1);
});
test('registry common item and NPC dispatch preserves order and exception isolation', (p, s, e) => {
    vm.runInContext(`
    for (const kind of ['item', 'npc']) {
        class Base extends GlobalType { Event() {} }
        class A extends Base { Event() { events.push(kind + '.a'); throw Error('expected'); } }
        class B extends Base { Event() { events.push(kind + '.b'); } }
        const r = new GlobalRegistry(Base, () => ({}), kind, 'Get'); r.Register(A, kind); r.Register(B, kind);
        r.Each({ type: 1 }, 'Event', g => g.Event());
    }
    `, context);
    assert.deepEqual(e, ['item.a', 'item.b', 'npc.a', 'npc.b']); assert.equal(errors.length, 2); errors.length = 0;
});
for (const file of ['GlobalItem.js', 'Loaders/GlobalItemLoader.js']) {
    vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
}
vm.runInContext(`
const globalItems = new GlobalRegistry(GlobalItem, () => Terraria.Item, '__itemGlobals', 'GetGlobalItem');
class CloneState extends GlobalItem { InstancePerEntity = true; charge = 0; }
GlobalItem.register(CloneState);
Object.assign(globalThis, { itemRegistry: globalItems, CloneState });
`, context);
test('cloned item preserves state and receives a late global registration', () => {
    const r = sandbox.itemRegistry, item = { type: 1 };
    r.Attach(item, true); const original = r.Find(item, sandbox.CloneState); original.charge = 7;
    vanilla('Terraria.Item', 'Clone', actual => ({ type: actual.type }));
    const copy = call('Terraria.Item', 'Clone', item);
    const cloned = r.Find(copy, sandbox.CloneState);
    assert.notEqual(cloned, original); assert.equal(cloned.charge, 7);
    vm.runInContext('class LateItem extends GlobalItem {} GlobalItem.register(LateItem); globalThis.LateItem = LateItem;', context);
    assert.ok(r.Find(copy, sandbox.LateItem)); assert.ok(r.Find(item, sandbox.LateItem));
    assert.equal(r.Find(copy, sandbox.CloneState), cloned); assert.equal(cloned.charge, 7);
});
assert.deepEqual(missing, []);
vm.runInContext(`class FallStyle extends GlobalProjectile {
    TileCollideStyle(p, width, height, fall) { events.push(fall.value); return true; }
} GlobalProjectile.register(FallStyle);`, context);
for (const [type, aiStyle, expected, fields] of [
    [1, -1, true], [9, -1, false], [24, -1, false], [378, -1, false], [378, 62, true],
    [281, 62, false], [253, 66, false], [1, 197, true], [663, -1, false], [759, -1, true],
    [1020, -1, false, { rotation: 0 }], [1020, -1, true, { rotation: Math.PI }],
    [1, 99, false, { ai: [-2] }], [1, 99, true, { ai: [-1] }],
    [500, -1, true], [500, -1, false, { position: vector(32, 50) }],
    [9, -1, true, { decidesManualFallThrough: true, shouldFallThrough: true }],
]) {
    test('vanilla platform default type=' + type + ' ai=' + aiStyle + ' fields=' + JSON.stringify(fields || {}), (p, s, e) => {
        p.type = type; p.aiStyle = aiStyle; p.ModProjectile = undefined; Object.assign(p, fields);
        vanilla('Terraria.Projectile', 'GetCollisionParams', (_, a, w, h) => { a.value = vector(.5, .5); w.value = h.value = 8; });
        pr('HandleMovement', p, vector()); assert.equal(e.find(v => typeof v === 'boolean'), expected);
    });
}
console.log(checks + ' behavior checks passed; ' + [...installed.values()].filter(e => e.hooks.length).length + ' native signatures verified.');
