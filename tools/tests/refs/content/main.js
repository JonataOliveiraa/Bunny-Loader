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
        check('ref Vector2/float preso num hook do jogo (Collision.StepUp)', () =>
            (steps > 0 && stepOk === steps) || `${stepOk}/${steps} ${stepSample}`);
        bl.log('refs FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('refs: carregado');
