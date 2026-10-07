import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'app/src/main/cpp/script/js/mod');
export const FIRST_NPC = 697;
export const AI = 'void AI()';

export function createRuntime(reverse = false) {
    const hooks = new Map();
    const flags = new Map();
    const marks = new Map();
    const definitions = new Map();
    const objects = new Map();
    const errors = [];
    const ready = [];
    let address = 1;

    function entry(signature) {
        if (!hooks.has(signature)) hooks.set(signature, { hooks: [], entered: 0, vanilla() {} });
        return hooks.get(signature);
    }

    function invoke(method, args, index = 0) {
        if (index === method.hooks.length) return method.vanilla(...args);

        const { callback, filter } = method.hooks[index];
        const original = (...next) => invoke(method, next.length ? next : args, index + 1);
        const type = args[0]?.type;

        if (filter.minType !== undefined && type < filter.minType ||
            filter.flag && !flags.get(filter.flag) ||
            filter.marks && !marks.get(filter.marks)?.has(type)) return original();

        method.entered++;
        return callback(original, ...args);
    }

    const nativeNPC = new Proxy({}, {
        get(target, signature) {
            if (signature in target) return target[signature];

            const method = entry(signature);
            const fn = (...args) => invoke(method, args);
            fn.hook = (callback, filter = {}) => {
                const hook = { callback, filter };
                if (reverse) method.hooks.unshift(hook);
                else method.hooks.push(hook);
            };
            return fn;
        },
    });
    const sandbox = {
        FIRST_NPC,
        SceneEffectPriority: { BossLow: 0 },
        Terraria: { NPC: nativeNPC, Item: {}, Projectile: {}, Main: { npc: [], netMode: 0 } },
        GoreLoader: { Autoload() {} },
        ModFiles: { ContentTexture: inst => inst.Texture, Texture: value => value },
        Lang: { Localized: () => '', Follow() {} },
        ModMusic: { TrackNpc() {} },
        ModNet: { InstallEntity() {} },
        Ready: { Add: fn => ready.push(fn) },
        TownNPCLoader: { Hook() {}, MoodTexts: () => [], LookFiles: () => [] },
        bl: {
            mod: { name: 'AIType test' },
            defineField() {},
            defineMethod(cls, name, fn) { cls[name] = fn; },
            addressOf(entity) { return entity.__address; },
            objectAt(pointer) { return objects.get(pointer); },
            error(message) { errors.push(message); },
            log() {},
            hookFlags: { set: (key, value) => flags.set(key, value) },
            hookMarks: {
                set(key, type) {
                    if (!marks.has(key)) marks.set(key, new Set());
                    marks.get(key).add(type);
                },
            },
            npcs: {
                register(def) {
                    const type = FIRST_NPC + definitions.size;
                    definitions.set(type, def);
                    return type;
                },
            },
        },
    };
    const context = vm.createContext(sandbox);
    const run = text => vm.runInContext(text, context);

    for (const file of [
        'Core/Hooks.js', 'Core/Safe.js', 'Core/Templates.js', 'Core/Entities.js',
        'Core/GlobalType.js', 'Core/GlobalRegistry.js', 'GlobalNPC.js',
        'ModNPC.js', 'Loaders/NPCLoader.js', 'Loaders/GlobalNPCLoader.js',
    ]) {
        vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
    }

    run("const globalNPCs = new GlobalRegistry(GlobalNPC, () => Terraria.NPC, '__globalNPCs', 'GetGlobalNPC');");

    function npc(type, defaults = true) {
        const entity = { type, netID: type, aiStyle: -1, ai: [0, 0, 0, 0], active: true, __address: address++ };
        objects.set(entity.__address, entity);
        if (defaults) definitions.get(type)?.setDefaults(entity);
        return entity;
    }

    const api = run('({ ModNPC, NPCLoader, GlobalNPC, globalNPCs })');
    return {
        run, npc, api, entry, flags, hooks, errors, definitions,
        call: npc => nativeNPC[AI](npc),
        vanilla(fn) { entry(AI).vanilla = fn; },
        get mode() { return sandbox.Terraria.Main.netMode; },
        set mode(value) { sandbox.Terraria.Main.netMode = value; },
    };
}
