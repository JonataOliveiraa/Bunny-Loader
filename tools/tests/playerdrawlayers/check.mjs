import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import { drawHarness as h } from '../modplayerhooks/check.mjs';

const { PlayerDrawLayer: Layer, PlayerDrawLayers: Native, ModPlayer, All, p, second } = h;
const nativeOwner = 'Terraria.DataStructures.PlayerDrawLayers', rendererOwner = 'Terraria.Graphics.Renderers.LegacyPlayerRenderer';
let checks = 0, clones = 0, setupCalls = 0, positionCalls = 0, nested = null;
const queue = [];
const append = (info, name) => {
    const data = { name, player: info.drawPlayer.whoAmI, shader: info.cWings, texture: info.texture };
    assert.equal(ModPlayer.AddDrawData(info, data), true);
    queue.push(name);
};
class WingsBefore extends Layer {
    SetStaticDefaults() { setupCalls++; }
    GetDefaultPosition() { positionCalls++; return Layer.BeforeParent(Native.Wings); }
    GetDefaultVisibility(info) { return info.visible; }
    Draw(info) { append(info, 'before'); }
}
class WingsAfter extends Layer {
    GetDefaultPosition() { positionCalls++; return Layer.AfterParent('Wings'); }
    GetDefaultVisibility(info) { return info.visible; }
    Draw(info) { if (nested) { const target = nested; nested = null; draw(target); } append(info, 'after'); }
}
class ChildBefore extends Layer {
    GetDefaultPosition() { positionCalls++; return Layer.BeforeParent(WingsAfter); }
    Draw(info) { append(info, 'child-before'); }
}
class ChildAfter extends Layer {
    GetDefaultPosition() { positionCalls++; return Layer.AfterParent(WingsAfter); }
    Draw(info) { append(info, 'child-after'); }
}
const before = Layer.register(WingsBefore), after = Layer.register(WingsAfter);
const childBefore = Layer.register(ChildBefore), childAfter = Layer.register(ChildAfter);
function make(player = p, visible = true) {
    const cache = Array(64).fill(null), prefix = { name: 'prefix', shader: 7 }, suffix = { name: 'suffix', shader: 9 };
    cache[0] = prefix;
    cache.cloneResized = length => { clones++; return cache.slice(0, length); };
    return { drawPlayer: player, DrawDataCache: cache, DrawDataCacheCount: 1, cWings: player.whoAmI + 20, texture: {}, visible, shadow: 0, prefix, suffix };
}
function draw(info = make()) { h.call(rendererOwner, 'DrawPlayer_UseNormalLayers', info); return info; }
const names = info => info.DrawDataCache.slice(0, info.DrawDataCacheCount).map(value => value.name);
function normal(info) {
    for (const name of ['Wings', 'Skin', 'Head', 'FrontAcc']) h.call(nativeOwner, Native[name].Method, info);
    info.DrawDataCache[info.DrawDataCacheCount++] = info.suffix;
}
for (const name of ['Wings', 'Skin', 'Head', 'FrontAccFront', 'FrontAccBack']) h.vanilla(nativeOwner, Native[name].Method, info => append(info, name));
h.vanilla(nativeOwner, Native.FrontAcc.Method, info => {
    h.call(nativeOwner, Native.FrontAccBack.Method, info);
    h.call(nativeOwner, Native.FrontAccFront.Method, info);
});
h.vanilla(rendererOwner, 'DrawPlayer_UseNormalLayers', normal);
function test(name, run) {
    h.reset(); queue.length = 0; nested = null;
    const errorCount = h.errors.length;
    run();
    assert.equal(h.errors.length, errorCount, name + ': unexpected callback failure');
    assert.equal(h.flags.get('player.DrawLayers'), false);
    let outsideCalls = 0;
    for (const native of Object.values(Native)) {
        const entry = h.method(nativeOwner, native.Method), callback = entry.callbacks[0], original = entry.vanilla;
        entry.callbacks[0] = (...args) => { outsideCalls++; return callback(...args); }; entry.vanilla = () => {};
        try { h.invoke(entry, [{}]); } finally { entry.callbacks[0] = callback; entry.vanilla = original; }
    }
    assert.equal(outsideCalls, 0, name + ': layers must stay outside JS after the gate closes');
    checks++;
}
const baseNames = ['prefix', 'before', 'Wings', 'child-before', 'after', 'child-after', 'Skin', 'Head', 'FrontAccBack', 'FrontAccFront', 'suffix'];
test('registration owns a template and initializes once at content readiness', () => {
    assert.equal(vm.runInContext('Templates.Get', h.context)(WingsBefore), before);
    assert.equal(before.Mod.uuid, 'test'); assert.equal(before.FullName, 'test/WingsBefore');
    assert.equal(setupCalls, 0);
    for (const task of h.ready) task();
    assert.equal(setupCalls, 1);
    draw(); draw(); assert.equal(setupCalls, 1);
});
test('recursive children draw around their parents and preserve cache data', () => {
    const info = draw();
    assert.deepEqual(names(info), baseNames);
    assert.equal(info.DrawDataCache[0], info.prefix); assert.equal(info.DrawDataCache[info.DrawDataCacheCount - 1], info.suffix);
    for (const data of info.DrawDataCache.slice(1, -1).filter(Boolean)) {
        if (data === info.suffix) continue;
        assert.equal(data.shader, info.cWings); assert.equal(data.texture, info.texture);
    }
    assert.equal(clones, 0);
});
test('default positions are cached across players and frames', () => {
    const count = positionCalls;
    for (let i = 0; i < 50; i++) draw(make(i % 2 ? p : second));
    assert.equal(positionCalls, count);
});
test('a single active anchor dispatches no unrelated native layer hooks', () => {
    const counts = new Map(), originals = [];
    for (const native of Object.values(Native)) {
        const entry = h.method(nativeOwner, native.Method), original = entry.callbacks[0];
        originals.push([entry, original]);
        entry.callbacks[0] = (...args) => { counts.set(native, (counts.get(native) || 0) + 1); return original(...args); };
    }
    try { draw(); } finally { for (const [entry, original] of originals) entry.callbacks[0] = original; }
    assert.deepEqual([...counts].map(([layer, count]) => [layer.Name, count]), [['Wings', 1]]);
});
test('an invisible custom parent suppresses its descendants without JS native dispatch', () => {
    const info = draw(make(p, false));
    assert.deepEqual(names(info), ['prefix', 'Wings', 'Skin', 'Head', 'FrontAccBack', 'FrontAccFront', 'suffix']);
});
test('hiding a native parent suppresses the whole custom subtree', () => {
    All.prototype.HideDrawLayers = () => Native.Wings.Hide();
    assert.deepEqual(names(draw()), ['prefix', 'Skin', 'Head', 'FrontAccBack', 'FrontAccFront', 'suffix']);
    assert.equal(Native.Wings.Visible, true);
});
test('stable visibility updates no individual native flags across consecutive draws', () => {
    draw();
    const set = h.sandbox.bl.hookFlags.set, changed = [];
    h.sandbox.bl.hookFlags.set = (key, value) => { if (key.startsWith('player.DrawLayer.')) changed.push([key, value]); set(key, value); };
    try { for (let index = 0; index < 20; index++) draw(); }
    finally { h.sandbox.bl.hookFlags.set = set; }
    assert.deepEqual(changed, []);
});
test('hiding a custom parent preserves siblings and restores its visibility', () => {
    All.prototype.HideDrawLayers = () => after.Hide();
    assert.deepEqual(names(draw()), ['prefix', 'before', 'Wings', 'Skin', 'Head', 'FrontAccBack', 'FrontAccFront', 'suffix']);
    assert.equal(after.Visible, true);
});
test('moving a native parent moves its complete custom bundle', () => {
    All.prototype.ModifyDrawLayerOrdering = (_, positions) => positions.set(Native.Wings, Layer.AfterParent(Native.Head));
    assert.deepEqual(names(draw()), ['prefix', 'Skin', 'Head', 'before', 'Wings', 'child-before', 'after', 'child-after', 'FrontAccBack', 'FrontAccFront', 'suffix']);
});
test('custom class keys can change parent for this draw without leaking defaults', () => {
    All.prototype.ModifyDrawLayerOrdering = (player, positions) => { if (player === p) positions.set(WingsAfter, Layer.BeforeParent('Head')); };
    assert.deepEqual(names(draw()), ['prefix', 'before', 'Wings', 'Skin', 'child-before', 'after', 'child-after', 'Head', 'FrontAccBack', 'FrontAccFront', 'suffix']);
    assert.deepEqual(names(draw(make(second))), baseNames);
});
test('mutating position objects never changes the cached defaults', () => {
    All.prototype.ModifyDrawLayerOrdering = (player, positions) => {
        if (player !== p) return;
        const value = positions.get(after); value.After = Native.Head;
    };
    const info = draw(); assert.equal(names(info).indexOf('after') > names(info).indexOf('Head'), true);
    assert.deepEqual(names(draw(make(second))), baseNames);
});
test('deleting a custom position suppresses its branch for one draw', () => {
    All.prototype.ModifyDrawLayerOrdering = (_, positions) => positions.delete(after);
    assert.deepEqual(names(draw()), ['prefix', 'before', 'Wings', 'Skin', 'Head', 'FrontAccBack', 'FrontAccFront', 'suffix']);
});
test('custom position cycles skip only their affected branch', () => {
    All.prototype.ModifyDrawLayerOrdering = (_, positions) => positions.set(after, Layer.AfterParent(childAfter));
    assert.deepEqual(names(draw()), ['prefix', 'before', 'Wings', 'Skin', 'Head', 'FrontAccBack', 'FrontAccFront', 'suffix']);
});
test('unknown and unregistered parents cannot render a custom layer', () => {
    for (const parent of ['missing', new WingsBefore()]) {
        All.prototype.ModifyDrawLayerOrdering = (_, positions) => positions.set(after, Layer.AfterParent(parent));
        assert.equal(names(draw()).includes('after'), false);
    }
});
test('two nested player draws restore each scope and retain separate shaders', () => {
    const outer = make(p), inner = make(second);
    nested = inner;
    All.prototype.HideDrawLayers = player => { if (player === second) before.Hide(); };
    draw(outer);
    assert.deepEqual(names(outer), baseNames);
    assert.deepEqual(names(inner), baseNames.filter(name => name !== 'before'));
    for (const info of [outer, inner]) for (const data of info.DrawDataCache.slice(1, info.DrawDataCacheCount - 1)) {
        assert.equal(data.player, info.drawPlayer.whoAmI); assert.equal(data.shader, info.cWings);
    }
});
test('native renderer failure restores custom visibility and every native filter', () => {
    All.prototype.HideDrawLayers = () => before.Hide();
    h.vanilla(rendererOwner, 'DrawPlayer_UseNormalLayers', () => { throw Error('draw failure'); });
    try { assert.throws(() => draw(), /draw failure/); } finally { h.vanilla(rendererOwner, 'DrawPlayer_UseNormalLayers', normal); }
    assert.equal(before.Visible, true);
});
test('callback exceptions do not prevent siblings or native drawing', () => {
    const saved = after.Draw;
    after.Draw = () => { throw Error('mod draw failure'); };
    const count = h.errors.length;
    try { assert.deepEqual(names(draw()), baseNames.filter(name => name !== 'after')); } finally { after.Draw = saved; }
    assert.match(h.errors.pop(), /WingsAfter.Draw: mod draw failure/); assert.equal(h.errors.length, count);
});
test('visibility exceptions hide only the failed branch', () => {
    const saved = after.GetDefaultVisibility;
    after.GetDefaultVisibility = () => { throw Error('visibility failure'); };
    const count = h.errors.length;
    try { assert.deepEqual(names(draw()), ['prefix', 'before', 'Wings', 'Skin', 'Head', 'FrontAccBack', 'FrontAccFront', 'suffix']); }
    finally { after.GetDefaultVisibility = saved; }
    assert.match(h.errors.pop(), /GetDefaultVisibility: visibility failure/); assert.equal(h.errors.length, count);
});
test('bad position getters are isolated from the native renderer', () => {
    All.prototype.ModifyDrawLayerOrdering = (_, positions) => positions.set(after, { get Before() { throw Error('position failure'); } });
    const count = h.errors.length;
    assert.equal(names(draw()).includes('after'), false);
    assert.match(h.errors.pop(), /Position: position failure/); assert.equal(h.errors.length, count);
});
test('late registration invalidates plans and initializes before first drawing', () => {
    let staticCalls = 0;
    class FrontChild extends Layer {
        SetStaticDefaults() { staticCalls++; }
        GetDefaultPosition() { return Layer.AfterParent(Native.FrontAccFront); }
        GetDefaultVisibility(info) { return info.front; }
        Draw(info) { append(info, 'front-child'); }
    }
    Layer.register(FrontChild);
    const info = make(); info.front = true;
    draw(info); assert.equal(staticCalls, 1);
    assert.deepEqual(names(info), [...baseNames.slice(0, -1), 'front-child', 'suffix']);
    for (const task of h.ready) task(); assert.equal(staticCalls, 1);
    All.prototype.HideDrawLayers = () => Native.FrontAcc.Hide();
    const hidden = make(); hidden.front = true; draw(hidden);
    assert.equal(names(hidden).includes('front-child'), false);
});
test('duplicate registration and incomplete classes are rejected before adoption', () => {
    assert.throws(() => Layer.register(WingsBefore), /registrado/);
    assert.throws(() => Layer.register(class Incomplete extends Layer {}), /Draw e GetDefaultPosition/);
    assert.throws(() => Layer.register(class Unrelated {}), /estende/);
});
test('same named layers from different mods resolve by full name and instance', () => {
    const original = h.sandbox.bl.mod;
    class SameName extends Layer {
        constructor() { super('WingsAfter'); }
        GetDefaultPosition() { return Layer.AfterParent(Native.Head); }
        GetDefaultVisibility() { return false; }
        Draw() {}
    }
    h.sandbox.bl.mod = { uuid: 'other' };
    try { Layer.register(SameName); } finally { h.sandbox.bl.mod = original; }
    All.prototype.ModifyDrawLayerOrdering = (_, positions) => positions.set('test/WingsAfter', Layer.AfterParent(Native.Head));
    const info = draw(); assert.ok(names(info).indexOf('after') > names(info).indexOf('Head'));
});
test('autoload discovers exports once, honors opt-out, and loads items before layers', () => {
    const bases = ['Mod', 'ModConfig', 'ModRarity', 'DamageClass', 'ModMount', 'ModBuff', 'ModPrefix', 'ModSurfaceBackgroundStyle', 'ModUndergroundBackgroundStyle', 'ModWaterfallStyle', 'ModWaterStyle', 'ModBiome', 'ModSceneEffect', 'ModMenu', 'ModProjectile', 'ModTile', 'ModWall', 'ModSystem', 'GlobalItem', 'GlobalNPC', 'GlobalProjectile', 'GlobalLoot', 'ModCommand', 'ModHair', 'ModCloud', 'ModEmoteBubble', 'ModAchievement'];
    for (const name of bases) vm.runInContext(`if (typeof ${name} === 'undefined') globalThis.${name} = class {};`, h.context);
    h.sandbox.LocalizationLoader = { Load() {} }; h.sandbox.BackgroundTextureLoader = { Autoload() {} }; h.sandbox.CloudLoader = { Autoload() {} };
    h.sandbox.ModRegistry.Adopt = (_, main) => new main().Load();
    let load; h.sandbox.bl.__setModLoader = fn => { load = fn; };
    vm.runInContext(fs.readFileSync(path.join(h.source, 'Core/ContentAutoload.js'), 'utf8'), h.context);
    const Mod = vm.runInContext('Mod', h.context), ModItem = vm.runInContext('ModItem', h.context);
    let itemsReady = false;
    const saved = ModItem.register;
    class AutoItem extends ModItem {}
    class AutoLayer extends Layer {
        constructor() { super(); assert.equal(itemsReady, true); }
        GetDefaultPosition() { return Layer.AfterParent(Native.Wings); }
        GetDefaultVisibility() { return false; }
        Draw() {}
    }
    class DisabledLayer extends AutoLayer { static Autoload = false; }
    class Entry extends Mod { Load() { assert.ok(vm.runInContext('Templates.Get', h.context)(AutoLayer)); } }
    ModItem.register = () => { itemsReady = true; };
    try { load([['main.js', { default: Entry, AutoLayer }], ['Content/layers.js', { AutoItem, AutoLayer, DisabledLayer }]]); }
    finally { ModItem.register = saved; }
    assert.equal(vm.runInContext('Templates.Get', h.context)(DisabledLayer), undefined);
});
assert.equal(h.errors.length, 0);
const sampleSource = fs.readFileSync(new URL('../../../samples/ExampleMod/content/Common/Players/WingsGlowmask.js', import.meta.url), 'utf8');
const built = [], sampleMain = { screenPosition: { X: 100, Y: 200 } };
const sampleContext = vm.createContext({
    PlayerDrawLayer: Layer, PlayerDrawLayers: Native, ModPlayer,
    Terraria: { Main: sampleMain, DataStructures: { DrawData: { new: () => {
        const data = {};
        const proxy = new Proxy(data, { get(target, key) {
            if (String(key).includes('.ctor(')) return (...args) => { data.args = args; built.push(proxy); };
            return target[key];
        } });
        return proxy;
    } } } },
    Vector2: { new: (X, Y) => ({ X, Y }) }, Rectangle: { new: (X, Y, Width, Height) => ({ X, Y, Width, Height }) }
});
vm.runInContext(sampleSource.replace('export class ', 'class ') + '\nglobalThis.Glow = WingsGlowmask;', sampleContext);
const Glow = sampleContext.Glow, texture = { Width: 32, Height: 80 }, color = {};
const glowPlayer = { wings: 8, dead: false, invis: false, wingFrame: 2, width: 20, height: 42, bodyFrame: { Height: 56 }, bodyRotation: 0.3,
    Directions: { X: -1, Y: 1 }, 'Color GetImmuneAlphaPure(Color newColor, float alphaReduction)': (value, shadow) => ({ value, shadow }) };
function glowInfo() { return { drawPlayer: glowPlayer, Position: { X: 120.5, Y: 225.9 }, DrawDataCache: Array(10), DrawDataCacheCount: 0, playerEffect: 2, cWings: 37, shadow: 0.5 }; }
test('glowmask registers a configuration once and rejects invalid slots, frames and offsets', () => {
    Glow.RegisterData(8, { Texture: texture, Color: color });
    Glow.RegisterData(8, { Texture: { Width: 64, Height: 80 }, Color: {} });
    const ref = {}; assert.equal(Glow.TryGetValue(8, ref), true); assert.equal(ref.value.Texture, texture);
    assert.equal(Glow.TryGetValue(9, {}), false);
    for (const slot of [-1, 0, 1.5]) assert.throws(() => Glow.RegisterData(slot, { Texture: texture, Color: color }), /wingSlot/);
    assert.throws(() => Glow.RegisterData(9, { Texture: texture, Color: color, Frames: 3 }), /Frames/);
    assert.throws(() => Glow.RegisterData(9, { Texture: texture, Color: color, Offset: { X: NaN, Y: 0 } }), /Offset/);
    assert.throws(() => Glow.RegisterData(9, { Texture: { Width: 0, Height: 80 }, Color: color }), /dimensoes/);
});
test('glowmask queues the actual DrawData with correct frame, geometry, shader and shadow alpha', () => {
    const info = glowInfo(); new Glow().Draw(info);
    const data = info.DrawDataCache[0]; assert.equal(data, built.at(-1)); assert.equal(info.DrawDataCacheCount, 1);
    const args = data.args;
    assert.equal(args[0], texture); assert.deepEqual({ ...args[1] }, { X: 39, Y: 48 });
    assert.deepEqual({ ...args[2] }, { X: 0, Y: 40, Width: 32, Height: 20 });
    assert.deepEqual(args[3], { value: color, shadow: 0.5 }); assert.equal(args[4], 0.3);
    assert.deepEqual({ ...args[5] }, { X: 16, Y: 10 }); assert.equal(args[7], 2); assert.equal(data.shader, 37);
});
test('glowmask skips dead, invisible, unequipped and unregistered wing slots', () => {
    const layer = new Glow(), info = glowInfo(); assert.equal(layer.GetDefaultVisibility(info), true);
    for (const field of ['dead', 'invis']) { glowPlayer[field] = true; assert.equal(layer.GetDefaultVisibility(info), false); glowPlayer[field] = false; }
    for (const slot of [-1, 0, 9]) { glowPlayer.wings = slot; assert.equal(layer.GetDefaultVisibility(info), false); }
    glowPlayer.wings = 8;
});
test('glowmask rejects out-of-range animation frames without adding draw data', () => {
    for (const frame of [-1, 4, 20]) {
        const info = glowInfo(); glowPlayer.wingFrame = frame; new Glow().Draw(info); assert.equal(info.DrawDataCacheCount, 0);
    }
    glowPlayer.wingFrame = 2;
});
test('a custom layer installs the native pipeline even without any ModPlayer', () => {
    const hooks = new Map(), tasks = [], reports = [];
    const nativeMethod = name => {
        if (hooks.has(name)) return hooks.get(name);
        const entry = { callbacks: [], filters: [], active: 0, vanilla: () => {} };
        const fn = (...args) => h.invoke(entry, args); fn.entry = entry;
        fn.hook = (callback, filter = {}) => { entry.callbacks.push(callback); entry.filters.push(filter); };
        hooks.set(name, fn); return fn;
    };
    const layerMethods = new Proxy({}, { get: (_, name) => nativeMethod(name) });
    const isolated = vm.createContext({
        Terraria: { Graphics: { Renderers: { LegacyPlayerRenderer: layerMethods } }, DataStructures: { PlayerDrawLayers: layerMethods } },
        bl: { ...h.sandbox.bl, classOf: () => layerMethods },
        Hooks: { Once: (_, install) => install(), Overrides: h.sandbox.Hooks.Overrides },
        Ready: { Add: task => tasks.push(task) },
        Safe: { Report: (...args) => reports.push(args), Once: (...args) => reports.push(args) },
        PlayerLoader: { Has: () => false, Call() {} }
    });
    for (const file of ['Core/Templates.js', 'Loaders/PlayerDrawHooks.js']) vm.runInContext(fs.readFileSync(path.join(h.source, file), 'utf8'), isolated);
    const api = vm.runInContext('({ PlayerDrawLayer, PlayerDrawLayers })', isolated);
    let setups = 0;
    class OnlyLayer extends api.PlayerDrawLayer {
        GetDefaultPosition() { return api.PlayerDrawLayer.AfterParent(api.PlayerDrawLayers.Wings); }
        SetStaticDefaults() { setups++; }
        Draw(info) { info.DrawDataCache[info.DrawDataCacheCount++] = 'custom'; }
    }
    api.PlayerDrawLayer.register(OnlyLayer);
    const wings = nativeMethod('void DrawPlayer_09_Wings(PlayerDrawSet drawinfo)');
    wings.entry.vanilla = info => { info.DrawDataCache[info.DrawDataCacheCount++] = 'native'; };
    const gate = nativeMethod('void DrawPlayer_UseNormalLayers(PlayerDrawSet drawinfo)'); gate.entry.vanilla = info => wings(info);
    const info = { DrawDataCache: Array(4), DrawDataCacheCount: 0 };
    gate(info); assert.deepEqual(info.DrawDataCache.slice(0, info.DrawDataCacheCount), ['native', 'custom']);
    assert.equal(setups, 1); tasks.forEach(task => task()); assert.equal(setups, 1);
    assert.deepEqual(reports, []);
});
console.log(`${checks} custom player draw layer checks passed.`);
