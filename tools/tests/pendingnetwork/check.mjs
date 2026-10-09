import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createRuntime, root } from '../apiexpansion/runtime.mjs';
let checks = 0;
function test(name, run) { run(); checks++; console.log('PASS ' + name); }
function runtime(mode, local) {
    const r = createRuntime(), packets = [], callbacks = [];
    r.Main.netMode = mode; r.Main.myPlayer = local; r.Main.player = []; r.Main.npc = []; r.Main.projectile = [];
    r.sandbox.PlayerLoader = { Veto: () => false, Nullable: () => null, Each() {}, Any: () => false, First: () => false, Call() {} };
    r.sandbox.ModProjectile = class {};
    r.sandbox.ProjectileLoader = { Of: p => p.ModProjectile };
    r.sandbox.globalProjectiles = { For: () => [] };
    r.sandbox.ModNet = {
        Install() {}, Send: (data, to, ignore) => packets.push(JSON.parse(JSON.stringify({ data, to, ignore }))),
        Received(key, ...types) { for (const type of types) r.marks.set(key, new Set(types)); return { minType: 0, firstByte: 0, marks: key }; }
    };
    for (const file of ['TagCompound', 'NetWriter', 'NetReader', 'StatModifier', 'HitModifiers', 'HitInfo', 'HurtModifiers', 'HurtInfo',
        'Loaders/ItemDataLoader', 'Loaders/ItemNetworkHooks', 'Loaders/ItemCombatHooks', 'Loaders/PvpItemNetwork', 'Loaders/CombatLoader'])
        r.run(fs.readFileSync(path.join(root, 'app/src/main/cpp/script/js/mod', file + '.js'), 'utf8'));
    r.run(`class Weapon extends ModItem {
        constructor() { super(); this.Mod = { uuid: 'mod-a' }; this.power = 3; this.allowed = true; }
        CanHitPvp() { callbacks.push('can'); return this.allowed; }
        ModifyHitPvp(item, player, target, modifiers) { callbacks.push(['modify', player.whoAmI, target.whoAmI, this.power]); modifiers.damage = this.power; }
        OnHitPvp(item, player, target, info) { callbacks.push(['hit', info.Damage]); }
        NetSend(writer) { writer.Write(this.power); }
        NetReceive(reader) { this.power = reader.ReadInt32(); }
    }
    ItemLoader.ByType.set(6196, new Weapon());
    ItemCombatHooks.Mark('player.PvpAttack', 6196);
    CombatLoader.InstallPvp();`);
    const api = r.run('({ CombatLoader, PvpItemNetwork, Weapon })');
    function item(type = 6196) {
        const obj = r.entity(type, type === 6196 ? new api.Weapon() : null); obj.prefix = 0;
        obj['void .ctor()'] = () => {};
        obj['void SetDefaults(int Type, ItemVariant variant)'] = type => { obj.type = type; obj.ModItem = type === 6196 ? new api.Weapon() : null; };
        obj['bool Prefix(int prefixWeWant)'] = prefix => { obj.prefix = prefix; return true; };
        return obj;
    }
    r.Terraria.Item.new = item;
    for (let i = 0; i < 3; i++) r.Main.player.push(Object.assign(r.entity(0), { whoAmI: i, active: true, inventory: [item()], selectedItem: 0, statDefense: 0, statLife: 100 }));
    r.vanilla('Terraria.Player', 'CanHit', () => true);
    r.vanilla('Terraria.Player', 'Hurt', (_, source, damage) => damage);
    r.Terraria.Main['double CalculateDamagePlayersTakeInPVP(int Damage, int Defense)'];
    r.vanilla('Terraria.Main', 'CalculateDamagePlayersTakeInPVP', damage => damage);
    r.sandbox.callbacks = callbacks;
    const source = { _sourcePlayerIndex: 1, _sourceItemType: 6196, _sourceItemPrefix: 0, _sourceNPCIndex: -1, _sourceProjectileLocalIndex: -1, _sourceProjectileType: 0 };
    const hurt = () => r.call('Terraria.Player', 'Hurt', r.Main.player[local], source, 10, 1, true, false, false, -1, false);
    return { ...r, ...api, item, packets, callbacks, source, hurt };
}
test('veto precedes native status, Hurt and send; flags and context restore', () => {
    const r = runtime(1, 1), player = r.Main.player[1], target = r.Main.player[0], item = player.inventory[0];
    item.ModItem.allowed = false;
    r.vanilla('Terraria.Player', 'ItemCheck_MeleeHitPVP', (p, item) => {
        if (!r.call('Terraria.Player', 'CanHit', p, target)) return;
        r.callbacks.push('status'); r.call('Terraria.NetMessage', 'SendPlayerHurt', 0, r.source, 10, 1, false, true, -1, -1, -1);
    });
    r.call('Terraria.Player', 'ItemCheck_MeleeHitPVP', player, item, {}, 10, 0);
    assert.deepEqual(r.callbacks, ['can']); assert.equal(r.packets.length, 0); assert.equal(r.CombatLoader.PvpAttack, null);
});
test('metadata precedes native Hurt send and serializes NetSend state', () => {
    const r = runtime(1, 1); r.Main.player[1].inventory[0].ModItem.power = 7;
    r.vanilla('Terraria.NetMessage', 'SendPlayerHurt', () => r.callbacks.push('native-send'));
    r.call('Terraria.NetMessage', 'SendPlayerHurt', 0, r.source, 10, 1, false, true, -1, -1, -1);
    assert.equal(r.packets[0].data.k, 'pvp-item'); assert.deepEqual(r.packets[0].data.n, [7]); assert.deepEqual(r.callbacks, ['native-send']);
});
test('server victim reconstructs item state after attacker switched slots', () => {
    const r = runtime(3, 0); r.Main.player[1].inventory[0] = r.item(1);
    r.PvpItemNetwork.Receive({ k: 'pvp-item', a: 1, i: 0, type: 6196, prefix: 0, t: 'mod-a/Weapon', n: [7] }, 1);
    r.vanilla('Terraria.MessageBuffer', 'ProcessData', () => assert.equal(r.hurt(), 7));
    r.call('Terraria.MessageBuffer', 'ProcessData', { whoAmI: 1 }, [117, 0], 2, {});
    assert.deepEqual(JSON.parse(JSON.stringify(r.callbacks)), [['modify', 1, 0, 7], ['hit', 7]]);
    assert.equal(r.PvpItemNetwork.Source(r.source, 0), null);
});
test('server forwards identical item metadata before native relay', () => {
    const r = runtime(3, 0), envelope = { k: 'pvp-item', a: 1, i: 2, type: 6196, prefix: 0, t: 'mod-a/Weapon', n: [9] };
    r.PvpItemNetwork.Receive(envelope, 1);
    r.vanilla('Terraria.MessageBuffer', 'ProcessData', () => r.call('Terraria.NetMessage', 'SendPlayerHurt', 2, r.source, 10, 1, false, true, -1, -1, 1));
    r.call('Terraria.MessageBuffer', 'ProcessData', { whoAmI: 1 }, [117, 2], 2, {});
    assert.deepEqual(r.packets[0].data.n, [9]); assert.equal(r.packets[0].ignore, 1);
});
test('remote target copy does not modify item damage twice', () => {
    const r = runtime(1, 1);
    const damage = r.call('Terraria.Player', 'Hurt', r.Main.player[0], r.source, 10, 1, true, false, false, -1, false);
    assert.equal(damage, 10); assert.deepEqual(r.callbacks, []);
});
test('invalid sender, source kind and item IDs do not invoke wrong item', () => {
    const r = runtime(3, 0);
    r.PvpItemNetwork.Receive({ a: 2, i: 0, type: 6196, prefix: 0, t: 'mod-a/Weapon', n: [7] }, 1);
    r.vanilla('Terraria.MessageBuffer', 'ProcessData', () => assert.equal(r.hurt(), 3));
    r.call('Terraria.MessageBuffer', 'ProcessData', { whoAmI: 1 }, [117, 0], 2, {});
    r.source._sourceProjectileType = 83; assert.equal(r.CombatLoader.PvpSource(r.source, 0), null);
    r.source._sourceProjectileType = 0; r.source._sourceItemType = 9000; assert.equal(r.CombatLoader.PvpSource(r.source, 0), null);
});
test('packet failure restores receiving scope and world clear drops queued attacks', () => {
    const r = runtime(3, 0), envelope = { a: 1, i: 0, type: 6196, prefix: 0, t: 'mod-a/Weapon', n: [8] };
    r.PvpItemNetwork.Receive(envelope, 1);
    r.vanilla('Terraria.MessageBuffer', 'ProcessData', () => { throw Error('native'); });
    assert.throws(() => r.call('Terraria.MessageBuffer', 'ProcessData', { whoAmI: 1 }, [117, 0], 2, {}));
    assert.equal(r.PvpItemNetwork.Source(r.source, 0), null);
    r.PvpItemNetwork.Receive(envelope, 1); r.call('Terraria.GameContent.BannerSystem', 'Clear');
    r.vanilla('Terraria.MessageBuffer', 'ProcessData', () => assert.equal(r.hurt(), 3));
    r.call('Terraria.MessageBuffer', 'ProcessData', { whoAmI: 1 }, [117, 0], 2, {});
});
test('NPC incoming modifiers precede item, target and general/specific player hooks', () => {
    const r = runtime(0, 0);
    r.run(`Weapon.prototype.ModifyHitNPC = function(item, player, target, m) { callbacks.push('item-modify'); m.damage *= 2; };
        Weapon.prototype.OnHitNPC = function() { callbacks.push('item-hit'); };
        class Target extends ModNPC {
            ModifyIncomingHit(npc, m) { callbacks.push('incoming'); m.damage = 11; }
            ModifyHitByItem(npc, player, item, m) { callbacks.push('target-modify'); m.damage += 3; }
            OnHitByItem() { callbacks.push('target-hit'); }
        }
        NPCLoader.ByType.set(697, new Target());
        CombatLoader.InstallItemCombat();`);
    const targetMod = r.run('new Target()'), npc = r.entity(697, targetMod);
    npc.defense = 0; r.Main.npc[0] = npc;
    r.sandbox.PlayerLoader.Call = (player, name, ...args) => {
        if (name === 'ModifyHitNPC') { r.callbacks.push('player-modify'); args[1].damage *= 2; }
        if (name === 'ModifyHitNPCWithItem') { r.callbacks.push('player-item-modify'); args[2].damage += 7; }
        if (name === 'OnHitNPC') r.callbacks.push('player-hit');
        if (name === 'OnHitNPCWithItem') r.callbacks.push('player-item-hit');
    };
    r.marks.set('player.ItemAttack', new Set([6196]));
    r.vanilla('Terraria.Player', 'ProcessHitAgainstNPC', (player, item, box, damage) =>
        r.call('Terraria.NPC', 'StrikeNPC', npc, damage, 0, 1, false, false, player.whoAmI));
    r.vanilla('Terraria.NPC', 'StrikeNPC', (target, damage) => { assert.equal(damage, 57); return damage; });
    r.call('Terraria.Player', 'ProcessHitAgainstNPC', r.Main.player[0], r.Main.player[0].inventory[0], {}, 10, 0, 0);
    assert.deepEqual(r.callbacks, ['incoming', 'item-modify', 'target-modify', 'player-modify', 'player-item-modify',
        'item-hit', 'target-hit', 'player-hit', 'player-item-hit']);
});
console.log('pendingnetwork: ' + checks + ' behavioral checks passed');
