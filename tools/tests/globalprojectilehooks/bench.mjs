import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const revision = process.argv[2], prefix = 'app/src/main/cpp/script/js/mod/';
const context = vm.createContext({ performance, assert, output: console.log,
    bl: { defineMethod() {} }, Templates: { Adopt() {} }, Ready: { Add() {} }, Entities: { Define() {} },
    Safe: { Run: (_, fn) => fn(), Report: (_, error) => { throw error; } },
});
for (const file of ['Core/Hooks.js', 'Core/GlobalType.js', 'Core/GlobalRegistry.js']) {
    const text = revision ? execFileSync('git', ['show', revision + ':' + prefix + file], { cwd: root, encoding: 'utf8' })
        : fs.readFileSync(path.join(root, prefix, file), 'utf8');
    vm.runInContext(text, context, { filename: file });
}
vm.runInContext(`
let calls = 0;
class Base extends GlobalType { Tick() {} Veto() { return true; } Empty() {} }
const registry = new GlobalRegistry(Base, () => ({}), 'globals', 'GetGlobal'), entity = { type: 1 };
for (let i = 0; i < 64; i++) {
    const Probe = class extends Base {};
    if (i < 8) { Probe.prototype.Tick = function () { calls++; }; Probe.prototype.Veto = function () { calls++; return true; }; }
    registry.Register(Probe, 'Global');
}
function measure(name, run, expected) {
    for (let i = 0; i < 10000; i++) run();
    const before = calls, samples = [];
    for (let repeat = 0; repeat < 7; repeat++) {
        const start = performance.now();
        for (let i = 0; i < 100000; i++) run();
        samples.push((performance.now() - start) * 1000 / 100000);
    }
    assert.equal(calls - before, expected * 700000);
    samples.sort((a, b) => a - b);
    output(JSON.stringify({ name, medianUs: samples[3], minUs: samples[0], callbacks: calls - before }));
}
const modern = !!registry.Call;
measure('dispatch_8_of_64', modern ? () => registry.Call(entity, 'Tick') : () => registry.Each(entity, 'Tick', g => g.Tick(entity)), 8);
measure('veto_8_of_64', modern ? () => registry.AllCall(entity, 'Veto') : () => registry.All(entity, 'Veto', g => g.Veto(entity)), 8);
measure('empty_64', modern ? () => registry.Call(entity, 'Empty') : () => registry.Each(entity, 'Empty', g => g.Empty(entity)), 0);
`, context);
