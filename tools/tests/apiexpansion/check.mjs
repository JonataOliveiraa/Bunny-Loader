import assert from 'node:assert/strict';
import { createRuntime } from './runtime.mjs';

let checks = 0;
function test(name, fn) { fn(); console.log('PASS ' + name); checks++; }
const eq = (a, b) => assert.equal(JSON.stringify(a), JSON.stringify(b));

function drawRuntime(kind, veto = false) {
    const r = createRuntime();
    r.sandbox.trace = [];
    r.sandbox.veto = veto;
    r.run(`class Probe extends ModItem {
        PreDrawInWorld(...a) { trace.push(['own-pre', a.length, a[4].value, a[5].value]); a[4].value += 1; a[5].value *= 2; return !veto; }
        PostDrawInWorld(...a) { trace.push(['own-post', a.length, a[4], a[5]]); }
        PreDrawInInventory(...a) { trace.push(['own-pre', a.length, a[4].alpha, a[5].item]); return !veto; }
        PostDrawInInventory(...a) { trace.push(['own-post', a.length, a[7]]); }
    }
    class G extends GlobalItem {
        PreDrawInWorld(...a) { trace.push(['global-pre', a.length, a[4].value, a[5].value]); a[4].value += 3; return !veto; }
        PostDrawInWorld(...a) { trace.push(['global-post', a.length, a[4], a[5]]); }
        PreDrawInInventory(...a) { trace.push(['global-pre', a.length, a[4].alpha, a[5].item]); return !veto; }
        PostDrawInInventory(...a) { trace.push(['global-post', a.length, a[7]]); }
    }
    ItemLoader.ByType.set(6196, new Probe());
    globalItems.list.push(new G());
    ItemDrawLoader.Want(Probe, 6196); ItemDrawLoader.Want(G);`);
    const item = r.entity(6196, r.api.ItemLoader.ByType.get(6196));
    const stage = r.stages.get(kind);
    if (kind === 'World') {
        const world = { inner: item };
        r.vanilla('Terraria.Main', 'DrawItem', () => {
            const result = stage.pre({ R: 11 }, { R: 22 }, 0.5, 0.75);
            if (result[0]) r.sandbox.trace.push(['native', result[1], result[2]]);
            stage.post();
        });
        r.draw = () => r.call('Terraria.Main', 'DrawItem', {}, world, 7);
    } else {
        r.vanilla('Terraria.UI.ItemSlot', 'DrawItemIcon', () => {
            const allowed = stage.pre(item, r.Main.spriteBatch, { X: 10, Y: 20 }, { Width: 16, Height: 20 }, { R: 11 }, { R: 22 }, { X: 8, Y: 10 }, 0.75);
            if (allowed) r.sandbox.trace.push(['native']);
            stage.post();
            return 0.75;
        });
        r.draw = () => r.call('Terraria.UI.ItemSlot', 'DrawItemIcon', item, 0, r.Main.spriteBatch, {}, 1, 32, {}, 1, false);
    }
    return r;
}

for (const veto of [false, true]) {
    test('world order, shared refs, post after veto=' + veto, () => {
        const r = drawRuntime('World', veto); r.draw();
        eq(r.sandbox.trace, [['own-pre', 7, 0.5, 0.75], ['global-pre', 7, 1.5, 1.5],
            ...(!veto ? [['native', 4.5, 1.5]] : []), ['own-post', 7, 4.5, 1.5], ['global-post', 7, 4.5, 1.5]]);
        assert.equal(r.flags.get('item.DrawWorld'), false);
    });
    test('inventory order, callback colors and return scale after veto=' + veto, () => {
        const r = drawRuntime('Inventory', veto);
        assert.equal(r.draw(), 0.75);
        eq(r.sandbox.trace, [['global-pre', 8, true, true], ['own-pre', 8, true, true],
            ...(!veto ? [['native']] : []), ['own-post', 8, 0.75], ['global-post', 8, 0.75]]);
    });
}
test('draw exceptions are isolated and following callbacks still run', () => {
    const r = drawRuntime('World');
    r.run(`Probe.prototype.PreDrawInWorld = () => { throw Error('probe'); };`);
    r.draw(); r.draw();
    assert.equal(r.errors.length, 1);
    assert.equal(r.sandbox.trace.filter(a => a[0] === 'global-pre').length, 2);
    assert.equal(r.sandbox.trace.filter(a => a[0] === 'own-post').length, 2);
});
test('native failure resets the draw context and flags', () => {
    const r = drawRuntime('Inventory');
    r.vanilla('Terraria.UI.ItemSlot', 'DrawItemIcon', () => { throw Error('native'); });
    assert.throws(r.draw, /native/);
    assert.equal(r.flags.get('item.DrawInventory'), false);
    const count = r.sandbox.trace.length;
    r.stages.get('Inventory').post();
    assert.equal(r.sandbox.trace.length, count);
});
test('unused draw callbacks install no hooks; global marks include future types', () => {
    const r = createRuntime();
    r.api.ItemDrawLoader.Want(r.api.ModItem, 6196);
    assert.equal(r.stages.size, 0);
    r.run('class G extends GlobalItem { PostDrawInInventory() {} } ItemDrawLoader.Want(G); class Plain extends ModItem {} ItemDrawLoader.Want(Plain, 6200);');
    assert.equal(r.stages.size, 1);
    assert.ok(r.marks.get('item.DrawInventory').has(1));
    assert.ok(r.marks.get('item.DrawInventory').has(6200));
});
test('per entity global draw filtering excludes unrelated items', () => {
    const r = drawRuntime('Inventory');
    r.run(`globalItems.list.length = 0; class Filtered extends G { AppliesToEntity(item) { return item.type === 1; } }
        globalItems.Register(Filtered, 'GlobalItem');`);
    r.draw();
    eq(r.sandbox.trace.map(a => a[0]), ['own-pre', 'native', 'own-post']);
});

function cloneRuntime() {
    const r = createRuntime();
    const m = new r.api.ModNPC(); m.Type = 697;
    const npc = r.entity(697, m);
    npc.__globalNPCs = { preserved: true };
    r.api.globalNPCs.ready = true;
    return { r, m, npc };
}
test('CloneDefaults copies values in classic difficulty and preserves identity and interaction', () => {
    const { r, m, npc } = cloneRuntime();
    const globals = npc.__globalNPCs;
    m.CloneDefaults(1);
    assert.equal(npc.damage, 7);
    assert.equal(npc.type, 697); assert.equal(npc.netID, 697);
    assert.equal(npc.ModNPC, m); assert.equal(npc.__globalNPCs, globals);
    eq(npc.playerInteraction, [true, false, true]);
    eq(r.events[0], { type: 1, params: { difficultyOverride: 1, sizeScaleOverride: 1 } });
});
test('CloneDefaults restores identity after native exception and allows subsequent copies', () => {
    const { m, npc } = cloneRuntime();
    const original = npc['void SetDefaults(int Type, NPCSpawnParams spawnparams)'];
    npc['void SetDefaults(int Type, NPCSpawnParams spawnparams)'] = () => { npc.type = 0; throw Error('native'); };
    assert.throws(() => m.CloneDefaults(1), /native/);
    assert.equal(npc.type, 697); assert.equal(npc.ModNPC, m);
    npc['void SetDefaults(int Type, NPCSpawnParams spawnparams)'] = original;
    m.CloneDefaults(1); assert.equal(npc.damage, 7);
});
test('CloneDefaults does not read an undefined bridge field without registered globals', () => {
    const { r, m, npc } = cloneRuntime();
    r.api.globalNPCs.ready = false;
    Object.defineProperty(npc, '__globalNPCs', { get() { throw Error('undefined bridge field'); }, configurable: true });
    npc['void SetDefaults(int Type, NPCSpawnParams spawnparams)'] = type => { npc.type = type; npc.damage = 7; };
    m.CloneDefaults(1);
    assert.equal(npc.type, 697); assert.equal(npc.damage, 7);
});
for (const type of [697, 900, 1.5, NaN]) test('CloneDefaults rejects invalid source ' + type, () => {
    const { m, npc } = cloneRuntime();
    assert.throws(() => m.CloneDefaults(type)); assert.equal(npc.type, 697);
});
test('nested CloneDefaults applies mod and global source defaults once and restores destination globals', () => {
    const { r, m, npc } = cloneRuntime();
    r.sandbox.count = 0;
    r.run(`class Source extends ModNPC { SetDefaults(npc) { npc.damage = 25; count++; } }
        const source = new Source(); source.Type = 698; NPCLoader.ByType.set(698, source);
        class G extends GlobalNPC { InstancePerEntity = true; SetDefaults(npc) { npc.damage += 2; count++; } }
        globalNPCs.Register(G, 'GlobalNPC');`);
    const globals = npc.__globalNPCs;
    m.SetDefaults = () => m.CloneDefaults(698);
    r.api.NPCLoader.Defaults(m, npc);
    assert.equal(npc.damage, 27); assert.equal(r.run('count'), 2);
    assert.equal(npc.ModNPC, m); assert.equal(npc.__globalNPCs, globals);
});
test('circular mod defaults terminate and restore the destination', () => {
    const { r, m, npc } = cloneRuntime();
    r.run(`class Loop extends ModNPC { SetDefaults(npc) { this.CloneDefaults(699); } }
        class Loop2 extends ModNPC { SetDefaults(npc) { this.CloneDefaults(698); } }
        const a = new Loop(), b = new Loop2(); a.Type = 698; b.Type = 699;
        NPCLoader.ByType.set(698, a); NPCLoader.ByType.set(699, b);`);
    m.SetDefaults = () => m.CloneDefaults(698);
    assert.throws(() => r.api.NPCLoader.Defaults(m, npc), /circular/);
    assert.equal(npc.type, 697); assert.equal(npc.ModNPC, m);
});

function bannerRuntime() {
    const r = createRuntime();
    r.run(`class Enemy extends ModNPC {} class Relative extends ModNPC {}
        const enemy = new Enemy(), relative = new Relative();
        enemy.Type = 697; enemy.Banner = 697; enemy.BannerItem = 6196; enemy.Mod = bl.mod;
        relative.Type = 698; relative.Banner = 697; relative.Mod = bl.mod;
        NPCLoader.ByType.set(697, enemy); NPCLoader.ByType.set(698, relative);
        BannerLoader.Install(); BannerLoader.Install(); BannerLoader.Finish();`);
    return r;
}
test('banners use the representative NPC ID and preserve vanilla mappings', () => {
    const r = bannerRuntime();
    r.vanilla('Terraria.GameContent.BannerSystem', 'NPCtoBanner', () => 1);
    r.vanilla('Terraria.GameContent.BannerSystem', 'BannerToItem', () => 1615);
    assert.equal(r.call('Terraria.GameContent.BannerSystem', 'NPCtoBanner', 697), 697);
    assert.equal(r.call('Terraria.GameContent.BannerSystem', 'NPCtoBanner', 698), 697);
    assert.equal(r.call('Terraria.GameContent.BannerSystem', 'NPCtoBanner', 1), 1);
    assert.equal(r.call('Terraria.GameContent.BannerSystem', 'BannerToItem', 697), 6196);
    assert.equal(r.call('Terraria.GameContent.BannerSystem', 'BannerToItem', 1), 1615);
    assert.equal(r.call('Terraria.GameContent.BannerSystem', 'BannerToNPC', 697), 697);
    assert.equal(r.systems.length, 1); assert.equal(r.systems[0].key, 'BunnyLoader/Banners');
    assert.equal(r.B.killCount.length, 700); assert.equal(r.Main.SceneMetrics.NPCBannerBuff.length, 700);
});
test('world save uses vanilla length; full network state uses extended length; references restore on failure', () => {
    const r = bannerRuntime(), array = r.B.killCount;
    r.B.killCount[697] = 51;
    r.vanilla('Terraria.GameContent.BannerSystem', 'Save', () => r.B.killCount.length);
    assert.equal(r.call('Terraria.GameContent.BannerSystem', 'Save', {}), 293);
    assert.equal(r.B.killCount, array);
    r.vanilla('Terraria.GameContent.BannerSystem.NetBannersModule', 'WriteFullState', () => r.call('Terraria.GameContent.BannerSystem', 'Save', {}));
    assert.equal(r.call('Terraria.GameContent.BannerSystem.NetBannersModule', 'WriteFullState'), 700);
    r.vanilla('Terraria.GameContent.BannerSystem', 'Save', () => { throw Error('writer'); });
    assert.throws(() => r.call('Terraria.GameContent.BannerSystem', 'Save', {}), /writer/);
    assert.equal(r.B.killCount, array); assert.equal(r.B.killCount[697], 51);
});
test('banner persistence keys survive reordered registration and absent mods', () => {
    const r = bannerRuntime();
    r.B.killCount[697] = 99; r.B.claimableBanners[697] = 1; r.B.newBanner[697] = true;
    const data = r.api.BannerLoader.Snapshot();
    r.run('const template = NPCLoader.ByType.get(697); NPCLoader.ByType.delete(697); template.Type = 699; NPCLoader.ByType.set(699, template);');
    data['absent/Enemy'] = { count: 200, claimable: 4, fresh: true };
    // Register one extra type to keep the native array size consistent.
    r.run('NPCLoader.ByType.set(697, { Type: 697, Mod: bl.mod, constructor: { name: "NewEnemy" } });');
    r.api.BannerLoader.Restore(data);
    assert.equal(r.B.killCount[699], 99); assert.equal(r.B.claimableBanners[699], 1); assert.equal(r.B.newBanner[699], true);
    assert.equal(r.B.killCount[697], 0);
    eq(r.api.BannerLoader.Snapshot()['absent/Enemy'], data['absent/Enemy']);
});
test('invalid banner values cannot corrupt typed native counters', () => {
    const r = bannerRuntime();
    r.api.BannerLoader.Restore({ 'mod-a/Enemy': { count: -5, claimable: NaN, fresh: true } });
    assert.equal(r.B.killCount[697], 0); assert.equal(r.B.newBanner[697], false);
    r.api.BannerLoader.Restore({ 'mod-a/Enemy': { count: 1e12, claimable: 1e9, fresh: true } });
    assert.equal(r.B.killCount[697], 2147483647); assert.equal(r.B.claimableBanners[697], 65535);
});
test('internal world system serializes banner data with the existing network writer', () => {
    const sender = bannerRuntime(), receiver = bannerRuntime();
    sender.B.killCount[697] = 125; sender.B.claimableBanners[697] = 2; sender.B.newBanner[697] = true;
    let payload;
    sender.systems[0].inst.NetSend({ Write(value) { payload = JSON.stringify(value); } });
    receiver.systems[0].inst.NetReceive({ Read() { return JSON.parse(payload); } });
    assert.equal(receiver.B.killCount[697], 125);
    assert.equal(receiver.B.claimableBanners[697], 2);
    assert.equal(receiver.B.newBanner[697], true);
});
test('clear removes mod counters and retained data; fresh scene metrics resize before reset', () => {
    const r = bannerRuntime();
    r.api.BannerLoader.Restore({ 'mod-a/Enemy': { count: 5, claimable: 1 }, 'absent/x': { count: 9 } });
    r.call('Terraria.GameContent.BannerSystem', 'Clear');
    assert.equal(r.B.killCount[697], 0); eq(r.api.BannerLoader.Snapshot(), {});
    const metrics = { NPCBannerBuff: r.array(293, false) };
    r.vanilla('Terraria.SceneMetrics', 'Reset', m => { assert.equal(m.NPCBannerBuff.length, 700); });
    r.call('Terraria.SceneMetrics', 'Reset', metrics);
});
test('banner counters and buffs include registered NPCs beyond gaps in native IDs', () => {
    const r = bannerRuntime();
    r.api.NPCLoader.ByType.set(905, r.api.NPCLoader.ByType.get(697));
    r.api.BannerLoader.Resize();
    assert.equal(r.B.killCount.length, 906);
    assert.equal(r.B.claimableBanners.length, 906);
    assert.equal(r.Main.SceneMetrics.NPCBannerBuff.length, 906);
    r.B.killCount[905] = 50; r.Main.SceneMetrics.NPCBannerBuff[905] = true;
    assert.equal(r.B.killCount[697], 0);
    assert.equal(r.Main.SceneMetrics.NPCBannerBuff[697], false);
});
test('placed banner effect respects scan radius, style mapping and disabled strength', () => {
    const r = bannerRuntime(), tile = new r.api.ModBannerTile(); tile.Type = 753;
    r.api.TileLoader.StyleAt = () => 2;
    r.api.TileLoader.GetItemDropFromTypeAndStyle = (type, style) => type === 753 && style === 2 ? 6196 : 0;
    r.Terraria.ID.ItemID.Sets.BannerStrength[6196] = { Enabled: true };
    tile.NearbyEffects(1, 2, true); assert.equal(r.Main.SceneMetrics.hasBanner, false);
    tile.NearbyEffects(1, 2, false); assert.equal(r.Main.SceneMetrics.NPCBannerBuff[697], true);
    r.Main.SceneMetrics.NPCBannerBuff[697] = false;
    r.Terraria.ID.ItemID.Sets.BannerStrength[6196].Enabled = false;
    tile.NearbyEffects(1, 2, false); assert.equal(r.Main.SceneMetrics.NPCBannerBuff[697], false);
});
for (const horizontal of [true, false]) test('native tile style calculation supports wrap, alternates and line skip horizontal=' + horizontal, () => {
    const r = createRuntime();
    const data = { CoordinateFullWidth: 18, CoordinateFullHeight: 54, StyleHorizontal: horizontal, StyleWrapLimit: 4, StyleLineSkip: 2, StyleMultiplier: 2 };
    r.api.TileLoader.ObjectData = () => data;
    for (let style = 0; style < 12; style++) for (let alt = 0; alt < 2; alt++) for (let cell = 0; cell < 3; cell++) {
        const number = style * 2 + alt, a = number % 4, b = Math.floor(number / 4) * 2;
        r.api.TileLoader.Tile = () => ({ type: 753, frameX: (horizontal ? a : b) * 18, frameY: (horizontal ? b : a) * 54 + cell * 18 });
        assert.equal(r.api.TileLoader.StyleAt(0, 0), style);
    }
});
console.log(`apiexpansion: ${checks} behavioral cases passed; native calls simulated, signatures checked against refs/dump.cs.`);
