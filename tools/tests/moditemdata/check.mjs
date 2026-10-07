import assert from 'node:assert/strict';
import { createRuntime, NEW, REQUEST } from './runtime.mjs';

let passed = 0;

function test(name, body) {
    try {
        body();
        passed++;
        console.log('PASS ' + name);
    }
    catch (error) {
        console.error('FAIL ' + name);
        throw error;
    }
}

const fixture = `
class Persistent extends ModItem {
    owner = '';
    count = 0;
    enabled = false;
    nested = null;

    SetDefaults(item) {
        this.owner = '';
        this.count = 0;
        this.enabled = false;
        this.nested = null;
    }

    SaveData(tag) {
        tag.Set('owner', this.owner);
        tag.Set('count', this.count);
        tag.Set('enabled', this.enabled);
        tag.Set('nested', this.nested);
    }

    LoadData(tag) {
        this.owner = tag.GetString('owner');
        this.count = tag.GetInt('count');
        this.enabled = tag.GetBool('enabled');
        this.nested = tag.Get('nested', null);
    }

    Clone(item) {
        const copy = super.Clone(item);
        copy.nested = this.nested === null ? null : JSON.parse(JSON.stringify(this.nested));
        return copy;
    }
}

class Inherited extends Persistent {}

class ExplicitNet extends Persistent {
    NetSend(writer) {
        writer.WriteString(this.owner);
        writer.WriteInt32(this.count);
    }

    NetReceive(reader) {
        this.owner = reader.ReadString();
        this.count = reader.ReadInt32();
    }
}

class Empty extends ModItem {
    SaveData(tag) {}

    LoadData(tag) {
        this.loadedEmpty = Object.keys(tag).length === 0;
    }
}

class Plain extends ModItem {}

class FailingSave extends Persistent {
    SaveData(tag) {
        tag.Set('partial', 1);
        throw Error('save failed');
    }
}

class FailingLoad extends Persistent {
    LoadData(tag) {
        throw Error('load failed');
    }
}

const classes = [Persistent, Inherited, ExplicitNet, Empty, Plain, FailingSave, FailingLoad];
globalThis.types = classes.map(cls => ModItem.register(cls));
`;

function setup(mode = 0) {
    const r = createRuntime(mode);
    r.run(fixture);
    return r;
}

const r = setup(),
    { ItemDataLoader: D, ItemNetworkHooks: N, ItemLoader: L } = r.api;
const persistent = () => r.item(6196);
const mod = item => L.Of(item);

test('base exposes four optional methods and inherited registration installs hooks only once', () => {
    for (const name of ['SaveData', 'LoadData', 'NetSend', 'NetReceive']) assert.equal(typeof r.api.ModItem.prototype[name], 'function');
    assert.equal(r.entry('Terraria.MessageBuffer', 'void ProcessData(byte[] messageData, int length, out int messageType)').hooks.length, 1);
    assert.equal(r.entry('Terraria.Item', 'Item DeepClone()').hooks.length, 1);
    assert.equal(typeof r.definitions.get(6196).saveData, 'function');
    assert.equal(typeof r.definitions.get(6196).loadData, 'function');
});
test('no SaveData/LoadData override leaves network and transfer hooks uninstalled', () => {
    const clean = createRuntime();
    clean.run('class Plain extends ModItem {}; ModItem.register(Plain)');
    assert.equal([...clean.hooks.values()].filter(h => h.hooks.length && !h.id.includes('Item Clone()')).length, 0);
});
test('round trip uses registered callbacks, actual per-instance ModItem, and TagCompound', () => {
    const source = persistent(),
        target = persistent();
    Object.assign(mod(source), { owner: 'Jogador A', count: 42, enabled: true });
    const data = r.definitions.get(source.type).saveData(source);
    r.definitions.get(target.type).loadData(target, data);
    assert.equal(mod(target).owner, 'Jogador A');
    assert.equal(mod(target).count, 42);
    assert.equal(mod(target).enabled, true);
    assert.notEqual(mod(source), mod(target));
    assert.equal(mod(target).Item, target);
});
test('default values, zero, false and null survive', () => {
    const source = persistent(),
        target = persistent();
    D.Load(target, D.Save(source));
    assert.deepEqual(JSON.parse(D.Save(target)), { owner: '', count: 0, enabled: false, nested: null });
});
test('nested compounds, lists, decimal numbers and unicode survive independently', () => {
    const source = persistent(),
        target = persistent();
    mod(source).owner = 'João 🎮\tA\n"B"\\';
    mod(source).nested = r.run('({list:[1,-2,0.125,false,null,"ç"], child:TagCompound.from({level:7})})');
    D.Load(target, D.Save(source));
    assert.deepEqual(JSON.parse(D.Save(target)), JSON.parse(D.Save(source)));
    mod(target).nested.list[0] = 100;
    assert.equal(mod(source).nested.list[0], 1);
});
test('different items of the same type never exchange state', () => {
    const a = persistent(),
        b = persistent(),
        c = persistent();
    mod(a).owner = 'A';
    mod(b).owner = 'B';
    D.Load(c, D.Save(a));
    assert.equal(mod(b).owner, 'B');
    assert.equal(mod(c).owner, 'A');
    assert.equal(mod(persistent()).owner, '');
});
test('inherited callbacks are invoked on the derived instance', () => {
    const a = r.item(6197),
        b = r.item(6197);
    mod(a).count = 808;
    D.Load(b, D.Save(a));
    assert.equal(mod(b).count, 808);
    assert.equal(mod(b).constructor.name, 'Inherited');
});
test('old saves without custom data call LoadData with an empty tag', () => {
    const item = persistent();
    mod(item).owner = 'stale';
    D.Load(item, '');
    assert.equal(mod(item).owner, '');
    const empty = r.item(6199);
    D.Load(empty, '');
    assert.equal(mod(empty).loadedEmpty, true);
    assert.equal(D.Save(empty), '');
});
test('fresh tag per save and empty saves clear old custom fields', () => {
    const item = r.item(6199);
    item.__blItemData = '{"removed":1}';
    assert.equal(D.Save(item), '');
    const a = persistent();
    mod(a).owner = 'A';
    const old = D.Save(a);
    mod(a).owner = 'B';
    assert.notEqual(D.Save(a), old);
});
test('unknown mod data is preserved without any loaded template', () => {
    const item = r.item(9000),
        text = '{"owner":"Ausente","revision":2}';
    D.Load(item, text);
    assert.equal(D.Save(item), text);
    const copy = r.item(9000);
    D.Copy(item, copy);
    assert.equal(D.Save(copy), text);
});
test('reusing an Item with SetDefaults clears cached data and failure status', () => {
    const item = persistent();
    D.Load(item, '{"owner":"old"}');
    item.__blItemDataLoadFailed = true;
    r.reset(item, 6197);
    assert.equal(item.__blItemData, undefined);
    assert.equal(item.__blItemDataLoadFailed, false);
    assert.equal(mod(item).owner, '');
});
for (const [name, expression] of [
    ['undefined', '({x:undefined})'],
    ['function', '({x:()=>1})'],
    ['symbol value', '({x:Symbol("a")})'],
    ['symbol key', '({[Symbol("a")]:1})'],
    ['bigint', '({x:1n})'],
    ['NaN', '({x:NaN})'],
    ['Infinity', '({x:Infinity})'],
    ['Date', '({x:new Date()})'],
    ['circular graph', '(()=>{const x={};x.child=x;return x})()'],
    ['sparse array', '({x:[,1]})'],
    ['array extra properties', '({x:Object.assign([1],{other:2})})'],
    ['getter', '({get x(){return 1}})'],
    ['toJSON substitution', '({x:{toJSON(){return 1}}})'],
    ['hidden toJSON substitution', '({x:Object.defineProperty({},"toJSON",{value:()=>1})})'],
    ['hidden data', 'Object.defineProperty({},"lost",{value:1})'],
    ['excessive depth', '(()=>{let x={};for(let i=0;i<130;i++)x={x};return x})()']
])
    test('reject non-serializable SaveData: ' + name, () => {
        assert.throws(() => D.Encode(r.run(expression)));
    });
test('repeated references without cycles are accepted', () => {
    assert.equal(D.Encode(r.run('(()=>{const x={a:1};return {x,y:x}})()')), '{"x":{"a":1},"y":{"a":1}}');
});
test('reserved JSON keys cannot mutate TagCompound prototypes or hide the entry count', () => {
    const tag = r.run('TagCompound.from(JSON.parse(\'{"__proto__":{"polluted":true},"Count":99,"Set":"value"}\'))');
    assert.equal(Object.getPrototypeOf(tag), r.api.TagCompound.prototype);
    assert.equal(tag.polluted, undefined);
    assert.deepEqual(JSON.parse(D.Encode(tag)), JSON.parse('{"__proto__":{"polluted":true},"Count":99,"Set":"value"}'));
});
for (const text of ['{', 'null', '[]', '1', '"text"', '{"x":1e999}'])
    test('reject invalid LoadData and preserve its original bytes: ' + text, () => {
        const item = persistent();
        assert.throws(() => D.Load(item, text));
        assert.equal(item.__blItemData, text);
        assert.throws(() => D.Save(item), /LoadData falhou/);
        D.Load(item, '{"owner":"recovered"}');
        assert.equal(mod(item).owner, 'recovered');
        assert.doesNotThrow(() => D.Save(item));
    });
test('SaveData exception leaves the last successful cached snapshot intact', () => {
    const item = r.item(6201);
    item.__blItemData = '{"owner":"backup"}';
    assert.throws(() => D.Save(item), /save failed/);
    assert.equal(item.__blItemData, '{"owner":"backup"}');
});
test('LoadData exception preserves raw data and blocks destructive resave', () => {
    const item = r.item(6202),
        text = '{"owner":"backup"}';
    assert.throws(() => D.Load(item, text), /load failed/);
    assert.equal(item.__blItemData, text);
    assert.throws(() => D.Save(item), /LoadData falhou/);
});
for (const signature of ['Item Clone()', 'Item DeepClone()', 'Item clientClone(Item cloneDestination)'])
    test(signature + ' preserves state, binds the copy and honors Clone override', () => {
        const a = persistent();
        mod(a).owner = 'A';
        mod(a).nested = r.run('({charges:[1,2]})');
        D.Save(a);
        const b = r.call('Terraria.Item', signature, a, signature.includes('clientClone') ? persistent() : undefined);
        assert.notEqual(a, b);
        assert.equal(mod(b).owner, 'A');
        assert.equal(mod(b).Item, b);
        assert.equal(b.__blItemData, a.__blItemData);
        mod(b).nested.charges.push(3);
        assert.equal(mod(a).nested.charges.length, 2);
    });
test('network snapshot detects custom-only changes after clientClone', () => {
    const a = persistent();
    mod(a).owner = 'A';
    const b = r.call('Terraria.Item', 'Item clientClone(Item cloneDestination)', a, persistent());
    assert.equal(r.call('Terraria.Item', 'bool IsNetStateDifferent(Item compareItem)', a, b), false);
    mod(a).owner = 'B';
    assert.equal(r.call('Terraria.Item', 'bool IsNetStateDifferent(Item compareItem)', a, b), true);
});
test('Copy handles nulls and same-object copies', () => {
    const a = persistent();
    assert.doesNotThrow(() => {
        D.Copy(null, a);
        D.Copy(a, null);
        D.Copy(a, a);
    });
});
test('200 deterministic mixed-data round trips preserve serialized values', () => {
    let seed = 0x51a9;
    const next = () => {
        seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
        return seed;
    };
    const value = depth => {
        const n = next();
        switch (n % (depth ? 7 : 5)) {
            case 0:
                return null;
            case 1:
                return n % 2 === 0;
            case 2:
                return (n - 0x80000000) / 16;
            case 3:
                return 'ç🎮\\\t\n' + n;
            case 4:
                return '';
            case 5:
                return [value(depth - 1), value(depth - 1)];
            default:
                return { left: value(depth - 1), right: value(depth - 1) };
        }
    };
    for (let i = 0; i < 200; ++i) {
        const original = { owner: 'owner' + i, count: i, enabled: i % 2 === 0, nested: value(4) };
        const item = persistent();
        D.Load(item, JSON.stringify(original));
        const saved = D.Save(item);
        const reloaded = persistent();
        D.Load(reloaded, saved);
        assert.deepEqual(JSON.parse(D.Save(reloaded)), original);
    }
});

for (const packet of [5, 21, 32, 90])
    test('metadata precedes native packet ' + packet + ' and respects routing', () => {
        const x = setup(2),
            value = x.item(6196);
        x.api.ItemLoader.Of(value).owner = 'A';
        x.Main.player[1] = { slots: { 3: value } };
        x.world(1, value);
        x.Main.chest[1] = { item: [null, null, null, { ExpandItem: () => value }] };
        x.send(packet, 1, 3, 7, 2);
        assert.equal(x.calls[0].kind, 'data');
        assert.equal(x.calls[1].kind, 'native');
        assert.equal(JSON.parse(x.sent[0].d).owner, 'A');
        assert.equal(x.calls[0].remote, 7);
        assert.equal(x.calls[0].ignore, 2);
    });
test('single player never sends item metadata', () => {
    r.world(1, persistent());
    r.send(21, 1);
    assert.equal(r.sent.length, 0);
});
test('vanilla items never send item metadata', () => {
    const x = setup(2);
    x.world(1, x.item(1));
    x.send(21, 1);
    assert.equal(x.sent.length, 0);
});
test('explicit NetSend/NetReceive override the automatic SaveData/LoadData fallback', () => {
    const a = r.item(6198),
        b = r.item(6198);
    mod(a).owner = 'A';
    mod(a).count = 73;
    mod(a).enabled = true;
    const payload = N.Payload(a);
    assert.equal(payload.d, undefined);
    assert.ok(Array.isArray(payload.n));
    assert.equal(N.Apply(b, { t: N.Key(a), ...payload }), true);
    assert.equal(mod(b).owner, 'A');
    assert.equal(mod(b).count, 73);
    assert.equal(mod(b).enabled, false);
});
test('owner A survives client -> server relay -> client B -> pickup and second drop', () => {
    const a = setup(1),
        server = setup(2),
        b = setup(1);
    const source = a.item(6196);
    a.api.ItemLoader.Of(source).owner = 'Jogador A';
    a.world(400, source);
    a.send(21, 400);
    const serverItem = server.item(6196);
    server.world(7, serverItem);
    server.receive(
        a.sent[0],
        0,
        () => {
            server.reset(serverItem, 6196);
            server.send(21, 7);
        },
        400
    );
    // Native server creation assigns a different world slot. Exercise the actual NewItem hook below too.
    // Here original explicitly identifies the assigned slot through NewItem, matching the game.
    assert.equal(server.api.ItemLoader.Of(serverItem).owner, '');
    // Above deliberately did not create through NewItem: do not attach data to an unrelated slot.
    assert.equal(JSON.parse(server.sent[0].d).owner, '');
    server.setVanilla('Terraria.Item', NEW, () => {
        server.reset(serverItem, 6196);
        return 7;
    });
    server.sent.length = 0;
    server.receive(a.sent[0], 0, () => server.call('Terraria.Item', NEW, null, {}, 6196, 1, 0, 0, null, null, false), 400);
    assert.equal(server.api.ItemLoader.Of(serverItem).owner, 'Jogador A');
    const receiver = b.item(6196);
    b.world(7, receiver);
    b.receive(server.sent[0], 256, () => b.reset(receiver, 6196));
    const picked = b.call('Terraria.Item', 'Item Clone()', receiver);
    assert.equal(b.api.ItemLoader.Of(picked).owner, 'Jogador A');
    b.world(400, picked);
    b.send(21, 400);
    assert.equal(JSON.parse(b.sent[0].d).owner, 'Jogador A');
});
test('client inventory update applies before server relay, including slots above 255', () => {
    const x = setup(2),
        item = x.item(6196);
    x.Main.player[2] = { slots: { 300: item } };
    x.receive({ p: 5, i: 2, s: 300, t: 'data-test/Persistent', d: '{"owner":"A"}' }, 2, () => {
        x.reset(item, 6196);
        x.send(5, 2, 300);
    });
    assert.equal(x.api.ItemLoader.Of(item).owner, 'A');
    assert.equal(JSON.parse(x.sent[0].d).owner, 'A');
});
test('chest update applies before relay and refreshes expanded InventoryStorage', () => {
    const x = setup(2),
        item = x.item(6196);
    x.Main.chest[1] = { item: [{ ExpandItem: () => item }] };
    x.receive({ p: 32, i: 1, s: 0, t: 'data-test/Persistent', d: '{"owner":"A"}' }, 0, () => {
        x.reset(item, 6196);
        x.send(32, 1, 0);
    });
    assert.equal(JSON.parse(x.Main.chest[1].item[0].data).owner, 'A');
    assert.equal(JSON.parse(x.sent[0].d).owner, 'A');
    assert.ok(x.calls.some(c => c.kind === 'storage'));
});
test('client chest update applies after native reset even without relay', () => {
    const x = setup(1),
        item = x.item(6196);
    x.Main.chest[1] = { item: [{ ExpandItem: () => item }] };
    x.receive({ p: 32, i: 1, s: 0, t: 'data-test/Persistent', d: '{"owner":"A"}' }, 256, () => x.reset(item, 6196));
    assert.equal(JSON.parse(x.Main.chest[1].item[0].data).owner, 'A');
});
test('native selected-item drop copies state before its first client broadcast', () => {
    const x = setup(1),
        source = x.item(6196);
    x.api.ItemLoader.Of(source).owner = 'A';
    const player = { inventory: [source], selectedItem: 0 };
    let suppressed;
    x.setVanilla('Terraria.Item', NEW, (...args) => {
        suppressed = args[8];
        x.world(400, x.item(6196));
        return 400;
    });
    x.setVanilla('Terraria.Item', REQUEST, (...args) => {
        x.call('Terraria.Item', NEW, ...args, false);
        x.send(21, 400);
    });
    x.setVanilla('Terraria.Player', 'void DropSelectedItem()', self => {
        x.call('Terraria.Item', REQUEST, null, {}, source.type, 1, 0, 1, null, null);
        self.inventory[0] = x.item(0);
    });
    x.call('Terraria.Player', 'void DropSelectedItem()', player);
    assert.equal(suppressed, true);
    assert.equal(x.sent.length, 1);
    assert.equal(JSON.parse(x.sent[0].d).owner, 'A');
});
for (const mode of [2, 3])
    test(`server-side selected drop broadcasts preserved data in netMode ${mode}`, () => {
        const x = setup(mode),
            source = x.item(6196);
        x.api.ItemLoader.Of(source).owner = 'A';
        const player = { inventory: [source], selectedItem: 0 };
        x.setVanilla('Terraria.Item', NEW, (...args) => {
            assert.equal(args[8], true);
            x.world(7, x.item(6196));
            return 7;
        });
        x.setVanilla('Terraria.Item', REQUEST, (...args) => x.call('Terraria.Item', NEW, ...args, false));
        x.setVanilla('Terraria.Player', 'void DropSelectedItem()', () => x.call('Terraria.Item', REQUEST, null, {}, source.type, 1, 0, 1, null, null));
        x.call('Terraria.Player', 'void DropSelectedItem()', player);
        assert.equal(x.sent.length, 1);
        assert.equal(x.sent[0].i, 7);
        assert.equal(JSON.parse(x.sent[0].d).owner, 'A');
    });
test('mobile UI drop uses the explicit item, independent of the selected inventory slot', () => {
    const x = setup(1),
        source = x.item(6196);
    x.api.ItemLoader.Of(source).owner = 'UI_A';
    const selected = x.item(6196);
    x.api.ItemLoader.Of(selected).owner = 'OTHER';
    x.setVanilla('Terraria.Item', NEW, () => {
        x.world(400, x.item(6196));
        return 400;
    });
    x.setVanilla('Terraria.Item', REQUEST, (...args) => {
        x.call('Terraria.Item', NEW, ...args, false);
        x.send(21, 400);
    });
    x.setVanilla('GUIPageIcons', 'void DropUIItem(Player player, Item item, int additionalVelocity)', (player, value) => x.call('Terraria.Item', REQUEST, null, {}, value.type, 1, 0, 1, null, null));
    x.call('GUIPageIcons', 'void DropUIItem(Player player, Item item, int additionalVelocity)', { inventory: [selected], selectedItem: 0 }, source, 0);
    assert.equal(JSON.parse(x.sent[0].d).owner, 'UI_A');
    x.sent.length = 0;
    x.call('Terraria.Item', REQUEST, null, {}, source.type, 1, 0, 1, null, null);
    assert.equal(JSON.parse(x.sent[0].d).owner, '');
});
test('network rejects wrong item identity and unauthorized player inventory metadata', () => {
    const x = setup(2),
        item = x.item(6196);
    x.world(1, item);
    x.receive({ p: 21, i: 1, s: 0, t: 'other/Persistent', d: '{"owner":"bad"}' }, 0);
    assert.equal(x.api.ItemLoader.Of(item).owner, '');
    x.Main.player[3] = { slots: { 0: item } };
    x.receive({ p: 5, i: 3, s: 0, t: 'data-test/Persistent', d: '{"owner":"bad"}' }, 2);
    assert.equal(x.api.ItemLoader.Of(item).owner, '');
});
test('expired metadata and metadata discarded at disconnect cannot be reused', () => {
    const x = setup(1),
        item = x.item(6196),
        envelope = { p: 21, i: 1, s: 0, t: 'data-test/Persistent', d: '{"owner":"old"}' };
    x.world(1, item);
    x.api.ItemNetworkHooks.Queue(envelope, 256);
    x.tick(10001);
    x.api.ItemNetworkHooks.Process(() => {}, { whoAmI: 256 }, [21, 1, 0], 3, {});
    assert.equal(x.api.ItemLoader.Of(item).owner, '');
    x.api.ItemNetworkHooks.Queue(envelope, 256);
    x.call('Terraria.MessageBuffer', 'void Reset(bool setupActive)', { whoAmI: 256 }, false);
    x.api.ItemNetworkHooks.Process(() => {}, { whoAmI: 256 }, [21, 1, 0], 3, {});
    assert.equal(x.api.ItemLoader.Of(item).owner, '');
});
test('native exception restores packet context for subsequent unrelated sends', () => {
    const x = setup(2),
        item = x.item(6196);
    x.world(1, item);
    assert.throws(() =>
        x.receive({ p: 21, i: 1, s: 0, t: 'data-test/Persistent', d: '{"owner":"bad"}' }, 0, () => {
            throw Error('native failure');
        })
    );
    x.send(21, 1);
    assert.equal(JSON.parse(x.sent[0].d).owner, '');
});
test('malformed envelopes, truncated native packets and missing target slots are ignored', () => {
    const x = setup(1),
        item = x.item(6196);
    x.world(1, item);
    for (const changes of [{ i: -1 }, { i: 401 }, { i: 1.5 }, { p: 99 }, { s: -1 }, { t: null }, { n: [], d: '{}' }, { d: null }]) {
        x.receive({ p: 21, i: 1, s: 0, t: 'data-test/Persistent', d: '{"owner":"bad"}', ...changes });
    }
    assert.equal(x.api.ItemLoader.Of(item).owner, '');
    let original = 0;
    for (const data of [[], [5], [5, 0, 0], [32, 0, 0]]) x.api.ItemNetworkHooks.Process(() => original++, { whoAmI: 256 }, data, data.length, {});
    assert.equal(original, 4);
    assert.doesNotThrow(() => x.receive({ p: 5, i: 10, s: 399, t: 'data-test/Persistent', d: '{}' }));
});
assert.equal(r.errors.length, 0, JSON.stringify(r.errors));
console.log(`moditemdata: ${passed} behavioral cases passed (real JS helpers; native game calls simulated).`);
