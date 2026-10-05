import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const prefix = 'app/src/main/cpp/script/js/mod/';
const revision = process.argv[2];
const context = vm.createContext({
    performance, output: console.log,
    bl: { mod: null, addressOf: player => player.address, error: error => { throw Error(error); }, log() {} },
});
for (const file of ['Core/Hooks.js', 'Core/Safe.js', 'ModPlayer.js', 'Loaders/PlayerLoader.js']) {
    const source = revision ? execFileSync('git', ['show', revision + ':' + prefix + file], { cwd: root, encoding: 'utf8' })
        : fs.readFileSync(path.join(root, prefix, file), 'utf8');
    vm.runInContext(source, context, { filename: file });
}
vm.runInContext(`
    let calls = 0;
    const player = { address: 1 };
    const measure = (name, fn) => {
        for (let i = 0; i < 10000; i++) fn();
        const samples = [];
        for (let r = 0; r < 7; r++) {
            const t = performance.now();
            for (let i = 0; i < 100000; i++) fn();
            samples.push((performance.now() - t) * 1000 / 100000);
        }
        samples.sort((a, b) => a - b);
        output(JSON.stringify({ name, medianUs: samples[3], minUs: samples[0] }));
    };
    for (let i = 0; i < 8; i++) {
        const Probe = class extends ModPlayer {
            PostUpdate() { calls++; }
            PreItemCheck() { calls++; return true; }
            UseSpeedMultiplier() { calls++; return 1; }
        };
        Object.defineProperty(Probe, 'name', { value: 'Probe' + i });
        PlayerLoader.Add(Probe);
    }
    PlayerLoader.Of(player);
    measure('Call_8', () => PlayerLoader.Call(player, 'PostUpdate'));
    measure('Call_vazio', () => PlayerLoader.Call(player, 'OnMissingMana'));
    measure('Veto_8', () => PlayerLoader.Veto(player, 'PreItemCheck'));
    measure('Factor_8', () => PlayerLoader.Factor(player, 'UseSpeedMultiplier', {}));
    output('callbacks=' + calls);
`, context);
