const Main = Terraria.Main;
const log = text => bl.log('modplayerperf ' + text);
const ticks = ['ResetEffects', 'PreUpdate', 'PostUpdate', 'PreUpdateBuffs', 'PostUpdateBuffs', 'UpdateEquips',
    'PostUpdateEquips', 'UpdateBadLifeRegen', 'UpdateLifeRegen', 'PreUpdateMovement', 'PostUpdateMiscEffects',
    'PostUpdateRunSpeeds', 'NaturalLifeRegen', 'ResetInfoAccessories', 'PostItemCheck'];
let calls = 0, frames = 0, before, done = false;
let gatedCalls = 0;
const wind = Terraria.Player['bool CanBePushedByWind()'];
const nested = Terraria.NPC['int GetStackForSlimeItemDrop(int item)'];
wind.hook((original, player) => original(player), { on: -1, field: 'whoAmI', minType: 255 });
wind.hook((original, player) => { nested(1); return original(player); });
nested.hook((original, item) => { gatedCalls++; return original(item); }, { whileIn: wind });

for (let i = 0; i < 8; i++) {
    const Probe = class extends ModPlayer {};
    Object.defineProperty(Probe, 'name', { value: 'TickProbe' + i });
    for (const name of ticks) Probe.prototype[name] = function () { calls++; };
    Probe.prototype.CanStartExtraJump = function () { return true; };
    Probe.prototype.PreItemCheck = function () { return false; };
    Probe.prototype.OnExtraJumpStarted = function () {};
    Probe.prototype.HideDrawLayers = function () {};
    Probe.prototype.ModifyDrawInfo = function () {};
    Probe.prototype.ModifyHitNPCWithItem = function () {};
    Probe.prototype.PlayerDisconnect = function () {};
    ModPlayer.register(Probe);
}

function measure(name, fn) {
    const samples = [];
    for (let repeat = 0; repeat < 7; repeat++) {
        const start = performance.now();
        for (let i = 0; i < 20000; i++) fn();
        samples.push((performance.now() - start) * 1000 / 20000);
    }
    samples.sort((a, b) => a - b);
    log(name + ' mediana_us=' + samples[3].toFixed(3) + ' minimo_us=' + samples[0].toFixed(3));
}

function snapshot() {
    return bl.hookStats().map(row => ({ name: row.name, calls: row.calls, js: row.js, ms: row.jsMs }));
}

function report(after) {
    for (let i = 0; i < after.length; i++) {
        const a = after[i], b = before[i];
        if (!b || !/Player\.|PlayerDraw|Rectangle.Intersects|SoundEngine.PlaySound/.test(a.name)) continue;
        const native = a.calls - b.calls, js = a.js - b.js;
        if (!native) continue;
        log('hook ' + JSON.stringify({ name: a.name, calls: native, js, usPerTick: (a.ms - b.ms) * 1000 / 300 }));
    }
    log('FIM frames=300 callbacks=' + calls);
}

Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (done || index !== Main.myPlayer || Main.gameMenu) return;
    frames++;
    if (frames === 119) {
        const before = gatedCalls;
        wind(player);
        const inside = gatedCalls;
        nested(1);
        log('whileIn_multiplos_hooks=' + (inside === before + 1 && gatedCalls === inside ? 'ok' : 'FALHOU'));
    } else if (frames === 120) {
        const cap = player['void CapAttackSpeeds()'], itemCheck = player['void ItemCheck()'];
        measure('CapAttackSpeeds_8', () => cap(player));
        measure('ItemCheck_veto_8', () => itemCheck(player));
    } else if (frames === 121) { calls = 0; before = snapshot(); }
    else if (frames === 421) { done = true; report(snapshot()); }
});

export default class ModPlayerPerformance extends Mod {}
