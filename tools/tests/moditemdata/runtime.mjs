import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

export const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'app/src/main/cpp/script/js/mod');
const dump = fs.readFileSync(path.join(root, 'refs/dump.cs'), 'utf8');
const signatures = new Map();
let owner = '',
    namespace = '';

function canonical(text) {
    return text
        .replace(/\s*=\s*[^,)]+/g, '')
        .replace(/\b(?:public|private|static|internal|override|virtual|sealed|protected)\s+/g, '')
        .replace(/\s+/g, ' ')
        .trim();
}
for (const line of dump.split(/\r?\n/)) {
    if (line.startsWith('// Namespace:')) namespace = line.slice(13).trim();
    const cls = line.match(/^(?:public|private|internal|protected).*?\b(?:class|struct)\s+([^\s:<]+)/);
    if (cls) {
        owner = (namespace ? namespace + '.' : '') + cls[1];
        if (owner.startsWith('PlayerItemSlotID.')) owner = 'Terraria.ID.' + owner;
    }
    if (/^\s+(?:public|private|internal|protected).*\([^)]*\).*\{ \}/.test(line)) {
        const signature = canonical(line.trim().split(' {')[0]);
        signatures.set(owner + ':' + signature, /\bstatic\b/.test(line));
    }
}

export function createRuntime(mode = 0) {
    const installed = new Map(),
        namespaces = new Map(),
        objects = new Map(),
        definitions = new Map();
    const errors = [],
        sent = [],
        fields = [],
        calls = [];
    let address = 1,
        nextType = 6196,
        clock = 1000;
    const invoke = (entry, args, at = 0) => {
        if (at === entry.hooks.length) return entry.vanilla(...args);
        const { callback, filter } = entry.hooks[at];
        const type = filter.arg >= 0 ? args[filter.arg] : args[0]?.type;
        const original = (...next) => invoke(entry, next, at + 1);
        if (filter.minType !== undefined && type < filter.minType) return original(...args);
        return callback(original, ...args);
    };
    function native(name) {
        if (namespaces.has(name)) return namespaces.get(name);
        const proxy = new Proxy(
            {},
            {
                get(target, key) {
                    if (key in target) return target[key];
                    if (key === 'new') return () => (name === 'Terraria.Item' ? item(0) : {});
                    if (typeof key !== 'string') return undefined;
                    if (!key.includes('(')) return native(name + '.' + key);
                    const id = name + ':' + canonical(key);
                    assert.ok(signatures.has(id), 'native signature exists with exact parameter names: ' + id);
                    if (!installed.has(id)) installed.set(id, { id, hooks: [], vanilla() {} });
                    const entry = installed.get(id);
                    const fn = (...args) => invoke(entry, args);
                    fn.hook = (callback, filter = {}) => entry.hooks.push({ callback, filter });
                    fn.entry = entry;
                    return fn;
                }
            }
        );
        namespaces.set(name, proxy);
        return proxy;
    }
    function item(type, defaults = true) {
        const value = new Proxy(
            { type, stack: type ? 1 : 0, __address: address++ },
            {
                get(target, key) {
                    if (typeof key === 'string' && key.includes('(')) return (...args) => native('Terraria.Item')[key](value, ...args);
                    return target[key];
                }
            }
        );
        objects.set(value.__address, value);
        if (defaults && definitions.has(type)) definitions.get(type).setDefaults(value);
        return value;
    }
    const Terraria = native('Terraria'),
        Main = Terraria.Main;
    Main.item = [];
    Main.player = [];
    Main.chest = [];
    Main.netMode = mode;
    Main.myPlayer = 0;
    Terraria.ID.PlayerItemSlotID.Count = 400;
    Terraria.ID.ItemID.Sets.IsAMaterial = [];
    Terraria.ID.PlayerItemSlotID.SlotReference.new = () => {
        let selected;
        return {
            'void .ctor(Player player, int slot)'(player, slot) {
                selected = player.slots[slot];
            },
            get Item() {
                return selected;
            }
        };
    };
    const sandbox = {
        Terraria,
        FIRST_ITEM: 6196,
        Date: class extends Date {

            static now() {
                return clock;
            }
        },
        Templates: {
            Adopt(cls, inst) {
                inst.Mod = { uuid: 'data-test' };
            },
            HideFromMenu() {}
        },
        GoreLoader: { Autoload() {} },
        EquipLoader: { Install() {}, Apply() {}, Autoload() {} },
        DamageClassLoader: {
            Defaulting(item, fn) {
                fn();
            }
        },
        ModFiles: { ContentTexture: () => 'test', Texture: v => v },
        Lang: { Localized() {}, Follow() {} },
        Ready: { Add() {} },
        PrefixLoader: { WantRollable() {} },
        AnglerQuestLoader: { Watch() {} },
        Safe: {
            Run(label, fn) {

                try {
                    return fn();
                }
                catch (e) {
                    errors.push([label, String(e)]);
                }
            }
        },
        bl: {
            classOf: (namespace, name) => native(namespace ? namespace + '.' + name : name),
            defineField(cls, key) {
                fields.push(key);
            },
            addressOf: obj => obj?.__address || 0,
            objectAt: id => objects.get(id),
            items: {
                register(def) {
                    const type = nextType++;
                    definitions.set(type, def);
                    return type;
                },
                __chestData(chest, slot, data) {
                    if (data !== undefined) chest.item[slot].data = data;
                    return chest.item[slot].data || '';
                }
            },
            log() {}
        }
    };
    const context = vm.createContext(sandbox);
    for (const file of [
        'Core/Hooks.js',
        'Core/Entities.js',
        'TagCompound.js',
        'ModItem.js',
        'NetWriter.js',
        'NetReader.js',
        'Core/ModNet.js',
        'Loaders/ItemLoader.js',
        'Loaders/ItemDataLoader.js',
        'Loaders/ItemNetworkHooks.js'
    ]) {
        vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
    }
    const run = code => vm.runInContext(code, context);
    const api = run('({ ModItem, ItemLoader, ItemDataLoader, ItemNetworkHooks, ModNet, TagCompound, Hooks })');
    api.ItemLoader.Hook = () => {};
    api.ItemLoader.SetupTooltip = () => {};
    api.ModNet.Send = (envelope, remote, ignore) => {
        // Serialize across realms exactly as the real JSON network transport does.
        const copy = JSON.parse(JSON.stringify(envelope));
        if (Buffer.byteLength(JSON.stringify(copy)) > 65000) throw RangeError('packet too large');
        sent.push(copy);
        calls.push({ kind: 'data', envelope: copy, remote, ignore });
    };
    const entry = (cls, signature) => native(cls)[signature].entry;
    const setVanilla = (cls, signature, fn) => {
        entry(cls, signature).vanilla = fn;
    };
    const call = (cls, signature, ...args) => native(cls)[signature](...args);
    function copyNative(source, destination) {
        const copy = destination || item(source.type, false);
        for (const key of ['type', 'stack', 'prefix', 'favorited']) copy[key] = source[key];
        return copy;
    }
    setVanilla('Terraria.Item', 'Item Clone()', copyNative);
    setVanilla('Terraria.Item', 'Item DeepClone()', copyNative);
    setVanilla('Terraria.Item', 'Item clientClone(Item cloneDestination)', copyNative);
    setVanilla('Terraria.Item', 'bool IsNetStateDifferent(Item compareItem)', () => false);
    setVanilla('Terraria.NetMessage', SEND, (...args) => calls.push({ kind: 'native', packet: args[0], index: args[4], slot: args[5] }));
    setVanilla('Terraria.InventoryStorage', 'void SyncChestItemUpdateToStorage(int chestIndex, int slot)', (index, slot) => calls.push({ kind: 'storage', index, slot }));
    function world(index, value) {
        return (Main.item[index] = {
            inner: value,
            SyncItem() {
                send(21, index, 0);
            }
        });
    }
    function send(packet, index, slot = 0, remote = -1, ignore = -1) {
        return call('Terraria.NetMessage', SEND, packet, remote, ignore, null, index, slot, 0, 0, 0, 0, 0);
    }
    const reset = (target, type) => {
        target.type = type;
        target.stack = 1;
        definitions.get(type)?.setDefaults(target);
    };
    const wire = (packet, index, slot = 0) => (packet === 5 ? [packet, index, slot & 255, slot >> 8] : packet === 32 ? [packet, index & 255, index >> 8, slot] : [packet, index & 255, index >> 8]);
    function receive(envelope, from = mode === 1 ? 256 : 0, original = () => {}, index = envelope.i) {
        api.ItemNetworkHooks.Queue(envelope, from);
        const data = wire(envelope.p, index, envelope.s);
        return api.ItemNetworkHooks.Process(original, { whoAmI: from }, data, data.length, {});
    }
    return {
        api,
        run,
        item,
        world,
        reset,
        send,
        receive,
        wire,
        entry,
        setVanilla,
        call,
        definitions,
        errors,
        sent,
        calls,
        fields,
        Main,
        tick(ms) {
            clock += ms;
        },
        hooks: installed
    };
}

export const SEND = 'void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)';
export const NEW =
    'int NewItem(IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier, bool noBroadcast)';
export const REQUEST =
    'void RequestNewItem(IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier)';
