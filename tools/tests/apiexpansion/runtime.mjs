import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

export const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'app/src/main/cpp/script/js/mod');
const signatures = new Map();
const canonical = text => text.replace(/\s*=\s*[^,)]+/g, '').replace(/\b(?:public|private|static|internal|override|virtual|sealed|protected)\s+/g, '').replace(/\s+/g, ' ').trim();
let namespace = '', owner = '';
for (const line of fs.readFileSync(path.join(root, 'refs/dump.cs'), 'utf8').split(/\r?\n/)) {
    if (line.startsWith('// Namespace:')) namespace = line.slice(13).trim();
    const cls = line.match(/^(?:public|private|internal|protected).*?\b(?:class|struct)\s+([^\s:<]+)/);
    if (cls) {
        owner = (namespace ? namespace + '.' : '') + cls[1];
        if (owner.startsWith('ItemID.')) owner = 'Terraria.ID.' + owner;
        if (owner.startsWith('BannerSystem.')) owner = 'Terraria.GameContent.' + owner;
    }
    if (/^\s+(?:public|private|internal|protected).*\([^)]*\).*\{ \}/.test(line))
        signatures.set(owner + ':' + canonical(line.trim().split(' {')[0]), /\bstatic\b/.test(line));
}

export function createRuntime() {
    const methods = new Map(), classes = new Map(), marks = new Map(), flags = new Map(), stages = new Map();
    const errors = [], objects = new Map(), ready = [], systems = [], events = [];
    let address = 1;
    const invoke = (entry, args, at = 0) => {
        if (at >= entry.hooks.length || entry.active) return entry.vanilla(...args);
        const { callback, filter } = entry.hooks[at];
        const first = entry.static ? 0 : 1;
        const object = args[filter.on >= 0 ? first + filter.on : 0];
        const type = filter.arg >= 0 ? args[first + filter.arg] : (filter.field || 'type').split('.').reduce((v, key) => v?.[key], object);
        const original = (...next) => at + 1 >= entry.hooks.length ? entry.vanilla(...next) : invoke(entry, next, at + 1);
        const markedType = filter.firstByte >= 0 ? args[first + filter.firstByte]?.[0] : type;
        if (filter.minType !== undefined && markedType < filter.minType || filter.marks && !marks.get(filter.marks)?.has(markedType)
            || filter.flag && !flags.get(filter.flag) || filter.whileIn && !filter.whileIn.entry.active) return original(...args);
        entry.active++;
        try { return callback(original, ...args); } finally { entry.active--; }
    };
    const native = name => {
        if (classes.has(name)) return classes.get(name);
        const value = new Proxy({}, { get(target, key) {
            if (key in target) return target[key];
            if (key === 'new') return () => ({});
            if (typeof key !== 'string') return undefined;
            if (!key.includes('(')) return native(name + '.' + key);
            const id = name + ':' + canonical(key);
            assert.ok(signatures.has(id), 'native signature and parameter names: ' + id);
            if (!methods.has(id)) methods.set(id, { id, static: signatures.get(id), hooks: [], active: 0, vanilla() {} });
            const entry = methods.get(id);
            const fn = (...args) => invoke(entry, args);
            fn.hook = (callback, filter = {}) => entry.hooks.push({ callback, filter });
            fn.entry = entry;
            return fn;
        } });
        classes.set(name, value);
        return value;
    };
    const array = (size, fill = 0) => {
        const value = Array(size).fill(fill);
        Object.defineProperty(value, 'cloneResized', { value: length => {
            const copy = array(length, fill);
            for (let i = 0; i < Math.min(length, value.length); i++) copy[i] = value[i];
            return copy;
        } });
        return value;
    };
    const Terraria = native('Terraria');
    Terraria.DataStructures.GameDifficultyLevel.Classic = 1;
    const Main = Terraria.Main, B = Terraria.GameContent.BannerSystem;
    Main.spriteBatch = { batch: true };
    Main.npcFrameCount = array(700);
    Main.SceneMetrics = { NPCBannerBuff: array(293, false), hasBanner: false };
    Main.PlayerSceneMetrics = { NPCBannerBuff: array(293, false) };
    B.MaxBannerTypes = 293;
    B.killCount = array(293); B.claimableBanners = array(293); B.newBanner = array(293, false); B.bannerState = 0;
    Terraria.ID.ItemID.Sets.BannerStrength = [];
    const sandbox = {
        Terraria, FIRST_ITEM: 6196, FIRST_NPC: 697, SceneEffectPriority: { BossLow: 0 },
        Ref: class { constructor(value) { this.value = value; } },
        ModFiles: {}, CombatLoader: { WantNPC() {} }, TownNPCLoader: { Hook() {} }, ModNet: { InstallEntity() {} },
        Ready: { Add: (fn, phase) => ready.push({ fn, phase }) },
        SystemLoader: { Add: (inst, key) => systems.push({ inst, key }), Hook() {} },
        bl: {
            mod: { uuid: 'mod-a', name: 'Mod A' }, defineField() {}, defineMethod(cls, name, fn) { cls[name] = fn; },
            addressOf: obj => obj?.__address, objectAt: pointer => objects.get(pointer),
            error: msg => errors.push(msg), log: msg => events.push(msg),
            hookFlags: { set: (key, val) => flags.set(key, val) },
            hookMarks: { set(key, type) { if (!marks.has(key)) marks.set(key, new Set()); marks.get(key).add(type); } },
            installItemDrawHooks: (kind, pre, post) => { assert.ok(!stages.has(kind)); stages.set(kind, { pre, post }); }
        }
    };
    const context = vm.createContext(sandbox), run = code => vm.runInContext(code, context);
    for (const file of ['Core/Hooks.js', 'Core/Safe.js', 'Core/Templates.js', 'Core/Entities.js',
        'Core/GlobalType.js', 'Core/GlobalRegistry.js', 'ModItem.js', 'GlobalItem.js', 'GlobalNPC.js',
        'ModNPC.js', 'Loaders/NPCLoader.js', 'ModSystem.js', 'Loaders/BannerLoader.js',
        'ModTile.js', 'Loaders/TileLoader.js', 'ModBannerTile.js', 'Loaders/ItemDrawLoader.js'])
        vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
    run(`const globalItems = new GlobalRegistry(GlobalItem, () => Terraria.Item, '__globalItems', 'GetGlobalItem');
        const globalNPCs = new GlobalRegistry(GlobalNPC, () => Terraria.NPC, '__globalNPCs', 'GetGlobalNPC');
        class ItemLoader { static ByType = new Map(); static Of(item) { return item.ModItem; } }`);
    const api = run('({ ModItem, GlobalItem, ModNPC, GlobalNPC, ItemLoader, ItemDrawLoader, NPCLoader, BannerLoader, ModBannerTile, TileLoader, Entities, globalItems, globalNPCs })');
    function entity(type, mod) {
        const obj = { type, netID: type, __address: address++, playerInteraction: [true, false, true], ModNPC: mod,
            GetAlpha: light => ({ ...light, alpha: true }), GetColor: light => ({ ...light, item: true }) };
        objects.set(obj.__address, obj);
        if (mod instanceof api.ModNPC) api.Entities.Bind(mod, obj, 'ModNPC');
        else if (mod) { obj.ModItem = mod; api.Entities.Bind(mod, obj, 'ModItem'); }
        obj['void SetDefaults(int Type, NPCSpawnParams spawnparams)'] = (type, params) => {
            events.push({ type, params });
            obj.type = obj.netID = type; obj.ModNPC = undefined; obj.__globalNPCs = undefined;
            obj.playerInteraction.fill(false); obj.damage = type === 1 ? 7 : 0;
        };
        return obj;
    }
    const method = (cls, name) => {
        const all = [...methods.values()].filter(m => m.id.startsWith(cls + ':') && m.id.slice(0, m.id.indexOf('(')).endsWith(' ' + name));
        assert.equal(all.length, 1, cls + '.' + name);
        return all[0];
    };
    return { run, api, native, array, entity, sandbox, Terraria, B, Main, stages, flags, marks, errors, ready, systems, events, methods,
        method,
        vanilla(cls, name, fn) { method(cls, name).vanilla = fn; },
        call(cls, name, ...args) { return invoke(method(cls, name), args); }
    };
}
