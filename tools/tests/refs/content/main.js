// ref/out na ponte: Ref solto numa chamada do mod, Ref preso dentro de um
// hook (int, Vector2), e o Ref guardado depois que o hook voltou.
const Main = Terraria.Main;
const NPC = Terraria.NPC;
const tryGetBuffTime = 'bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)';
const getLocation = 'bool GetNPCLocation(int i, bool seekHead, bool averageDirection, out int index, out Vector2 pos)';
const stepUp = 'void StepUp(ref Vector2 position, ref Vector2 velocity, int width, int height, ref float stepSpeed, ref float gfxOffY, int gravDir, bool holdsMatching, int specialChecksMode)';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('refs ' + label + ': ok');
        else { fails++; bl.log('refs ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('refs ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

let bump = 0, seenInHook = null, keptRef = null;
Main[tryGetBuffTime].hook((original, slot, time) => {
    const r = original(slot, time);
    seenInHook = time.value;
    time.value += bump;
    keptRef = time;
    return r;
});

let forcedPos = null;
NPC[getLocation].hook((original, i, seekHead, avg, index, pos) => {
    const r = original(i, seekHead, avg, index, pos);
    if (forcedPos) {
        index.value = 77;
        pos.value = forcedPos;
        return true;
    }
    return r;
});

let steps = 0, stepOk = 0, stepSample = '';
Terraria.Collision[stepUp].hook((original, position, velocity, width, height, stepSpeed, gfxOffY, gravDir, holds, mode) => {
    original(position, velocity, width, height, stepSpeed, gfxOffY, gravDir, holds, mode);
    const me = Main.player[Main.myPlayer];
    if (width === me.width && height === me.height) {
        steps++;
        const p = position.value;
        if (Math.abs(p.X - me.position.X) < 64 && Math.abs(p.Y - me.position.Y) < 64) stepOk++;
        else stepSample = `${p.X},${p.Y} x ${me.position.X},${me.position.Y}`;
        typeof stepSpeed.value === 'number' || (stepSample = 'stepSpeed ' + stepSpeed.value);
    }
});

// Pesca: 7 parametros (6 out bool) + self + MethodInfo = 9 inteiros, 1 na pilha.
const rollLevels = 'void FishingCheck_RollDropLevels(int fishingLevel, out bool common, out bool uncommon, out bool rare, out bool veryrare, out bool legendary, out bool crate)';
let forceCrate = false, rollSeen = 0;
Terraria.Projectile[rollLevels].hook((original, self, level, common, uncommon, rare, veryrare, legendary, crate) => {
    original(self, level, common, uncommon, rare, veryrare, legendary, crate);
    rollSeen++;
    if (forceCrate) crate.value = true;
});

// ref de struct grande (FishingAttempt, 92 bytes): copia, muda, devolve.
const probeQuest = 'void FishingCheck_ProbeForQuestFish(ref FishingAttempt fisher)';
let probeBefore = null;
Terraria.Projectile[probeQuest].hook((original, self, fisher) => {
    const a = fisher.value;
    probeBefore = a.fishingLevel;
    a.crate = true;
    fisher.value = a;
    original(self, fisher);
});

// Taxa de spawn: o jogo chama sozinho, todo quadro.
const spawnRate = 'void GetSpawnRate(Player player, out int spawnRate, out int maxSpawns)';
let spawnCalls = 0, spawnSample = '';
Terraria.NPC.Spawner[spawnRate].hook((original, self, player, rate, max) => {
    original(self, player, rate, max);
    spawnCalls++;
    const r0 = rate.value, m0 = max.value;
    max.value = m0 * 2;
    if (max.value !== m0 * 2 || !(r0 > 0)) spawnSample = `rate ${r0}, max ${m0} -> ${max.value}`;
});

function run() {
    const me = Main.player[Main.myPlayer];
    me['void AddBuff(int type, int time, bool fromNetPvP)'](2, 600, false);
    const slot = me['int FindBuffIndex(int type)'](2);

    check('out int numa chamada (Ref solto)', () => {
        const t = new Ref();
        const ok = Main[tryGetBuffTime](slot, t);
        return (ok === true && t.value > 500 && t.value <= 600 && seenInHook === t.value) ||
            `ok ${ok}, valor ${t.value}, hook viu ${seenInHook}`;
    });
    check('o hook altera o out de quem chamou', () => {
        bump = 1000;
        const t = new Ref(-1);
        Main[tryGetBuffTime](slot, t);
        bump = 0;
        return (t.value > 1500 && t.value <= 1600) || 'valor ' + t.value;
    });
    check('Ref guardado depois do hook fica com o ultimo valor', () =>
        (keptRef instanceof Ref && keptRef.value > 1500 && keptRef.value <= 1600) || 'valor ' + (keptRef && keptRef.value));
    check('escrever no Ref guardado nao toca o jogo', () => {
        const kept = keptRef;
        kept.value = 5;
        const t = new Ref();
        Main[tryGetBuffTime](slot, t);
        return (kept.value === 5 && kept !== keptRef && t.value > 500 && t.value <= 600) || `guardado ${kept.value}, novo ${t.value}`;
    });
    check('out Vector2 escrito pelo hook', () => {
        forcedPos = Vector2.new(123, 456);
        const idx = new Ref(0), pos = new Ref();
        const ok = NPC[getLocation](0, false, false, idx, pos);
        forcedPos = null;
        const p = pos.value;
        return (ok === true && idx.value === 77 && p.X === 123 && p.Y === 456) || `ok ${ok}, idx ${idx.value}, pos ${p && p.X},${p && p.Y}`;
    });
    check('argumento ref sem Ref da erro claro', () => {
        try { Main[tryGetBuffTime](slot, 5); return 'aceitou numero'; }
        catch (e) { return String(e).includes('Ref') || String(e); }
    });
    const bobber = Terraria.Projectile.new();
    bobber['void .ctor()']();
    check('seis out bool numa chamada com argumento na pilha (pesca)', () => {
        const outs = [new Ref(), new Ref(), new Ref(), new Ref(), new Ref(), new Ref()];
        forceCrate = true;
        bobber[rollLevels](10, ...outs);
        forceCrate = false;
        const vals = outs.map((r) => r.value);
        return (rollSeen > 0 && vals.every((v) => typeof v === 'boolean') && vals[5] === true) ||
            `hook ${rollSeen}, valores ${vals.join(',')}`;
    });
    check('ref de struct: o hook muda uma copia e devolve (FishingAttempt)', () => {
        const Attempt = Terraria.DataStructures.FishingAttempt;
        const start = Attempt.new();
        start.fishingLevel = 25;
        const fisher = new Ref(start);
        bobber[probeQuest](fisher);
        const a = fisher.value;
        return (probeBefore === 25 && a.crate === true && a.fishingLevel === 25) ||
            `hook viu ${probeBefore}, crate ${a.crate}, nivel ${a.fishingLevel}`;
    });
    check('casa de buff vazia: o out volta 0', () => {
        const t = new Ref(99);
        Main[tryGetBuffTime](me.buffType.length - 1, t);
        return t.value === 0 || 'valor ' + t.value;
    });
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 60) check('preparo', run);
    if (frames === 200) {
        done = true;
        check('out int num hook que o jogo chama sozinho (GetSpawnRate)', () =>
            (spawnCalls > 0 && !spawnSample) || `${spawnCalls} chamadas ${spawnSample}`);
        check('ref Vector2/float preso num hook do jogo (Collision.StepUp)', () =>
            (steps > 0 && stepOk === steps) || `${stepOk}/${steps} ${stepSample}`);
        bl.log('refs FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('refs: carregado');
