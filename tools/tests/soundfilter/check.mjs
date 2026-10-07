import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'app/src/main/cpp/script/js/mod');
let checks = 0;

function check(name, test) {
    test();
    checks++;
    console.log('PASS ' + name);
}

function runtime() {
    const hooks = [];
    const marks = new Map();
    const installed = new Set();
    const plays = [];
    const stops = [];
    const loads = [];
    const originalCalls = [];
    const errors = [];
    const active = new Map();
    const states = new Map();
    let now = 1000;
    let stream = 20;
    let entries = 0;
    let fail = false;
    const signature = 'SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)';

    function invoke(index, args) {
        if (index === hooks.length) {
            originalCalls.push(args);
            return { original: true, args };
        }

        const { callback, filter, soundHook } = hooks[index];
        const original = (...next) => invoke(index + 1, next);
        const type = args[1 + (filter.arg ?? 0)];
        if (filter.minType !== undefined && type < filter.minType ||
            filter.marks && !marks.get(filter.marks)?.has(type)) return original(...args);

        if (soundHook) entries++;
        return callback(original, ...args);
    }

    const native = {
        [signature]: { hook: (callback, filter = {}) => hooks.push({ callback, filter, soundHook: true }) },
        SoundAttenuationDistance: 2500,
    };
    const context = vm.createContext({
        Date: { now: () => now },
        Math: Object.assign(Object.create(Math), { random: () => 0.5 }),
        Hooks: { Once(key, callback) {
            if (installed.has(key)) return;
            installed.add(key);
            callback();
        } },
        Safe: {
            Run(label, callback) {
                try { return callback(); }
                catch (error) { errors.push(error); }
            },
            Once: (key, message) => errors.push(message),
        },
        ModFiles: { Audio: (kind, base, file) => file.includes('missing') ? null : '/fixture/' + file.replace(/\.wav$/, ''), AUDIO: ['wav'] },
        SoundLimitBehavior: { ReplaceOldest: 0, IgnoreNew: 1 },
        Terraria: {
            Main: { soundVolume: 1, screenPosition: { X: 0, Y: 0 }, screenWidth: 1000, screenHeight: 600 },
            Audio: {
                LegacySoundPlayer: native,
                LegacySoundStyle: { new: () => ({
                    ['void .ctor(int soundId, int style, SoundType type, int maxTrackedInstances)'](id, style) {
                        this.SoundId = id;
                        this.Style = style;
                    },
                }) },
                SoundEngine: { [signature]: (...args) => invoke(0, [null, ...args]) },
            },
        },
        bl: {
            hookMarks: { set(key, type) {
                if (!marks.has(key)) marks.set(key, new Set());
                marks.get(key).add(type);
            } },
            sounds: {
                load(file) { loads.push(file); return loads.length; },
                enqueue(group, id, left, right, rate, max, ignore) {
                    const live = [...active.entries()].filter(([, p]) => p.group === group && p.end > now && states.get(p.handle) === 2);
                    if (fail || max > 0 && live.length >= max && ignore) return 0;
                    if (max > 0 && live.length >= max) {
                        const previous = live[0][0];
                        stops.push(previous);
                        states.set(previous, 4);
                    }
                    plays.push([id, left, right, rate]);
                    const handle = stream++;
                    states.set(handle, 2);
                    active.set(handle, { handle, group, end: now + 100 / Math.max(0.5, Math.min(2, rate)) });
                    return handle;
                },
                cancel(id) { stops.push(id); states.set(id, 4); },
                playbackState: id => states.get(id) || 0,
                active: group => [...active.values()].filter(p => p.group === group && p.end > now && states.get(p.handle) === 2).at(-1)?.handle || 0,
            },
        },
    });

    for (const file of ['Loaders/SoundLoader.js', 'SoundStyle.js', 'SoundEngine.js']) {
        vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
    }
    const api = vm.runInContext('({ SoundLoader, SoundStyle, SoundEngine })', context);
    const of = style => api.SoundLoader.Sounds[style.Style];
    const newStyle = options => new api.SoundStyle('Sounds/Pulse', options);
    return { ...api, newStyle, of, plays, stops, loads, originalCalls, errors, hooks, marks, states,
        call: (type, style = 0, ...tail) => invoke(0, [{ receiver: true }, type, -1, -1, style, ...(tail.length ? tail : [1, 0])]),
        advance: ms => { now += ms; },
        hook(callback, first = false) {
            const entry = { callback, filter: {}, soundHook: false };
            if (first) hooks.unshift(entry);
            else hooks.push(entry);
        },
        get entries() { return entries; },
        set fail(value) { fail = value; },
    };
}

check('installation is lazy and runs once across styles', () => {
    const r = runtime();
    assert.equal(r.hooks.length, 0);
    r.newStyle();
    r.newStyle({ Pitch: 1 });
    r.SoundLoader.Install();
    assert.equal(r.hooks.length, 1);
    assert.equal(r.hooks[0].filter.arg, 0);
    assert.equal(r.hooks[0].filter.minType, 1000);
    assert.ok(r.marks.get(r.hooks[0].filter.marks).has(1000));
});

check('10000 vanilla requests bypass the sound JS callback', () => {
    const r = runtime();
    r.newStyle();
    for (let i = 0; i < 10000; i++) assert.ok(r.call(i % 1000).original);
    assert.equal(r.entries, 0);
    assert.equal(r.originalCalls.length, 10000);
    assert.equal(r.plays.length, 0);
});

check('negative and unmarked higher types reach the original', () => {
    const r = runtime();
    r.newStyle();
    for (const type of [-2147483648, -1, 1001, 65535, 65536, 2147483647]) {
        assert.equal(r.call(type).args[1], type);
    }
    assert.equal(r.entries, 0);
});

check('vanilla arguments and receiver are preserved', () => {
    const r = runtime();
    r.newStyle();
    const result = r.call(12, 3, 0.6, -0.25);
    assert.deepEqual(result.args, [{ receiver: true }, 12, -1, -1, 3, 0.6, -0.25]);
});

check('mixed routing enters JS only for the marker', () => {
    const r = runtime();
    const style = r.newStyle({ MaxInstances: 0 });
    for (let i = 0; i < 100; i++) r.call(i);
    for (let i = 0; i < 12; i++) assert.equal(r.call(1000, style.Style), null);
    assert.equal(r.entries, 12);
    assert.equal(r.originalCalls.length, 100);
    assert.equal(r.plays.length, 12);
});

check('unknown marker style remains silent', () => {
    const r = runtime();
    r.newStyle();
    assert.equal(r.call(1000, 9999), null);
    assert.equal(r.plays.length, 0);
    assert.equal(r.originalCalls.length, 0);
});

for (const first of [false, true]) {
    check('independent raw hook remains active, first=' + first, () => {
        const r = runtime();
        const style = r.newStyle({ MaxInstances: 0 });
        const seen = [];
        r.hook((original, ...args) => { seen.push(args[1]); return original(...args); }, first);
        r.call(12);
        r.call(1000, style.Style);
        assert.deepEqual(seen, first ? [12, 1000] : [12]);
        assert.equal(r.entries, 1);
        assert.equal(r.originalCalls.length, 1);
        assert.equal(r.plays.length, 1);
    });
}

check('direct custom API keeps its existing path without a native marker call', () => {
    const r = runtime();
    const style = r.newStyle({ MaxInstances: 0 });
    for (let i = 0; i < 12; i++) assert.ok(r.SoundEngine.PlaySound(style) > 0);
    assert.equal(r.entries, 0);
    assert.equal(r.plays.length, 12);
});

check('volume, pitch and pan remain correct', () => {
    const r = runtime();
    const style = r.newStyle({ Volume: 0.5, Pitch: 1 });
    r.SoundEngine.PlaySound(style, { X: 750, Y: 300 });
    assert.deepEqual(r.plays[0], [1, 0.225, 0.45, 2]);
});

check('native marker volumeScale and pitchOffset remain correct', () => {
    const r = runtime();
    const style = r.newStyle({ Volume: 0.5, Pitch: 0.5 });
    r.call(1000, style.Style, 0.5, 0.5);
    assert.deepEqual(r.plays[0], [1, 0.25, 0.25, 2]);
});

check('far away and zero volume do not play', () => {
    const r = runtime();
    const style = r.newStyle();
    assert.equal(r.SoundEngine.PlaySound(style, { X: 3000, Y: 300 }), 0);
    assert.equal(r.SoundEngine.PlaySound(r.newStyle({ Volume: 0 })), 0);
    assert.equal(r.plays.length, 0);
});

check('IgnoreNew preserves the active stream', () => {
    const r = runtime();
    const style = r.newStyle({ SoundLimitBehavior: 1 });
    const first = r.SoundEngine.PlaySound(style);
    assert.ok(first > 0);
    assert.equal(r.SoundEngine.PlaySound(style), 0);
    assert.equal(r.SoundEngine.FindActiveSound(style), first);
    assert.equal(r.plays.length, 1);
});

check('ReplaceOldest stops the previous stream', () => {
    const r = runtime();
    const style = r.newStyle();
    const first = r.SoundEngine.PlaySound(style);
    const second = r.SoundEngine.PlaySound(style);
    assert.deepEqual(r.stops, [first]);
    assert.equal(r.SoundEngine.FindActiveSound(style), second);
});

check('unlimited instances preserve independent handles', () => {
    const r = runtime();
    const style = r.newStyle({ MaxInstances: 0 });
    const ids = new Set(Array.from({ length: 100 }, () => r.SoundEngine.PlaySound(style)));
    assert.equal(ids.size, 100);
    assert.equal(r.stops.length, 0);
});

check('queue admission failure stays zero and does not create an active instance', () => {
    const r = runtime();
    const style = r.newStyle();
    r.fail = true;
    assert.equal(r.SoundEngine.PlaySound(style), 0);
    assert.equal(r.SoundEngine.FindActiveSound(style), 0);
    assert.equal(r.SoundEngine.GetSoundState(0), 0);
});

check('pending and failed handles are exposed without claiming active playback', () => {
    const r = runtime();
    const style = r.newStyle();
    const h = r.SoundEngine.PlaySound(style);
    r.states.set(h, 1);
    assert.equal(r.SoundEngine.GetSoundState(h), 1);
    assert.equal(r.SoundEngine.FindActiveSound(style), 0);
    r.states.set(h, -1);
    assert.equal(r.SoundEngine.GetSoundState(h), -1);
    assert.equal(r.SoundEngine.FindActiveSound(style), 0);
});

check('FindActiveSound expiration and explicit stop remain unchanged', () => {
    const r = runtime();
    const style = r.newStyle();
    const id = r.SoundEngine.PlaySound(style);
    r.SoundEngine.StopSound(id);
    assert.deepEqual(r.stops, [id]);
    r.advance(101);
    assert.equal(r.SoundEngine.FindActiveSound(style), 0);
});

check('style and audio loading caches remain intact', () => {
    const r = runtime();
    const a = r.newStyle();
    assert.equal(r.newStyle(), a);
    assert.notEqual(r.newStyle({ Pitch: 1 }), a);
    assert.equal(r.loads.length, 1);
});

check('missing file produces a silent marker', () => {
    const r = runtime();
    const style = new r.SoundStyle('Sounds/missing');
    assert.equal(style.SoundId, 1000);
    assert.equal(r.SoundEngine.PlaySound(style), 0);
    assert.equal(r.plays.length, 0);
});

console.log('soundfilter: ' + checks + ' verificacoes passaram (ponte simulada).');
