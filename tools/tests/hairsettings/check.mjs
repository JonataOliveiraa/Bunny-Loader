import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'app/src/main/cpp/script/js');
const signature = 'void GetHairSettings(out bool fullHair, out bool hatHair, out bool hideHair, out bool backHairDraw, out bool drawsBackHairWithoutHeadgear)';
assert.ok(fs.readFileSync(path.join(root, 'refs/dump.cs'), 'utf8').includes('public ' + signature + ' { }'));
const callbacks = [], errors = [];
const ArmorIDs = { Head: { Sets: { HidesHead: [] } }, Face: { Sets: { PreventHairDraw: [] } } };
const context = vm.createContext({
    Terraria: { ID: { ArmorIDs }, Player: { [signature]: { hook: (callback) => callbacks.push(callback) } } },
    Safe: { Run(label, run) { try { return run(); } catch (error) { errors.push([label, error]); } } },
});
for (const file of ['ModHelpers.js', 'mod/Core/Hooks.js', 'mod/Loaders/ArmorSetLoader.js']) {
    vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
}
Object.assign(ArmorIDs.Head.Sets, context.__blExtraStatics['Terraria.ID.ArmorIDs.Head.Sets']);
const HairID = context.Terraria.ID.HairID = context.__blExtraClasses['Terraria.ID'].HairID;
vm.runInContext('ArmorSetLoader.InstallHairSettings(); ArmorSetLoader.InstallHairSettings();', context);
assert.equal(callbacks.length, 1);
assert.deepEqual(errors, []);
const head = ArmorIDs.Head.Sets, hair = HairID.Sets;
let checks = 0;
function settings(fields = {}) {
    const player = { head: 0, face: -1, faceHead: -1, hair: 0, ...fields };
    const outputs = Array.from({ length: 5 }, () => ({ value: 'old' }));
    callbacks[0](() => assert.fail('GetHairSettings must replace the native implementation'), player, ...outputs);
    return outputs.map((output) => output.value);
}
function test(label, run) { run(); checks++; console.log(label + ': ok'); }

test('vanilla head and hair defaults', () => {
    assert.deepEqual(settings({ head: 10, hair: 51 }), [true, false, false, true, false]);
    assert.deepEqual(settings({ head: 14 }), [false, true, false, false, false]);
    assert.deepEqual(settings({ hair: 6 }), [false, false, false, true, true]);
    assert.deepEqual(settings({ head: 23 }), [false, false, false, false, true]);
    assert.deepEqual(settings({ head: 259 }), [false, false, false, false, true]);
    assert.deepEqual(settings({ head: 1 }), [false, false, false, false, false]);
});
test('negative slots never use table entries', () => {
    for (const table of [head.DrawFullHair, head.DrawHatHair, head.DrawsBackHairWithoutHeadgear, hair.DrawBackHair, ArmorIDs.Face.Sets.PreventHairDraw]) table[-1] = true;
    assert.deepEqual(settings({ head: -1, hair: -1 }), [false, false, false, false, false]);
});
test('unmarked mod slots default to false', () => {
    assert.deepEqual(settings({ head: 500, hair: 300 }), [false, false, false, false, false]);
});
test('SetStaticDefaults configures the equip slot of a mod hat', () => {
    const item = { headSlot: 501, type: 10000 };
    class Hat {
        Item = item;
        SetStaticDefaults() { ArmorIDs.Head.Sets.DrawHatHair[this.Item.headSlot] = true; }
    }
    new Hat().SetStaticDefaults();
    assert.deepEqual(settings({ head: item.headSlot }), [false, true, false, false, false]);
    assert.equal(head.DrawHatHair[item.type], false);
});
test('sets remain mutable after hook installation', () => {
    head.DrawHatHair[501] = false;
    head.DrawFullHair[501] = true;
    assert.deepEqual(settings({ head: 501 }), [true, false, false, false, false]);
    head.DrawFullHair[501] = false;
    head.DrawsBackHairWithoutHeadgear[501] = true;
    assert.deepEqual(settings({ head: 501 }), [false, false, false, false, true]);
});
test('a mod hairstyle can enable back hair', () => {
    hair.DrawBackHair[300] = true;
    assert.deepEqual(settings({ head: 501, hair: 300 }), [false, false, false, true, true]);
    hair.DrawBackHair[300] = false;
    assert.deepEqual(settings({ head: 501, hair: 300 }), [false, false, false, false, true]);
});
test('face equipment can prevent hair drawing', () => {
    ArmorIDs.Face.Sets.PreventHairDraw[3] = true;
    assert.deepEqual(settings({ head: 14, face: 3, hair: 51 }), [false, true, true, true, false]);
    ArmorIDs.Face.Sets.PreventHairDraw[3] = false;
    assert.deepEqual(settings({ head: 14, face: 3 }), [false, true, false, false, false]);
});
test('faceHead hides hair only with a nonzero head slot', () => {
    assert.equal(settings({ head: 14, faceHead: 0 })[2], true);
    assert.equal(settings({ head: -1, faceHead: 0 })[2], true);
    assert.equal(settings({ head: 0, faceHead: 0 })[2], false);
    assert.equal(settings({ head: 14, faceHead: -1 })[2], false);
});
test('all five outputs are overwritten on each call', () => {
    assert.deepEqual(settings({ head: 1, hair: 0 }), [false, false, false, false, false]);
    assert.deepEqual(settings(), [false, false, false, false, true]);
});

test('native equipment fields are read once per callback without persisting results', () => {
    const reads = {}, values = { head: 14, face: -1, faceHead: -1, hair: 51 }, player = {};
    for (const name of Object.keys(values)) Object.defineProperty(player, name, { get() { reads[name] = (reads[name] || 0) + 1; return values[name]; } });
    const outputs = Array.from({ length: 5 }, () => ({ value: null }));
    const invoke = () => callbacks[0](() => assert.fail('native body must be replaced'), player, ...outputs);
    invoke();
    assert.deepEqual(outputs.map(output => output.value), [false, true, false, true, false]);
    assert.deepEqual(reads, { head: 1, face: 1, faceHead: 1, hair: 1 });
    values.head = 10; values.hair = 0;
    invoke();
    assert.deepEqual(outputs.map(output => output.value), [true, false, false, false, false]);
    assert.deepEqual(reads, { head: 2, face: 2, faceHead: 2, hair: 2 });
});
test('DrawHead still maps to the native HidesHead set', () => {
    head.DrawHead[501] = false;
    assert.equal(ArmorIDs.Head.Sets.HidesHead[501], true);
    head.DrawHead[501] = true;
    assert.equal(ArmorIDs.Head.Sets.HidesHead[501], false);
});
assert.deepEqual(errors, []);
console.log(checks + ' hair settings checks passed.');
