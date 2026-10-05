import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'app/src/main/cpp/script/js/mod');
const hooks = new Map(), marks = new Map(), flags = new Map(), errors = [];
const vector = (X = 0, Y = 0) => ({ X, Y });
const native = new Proxy({}, {
    get(_, signature) {
        if (!hooks.has(signature)) hooks.set(signature, { callbacks: [], vanilla() {} });
        const entry = hooks.get(signature);
        const method = (...args) => {
            if (entry.depth) return entry.vanilla(...args);
            entry.depth = 1;
            try { return invoke(entry, args); }
            finally { entry.depth = 0; }
        };
        method.hook = (callback, filter = {}) => entry.callbacks.push({ callback, filter });
        return method;
    },
});
function invoke(entry, args, index = 0) {
    if (index === entry.callbacks.length) return entry.vanilla(...args);
    const { callback, filter } = entry.callbacks[index];
    const original = (...next) => invoke(entry, next, index + 1);
    if (filter.marks && !marks.get(filter.marks)?.has(args[0].type)) return original(...args);
    if (filter.flag && !flags.get(filter.flag)) return original(...args);
    return callback(original, ...args);
}
const call = (signature, ...args) => native[signature](...args);
const vanilla = (signature, fn) => { native[signature]; hooks.get(signature).vanilla = fn; };
const Main = { player: [{ position: vector(), height: 40 }], projPet: [], projectile: [] };
const sandbox = {
    Terraria: { Projectile: native, Main, Collision: { 'bool SolidCollision(Vector2 Position, int Width, int Height)': () => true } },
    Vector2: { new: vector, Clone: ({ X, Y }) => vector(X, Y), Add: (a, b) => vector(a.X + b.X, a.Y + b.Y) },
    Entities: { InstanceOf: (p) => p.ModProjectile, Of: (m) => m.projectile, Define() {} },
    globalProjectiles: { For() { return []; }, AllCall() { return true; }, Call() {} },
    FIRST_PROJECTILE: 1136,
    bl: { hookMarks: { set(key, type) { if (!marks.has(key)) marks.set(key, new Set()); marks.get(key).add(type); } },
        hookFlags: { set(key, value) { flags.set(key, value); } } },
    Safe: {
        Run(label, fn) { try { return fn(); } catch (error) { errors.push([label, error]); } },
        Report(label, error) { errors.push([label, error]); },
    },
};
const context = vm.createContext(sandbox);
for (const file of ['Core/Hooks.js', 'ModProjectile.js', 'Loaders/ProjectileLoader.js']) {
    vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
}
vm.runInContext(`
    class Probe extends ModProjectile {
        OnTileCollide(p, velocity) { return this.tile(p, velocity); }
        PreKill(p, timeLeft) { return this.pre(p, timeLeft); }
        OnKill(p, timeLeft) { this.kill(p, timeLeft); }
    }
    ProjectileLoader.Hook(Probe, FIRST_PROJECTILE);
    globalThis.Probe = Probe;
`, context);
const UPDATE = 'void Update(int i)', MOVE = 'void HandleMovement(Vector2 wetVelocity)', POSITION = 'void UpdatePosition(Vector2 wetVelocity)', KILL = 'void Kill()';
const make = () => {
    const m = new sandbox.Probe(), p = { type: 1136, active: true, tileCollide: true, timeLeft: 60, penetrate: 1,
        position: vector(32, 16), velocity: vector(0, 1), width: 8, height: 8, ModProjectile: m };
    m.projectile = p;
    p[KILL] = () => call(KILL, p);
    m.tile = () => false; m.pre = () => true; m.kill = () => {};
    return p;
};
let checks = 0;
function test(label, run) {
    for (const entry of hooks.values()) entry.vanilla = () => {};
    const p = make(), events = [];
    p.ModProjectile.tile = (_, velocity) => { events.push(['tile', velocity.Y]); return false; };
    p.ModProjectile.pre = (_, time) => { events.push(['pre', time]); return true; };
    p.ModProjectile.kill = (_, time) => { events.push(['kill', time]); };
    vanilla(KILL, (actual) => { events.push(['native']); actual.active = false; });
    run(p, events);
    assert.deepEqual(errors, [], label + ': callback errors');
    checks++;
    console.log(label + ': ok');
}
test('timeout after a blocked tile collision still kills', (p, events) => {
    vanilla(MOVE, (actual) => { actual.velocity.Y = 0; call(KILL, actual); });
    vanilla(UPDATE, (actual) => { call(MOVE, actual, vector()); actual.timeLeft = 0; call(KILL, actual); });
    call(UPDATE, p, 0);
    assert.equal(p.active, false);
    assert.deepEqual(events, [['tile', 1], ['pre', 0], ['kill', 0], ['native']]);
});
test('NPC penetration death near a tile still kills', (p, events) => {
    vanilla(UPDATE, (actual) => { actual.penetrate = 0; call(KILL, actual); });
    call(UPDATE, p, 0);
    assert.equal(p.active, false);
    assert.deepEqual(events, [['pre', 60], ['kill', 60], ['native']]);
});
test('explicit AI death near a tile still kills', (p, events) => {
    vanilla(UPDATE, (actual) => call(KILL, actual));
    call(UPDATE, p, 0);
    assert.equal(p.active, false);
    assert.deepEqual(events, [['pre', 60], ['kill', 60], ['native']]);
});
test('tile veto preserves projectile and collision position', (p, events) => {
    vanilla(MOVE, (actual) => { actual.velocity.Y = 0; actual.position.Y += 10; call(KILL, actual); });
    call(MOVE, p, vector());
    assert.equal(p.active, true); assert.equal(p.position.Y, 16);
    assert.deepEqual(events, [['tile', 1]]);
});
test('tile permission proceeds to normal death', (p, events) => {
    p.ModProjectile.tile = () => { events.push(['tile']); return true; };
    vanilla(MOVE, (actual) => { actual.velocity.Y = 0; call(KILL, actual); });
    call(MOVE, p, vector());
    assert.equal(p.active, false);
    assert.deepEqual(events, [['tile'], ['pre', 60], ['kill', 60], ['native']]);
});
test('explicit death inside OnTileCollide does not recurse', (p, events) => {
    p.ModProjectile.tile = () => { events.push(['tile']); call(KILL, p); return false; };
    vanilla(MOVE, (actual) => { actual.velocity.Y = 0; call(KILL, actual); });
    call(MOVE, p, vector());
    assert.equal(p.active, false);
    assert.deepEqual(events, [['tile'], ['pre', 60], ['kill', 60], ['native']]);
});
test('OnTileCollide returning true after explicit death does not duplicate OnKill', (p, events) => {
    p.ModProjectile.tile = () => { events.push(['tile']); call(KILL, p); return true; };
    vanilla(MOVE, (actual) => { actual.velocity.Y = 0; call(KILL, actual); });
    call(MOVE, p, vector());
    assert.equal(p.active, false);
    assert.deepEqual(events, [['tile'], ['pre', 60], ['kill', 60], ['native']]);
});
test('Kill inside movement without a collision remains a normal death', (p, events) => {
    vanilla(MOVE, (actual) => call(KILL, actual));
    call(MOVE, p, vector());
    assert.equal(p.active, false);
    assert.deepEqual(events, [['pre', 60], ['kill', 60], ['native']]);
});
test('PreKill veto deactivates without OnKill', (p, events) => {
    p.ModProjectile.pre = () => { events.push(['pre']); return false; };
    call(KILL, p);
    assert.equal(p.active, false); assert.deepEqual(events, [['pre']]);
});
test('explicit Kill outside movement ignores adjacent tiles', (p, events) => {
    call(KILL, p);
    assert.equal(p.active, false);
    assert.deepEqual(events, [['pre', 60], ['kill', 60], ['native']]);
});
test('surviving collision reports once per movement', (p, events) => {
    vanilla(MOVE, (actual) => { actual.velocity.Y = 0; call(KILL, actual); call(POSITION, actual, vector()); });
    call(MOVE, p, vector());
    assert.equal(p.active, true); assert.deepEqual(events, [['tile', 1]]);
});

test('slope correction after UpdatePosition reports collision without a native Kill', (p, events) => {
    vanilla(MOVE, (actual) => { call(POSITION, actual, vector()); actual.velocity.Y = 0; });
    call(MOVE, p, vector());
    assert.equal(p.active, true);
    assert.deepEqual(events, [['tile', 1]]);
});
test('a second Kill on an inactive projectile does not repeat callbacks', (p, events) => {
    call(KILL, p); call(KILL, p);
    assert.equal(p.active, false);
    assert.deepEqual(events, [['pre', 60], ['kill', 60], ['native'], ['native']]);
});
test('movement state restores after native failure', (p, events) => {
    vanilla(MOVE, () => { throw new Error('movement failed'); });
    assert.throws(() => call(MOVE, p, vector()), /movement failed/);
    assert.equal(p.ModProjectile.__moving, undefined);
    call(KILL, p);
    assert.equal(p.active, false);
    assert.deepEqual(events, [['pre', 60], ['kill', 60], ['native']]);
});
const globalEvents = [];
sandbox.globalProjectiles = {
    For() { return []; },
    AllCall(_, method) { if (method === 'PreKill') globalEvents.push('pre'); return true; },
    Call(_, method) { globalEvents.push(method); },
};
vm.runInContext('class GlobalType {}', context);
for (const file of ['GlobalProjectile.js', 'Loaders/GlobalProjectileLoader.js']) {
    vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
}
vm.runInContext(`
    class GlobalProbe extends GlobalProjectile { OnKill() {} }
    GlobalProjectileLoader.Hook(GlobalProbe);
`, context);
for (const order of ['mod first', 'global first']) {
    if (order === 'global first') hooks.get(KILL).callbacks.reverse();
    test('global kill notifications wait for actual death: ' + order, (p, events) => {
        globalEvents.length = 0;
        vanilla(MOVE, (actual) => { actual.velocity.Y = 0; call(KILL, actual); });
        call(MOVE, p, vector());
        assert.equal(p.active, true); assert.deepEqual(globalEvents, []);
        p.timeLeft = 0;
        call(KILL, p);
        assert.equal(p.active, false); assert.deepEqual(globalEvents, ['pre', 'OnKill']);
        assert.deepEqual(events, [['tile', 1], ['pre', 0], ['kill', 0], ['native']]);
    });
}
console.log(checks + ' projectile lifecycle checks passed.');
