import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const directory = path.dirname(fileURLToPath(import.meta.url));
const worldName = 'BL_GenPass_proof';
const marker = { x: 2180, y: 100, width: 7, height: 5, typeA: 7, typeB: 166 };
const proof = { worldName, marker, events: ['pre', 'genpass-before', 'genpass-structure', 'passlegacy-after', 'post'],
    subclassRuns: 1, legacyRuns: 1, disabledRuns: 0, failures: [] };
const Main = { ActiveWorldFileData: { Name: worldName }, tile: {
    'Tile get_Item(int x, int y)'(x, y) {
        assert.ok(x >= marker.x && x < marker.x + marker.width);
        assert.ok(y >= marker.y && y < marker.y + marker.height);
        return { type: (x - marker.x + y - marker.y) % 2 ? marker.typeA : marker.typeB,
            'bool active()': () => true };
    },
} };
const logs = [];
const hook = { hook() {} };
const context = vm.createContext({ worldName, Mod: class {}, ModSystem: class {}, GenPass: class {},
    PassLegacy: class {}, Terraria: { Main, WorldBuilding: { WorldGenerator: {
        'GenPassResult RunPass(GenPass pass)': hook,
    } }, IO: { WorldFile: { 'void InternalSaveWorld(bool useCloudSaving, bool resetTime)': hook } } },
    bl: { classOf: () => ({ 'void CreateWorld()': hook }), log: value => logs.push(value) },
});
const source = fs.readFileSync(path.join(directory, 'content/main.js'), 'utf8')
    .replace("import { worldName } from './config.js';", '')
    .replace('export class FullWorldSystem', 'class FullWorldSystem')
    .replace('export default class FullGenerationChecks', 'class FullGenerationChecks');
vm.runInContext(source + '\nglobalThis.fixture = new FullWorldSystem();', context);
context.fixture.LoadWorldData({ Get: () => proof });
const reloaded = JSON.parse(logs.at(-1).split('GENPASS_RELOADED ')[1]);
assert.equal(reloaded.passed, true);
assert.equal(reloaded.matchingTiles, 35);
assert.equal(reloaded.generationCallbacksThisProcess, 0);
let saved;
context.fixture.SaveWorldData({ Set(key, value) { assert.equal(key, 'proof'); saved = value; } });
assert.deepEqual(saved, proof);
assert.equal(saved.subclassRuns, 1);
assert.equal(saved.legacyRuns, 1);
Main.ActiveWorldFileData.Name = 'Other world';
let writes = 0;
context.fixture.SaveWorldData({ Set() { writes++; } });
assert.equal(writes, 0);
console.log('PASS: loaded generation proof survives a later save; unrelated worlds remain untouched');
