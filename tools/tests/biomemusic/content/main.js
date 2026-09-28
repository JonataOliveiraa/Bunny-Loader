// Etapa B3 do ModBiome (docs/local/PLANO-MODBIOME.md): a música da cena
// (ModSceneEffect/ModBiome) contra a escolha do jogo, como no tModLoader: ganha
// se a Priority dela chegar ao degrau da música do jogo.
//   - de dia, na superfície (degrau BiomeLow): BiomeLow ganha; Priority None e
//     Music -1 deixam a do jogo; Music 0 é silêncio;
//   - chuva de slime (degrau Environment): BiomeHigh perde, Environment ganha;
//   - um chefe do jogo perto (degrau BossLow): Event perde, BossLow empata e ganha;
//   - caixa de música do jogo ligada: ela ganha de tudo;
//   - Otherworld (Main.swapMusic): a escolha dele também, e lá a chuva de
//     slime não tem ramo (BiomeLow ganha);
//   - uma faixa de mod (a Ropocalypse2 do Example Mod) toca e cala o jogo, e
//     ao desligar o jogo volta.
// A música é conferida logo depois da escolha do jogo (o hook deste teste fica
// por fora do ModMusic). Loga "biomemusic <caso>: ok | FALHOU".
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('biomemusic ' + label + ': ok');
        else { fails++; bl.log('biomemusic ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('biomemusic ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

export class TestScene extends ModSceneEffect {
    static on = false;
    static music = -1;
    static priority = SceneEffectPriority.BiomeLow;
    get Music() { return TestScene.music; }
    get Priority() { return TestScene.priority; }
    IsSceneEffectActive(player) { return TestScene.on; }
}

// A escolha do quadro, depois do UpdateAudio inteiro (os hooks de escolha e a
// caixa de música do jogo): { tow, newMusic }.
let last = null, towNow = false, towCalls = 0, forceBox = false, boxError = null;
function setBox(value) {
    const sm = Main.SceneMetrics;
    try { sm.ActiveMusicBox = value; return; } catch (e) { boxError = String(e); }
    try { sm['<ActiveMusicBox>k__BackingField'] = value; boxError = null; } catch (e) { boxError += ' | ' + e; }
}
const watch = (tow) => (original, self) => {
    if (forceBox) setBox(3);
    original(self);
    towNow = tow;
    if (tow) towCalls++;
};
Main['void UpdateAudio_DecideOnNewMusic()'].hook(watch(false));
Main['void UpdateAudio_DecideOnTOWMusic()'].hook(watch(true));
Main['void UpdateAudio()'].hook((original, self) => {
    original(self);
    if (!Main.gameMenu) last = { tow: towNow, newMusic: Main.newMusic };
});

const P = SceneEffectPriority;
let vanilla = -1, marker = -1, boss = -1, trackSlot = 0, saved = null, waitFrom = 0;
const state = () => JSON.stringify({ last, cur: Main.curMusic, scene: Main.LocalPlayer.CurrentSceneEffect && Main.LocalPlayer.CurrentSceneEffect.music.value });
const set = (on, music, priority) => { TestScene.on = on; TestScene.music = music; TestScene.priority = priority; };
const expect = (label, want) => check(label, () => (last && last.newMusic === want) || 'esperado ' + want + ', ' + state());
// A do jogo: de dia ele alterna entre duas faixas depois de um silêncio.
const expectGame = (label) => check(label, () => (last && last.newMusic > 0 && last.newMusic !== marker) || 'esperada a do jogo, ' + state());
const expectNot = (label, not) => check(label, () => (last && last.newMusic !== not) || 'não devia ser ' + not + ', ' + state());

// A Geleia Rainha (degrau BossLow): um pirata sem invasão some no quadro seguinte.
function spawnBoss() {
    const p = Main.LocalPlayer;
    const src = Terraria.DataStructures.EntitySource_DebugCommand.new();
    const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
    boss = newNpc(src, Math.floor(p.Center.X) + 300, Math.floor(p.Center.Y) - 300, 50, 0, 0, 0, 0, 0, Main.myPlayer);
}

function removeBoss() {
    if (boss < 0) return;
    Main.npc[boss].active = false;
    boss = -1;
}

const STEPS = [
    [1, () => {
        saved = { day: Main.dayTime, time: Main.time, rain: Main.raining, swap: Main.swapMusic };
        Main.dayTime = true;
        Main.time = 27000;
        Main.swapMusic = false;
    }],
    [10, () => {
        vanilla = last ? last.newMusic : -1;
        marker = vanilla === 29 ? 23 : 29;   // cogumelo, ou masmorra
        check('a do jogo sem cena (dia, superfície): ' + vanilla, () => (vanilla > 0 && !last.tow) || state());
        set(true, marker, P.BiomeLow);
    }],
    [13, () => { expect('BiomeLow ganha da superfície', marker); set(true, 0, P.BiomeLow); }],
    [16, () => { expect('Music 0: silêncio', 0); set(true, -1, P.BiomeLow); }],
    [19, () => { expectGame('Music -1: fica a do jogo'); set(true, marker, P.None); }],
    [22, () => { expectGame('Priority None: fica a do jogo'); set(true, marker, P.BiomeHigh); Main.slimeRain = true; }],
    [25, () => { expectNot('chuva de slime (Environment) ganha de BiomeHigh', marker); TestScene.priority = P.Environment; }],
    [28, () => { expect('Environment ganha da chuva de slime', marker); Main.slimeRain = false; TestScene.on = false; spawnBoss(); }],
    [31, () => {
        const npc = boss >= 0 ? Main.npc[boss] : null;
        check('o jogo sozinho troca pela do chefe (Geleia Rainha)', () =>
            (npc && npc.active && last.newMusic > 0 && last.newMusic !== 1 && last.newMusic !== 18) || state() + ', ativa ' + (npc && npc.active));
        set(true, marker, P.Event);
    }],
    [34, () => { expectNot('chefe do jogo (BossLow) ganha de Event', marker); TestScene.priority = P.BossLow; }],
    [37, () => { expect('BossLow empata com o chefe do jogo e ganha', marker); removeBoss(); forceBox = true; }],
    [40, () => {
        check('caixa de música do jogo ganha da cena', () =>
            boxError ? 'não deu para ligar a caixa: ' + boxError : (last.newMusic !== marker || state()));
        forceBox = false;
        setBox(-1);
        set(true, marker, P.BiomeLow);
        Main.swapMusic = true;
    }],
    [46, () => {
        check('Otherworld: a escolha dele e BiomeLow ganha', () => (last.tow && towCalls > 0 && last.newMusic === marker) || state());
        Main.slimeRain = true;
    }],
    [49, () => {
        expect('Otherworld: chuva de slime não tem ramo, BiomeLow ganha', marker);
        Main.slimeRain = false;
        Main.swapMusic = false;
        const r = new Ref();
        if (ModLoader.TryGetMod('examplemod', r)) trackSlot = MusicLoader.GetMusicSlot(r.value, 'Music/Ropocalypse2');
        check('faixa do Example Mod', () => trackSlot > 0 || trackSlot);
        set(true, trackSlot, P.BiomeLow);
        waitFrom = 49;
    }],
];

let frame = 0, phase = 0, done = false;
function finish() {
    set(false, -1, P.BiomeLow);
    removeBoss();
    Main.slimeRain = false;
    forceBox = false;
    if (saved) {
        Main.dayTime = saved.day;
        Main.time = saved.time;
        Main.swapMusic = saved.swap;
    }
    done = true;
    bl.log('biomemusic FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frame++;
    for (const [at, fn] of STEPS) if (at === frame) check('passo ' + at, fn);

    // A faixa: o jogo cala (newMusic 0) quando ela já se ouve; ao desligar, volta.
    if (frame > 49 && phase === 0 && trackSlot > 0) {
        if (last.newMusic === 0 && MusicLoader.IsMusicPlaying(trackSlot)) {
            check('faixa de mod tocando e o jogo calado (' + (frame - waitFrom) + ' quadros)', () => true);
            set(false, -1, P.BiomeLow);
            phase = 1;
            waitFrom = frame;
        } else if (frame - waitFrom > 240) {
            check('faixa de mod tocando', () => state() + ', tocando ' + MusicLoader.IsMusicPlaying(trackSlot));
            phase = 2;
        }
    } else if (phase === 1) {
        if (last.newMusic > 0 && !MusicLoader.IsMusicPlaying(trackSlot)) {
            check('cena desligada: a do jogo volta e a faixa para (' + (frame - waitFrom) + ' quadros)', () => true);
            phase = 2;
        } else if (frame - waitFrom > 400) {
            check('cena desligada: a do jogo volta', () => state() + ', tocando ' + MusicLoader.IsMusicPlaying(trackSlot));
            phase = 2;
        }
    }
    if (phase === 2 || (frame > 49 && trackSlot <= 0)) finish();
});
bl.log('biomemusic: carregado');

export default class TestBiomemusic extends Mod {}
