import * as config from './config.js';
import { installProfile, resetProfile, profileResult } from './profile.js';

const { variant } = config;
const asynchronous = typeof SoundEngine.GetSoundState === 'function';
const lifecycleStyle = config.lifecycle && asynchronous ? new SoundStyle('Sounds/Long', { Volume: 0.02 }) : null;

const Main = Terraria.Main;
const Engine = Terraria.Audio.SoundEngine;
const stylePlay = Engine['SoundEffectInstance PlaySound(LegacySoundStyle type, int x, int y, float pitchOffset, float volumeScale)'];
const vanillaStyle = Terraria.ID.SoundID.Item1;
const styles = Array.from({ length: 4 }, (_, i) => new SoundStyle('Sounds/Pulse' + i, {
    Volume: 0.02,
    MaxInstances: 0,
    PitchVariance: 0.1,
}));
const replacing = new SoundStyle('Sounds/Pulse0', { Volume: 0.02, MaxInstances: 1 });
const ignoring = new SoundStyle('Sounds/Pulse1', {
    Volume: 0.02,
    MaxInstances: 1,
    SoundLimitBehavior: SoundLimitBehavior.IgnoreNew,
});
const music = MusicLoader.GetMusicSlot('Music/Loop');
const phases = [
    { name: 'vanilla', vanilla: 48, marker: 0, direct: 0 },
    { name: 'mixed', vanilla: 48, marker: 12, direct: 0 },
    { name: 'custom-marker', vanilla: 0, marker: 12, direct: 0 },
    { name: 'custom-direct', vanilla: 0, marker: 0, direct: 12 },
];

if (config.extended) {
    phases.push({ name: 'custom-normal', vanilla: 0, marker: 0, direct: config.normalRate || 1 });
    phases.push({ name: 'custom-burst', vanilla: 0, marker: 0, direct: 48, burst: true });
    phases.push({ name: 'custom-ignore', vanilla: 0, marker: 0, direct: 12, ignore: true });
}

if (config.profile) {
    installProfile();
    if (!config.extended) phases.push({ name: 'custom-ignore', vanilla: 0, marker: 0, direct: 12, ignore: true });
}
if (config.onlyNormal) {
    const normal = phases.find(load => load.name === 'custom-normal');
    phases.splice(0, phases.length, normal);
}
const SETTLE = 60;
const SAMPLES = 180;
const owned = [];
let frames = 0;
let phase = -1;
let phaseFrames = 0;
let before = null;
let batch = [];
let updates = [];
let gaps = [];
let lastDraw = 0;
let done = false;
let checks = 0;
let failures = 0;
let forceUse = false;
let itemSoundObserved = false;
let savedItem = null;
let measureUpdate = false;
let queueBefore = null;
let lifecycleHandle = 0;
let lifecycleReady = 0;
let measurementStarted = 0;

function log(text) {
    bl.log('soundfilter ' + text);
}

function check(name, value) {
    checks++;
    if (!value) failures++;
    log((value ? 'PASS ' : 'FAIL ') + name);
}

function soundStats() {
    const rows = bl.hookStats().filter(h => h.name.includes('LegacySoundPlayer.PlaySound('));
    if (rows.length !== 1) throw Error('hooks de áudio: ' + rows.length);
    return rows[0];
}

function summary(values) {
    const sorted = values.slice().sort((a, b) => a - b);
    const at = percentile => sorted[Math.min(sorted.length - 1, Math.ceil(sorted.length * percentile) - 1)] || 0;
    return {
        n: sorted.length,
        mean: sorted.reduce((a, b) => a + b, 0) / Math.max(1, sorted.length),
        p50: at(0.5),
        p95: at(0.95),
        p99: at(0.99),
    };
}

class AudioProbe extends ModNPC {
    Texture = 'Box';
    HideFromBestiary = true;
    HideFromModMenu = true;
    Music = music;
    SceneEffectPriority = SceneEffectPriority.BossHigh;

    SetDefaults(npc) {
        npc.width = 16;
        npc.height = 16;
        npc.aiStyle = -1;
        npc.damage = 0;
        npc.lifeMax = 100;
        npc.friendly = true;
        npc.noGravity = true;
        npc.noTileCollide = true;
        npc.dontTakeDamage = true;
        npc.npcSlots = 0;
        npc.HitSound = styles[0];
    }

    PreAI() { return false; }
}

const npcType = ModNPC.register(AudioProbe);

function setup(player) {
    const source = Terraria.DataStructures.EntitySource_DebugCommand.new();
    source['void .ctor()']();
    const spawn = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

    for (let i = 0; i < 32; i++) {
        const slot = spawn(source, Math.floor(player.Center.X + (i % 8) * 20 - 80),
            Math.floor(player.Center.Y - 180 - Math.floor(i / 8) * 20), npcType, 0, 0, 0, 0, 0, Main.myPlayer);
        if (slot < 0 || slot >= 200) throw Error('sem slot para NPC ' + i);
        owned.push(Main.npc[slot]);
    }

    check('32 NPCs temporarios ativos', owned.every(npc => npc.active && npc.type === npcType));
    check('musica customizada registrada', music >= Terraria.ID.MusicID.Count);
    check('quatro marcadores distintos', new Set(styles.map(style => style.Style)).size === 4);
    const a = SoundEngine.PlaySound(replacing);
    const b = SoundEngine.PlaySound(replacing);
    check('ReplaceOldest conserva o novo pedido', a > 0 && b > 0 && a !== b &&
        (asynchronous ? SoundEngine.GetSoundState(a) === 4 && [1, 2].includes(SoundEngine.GetSoundState(b)) : SoundEngine.FindActiveSound(replacing) === b));
    const c = SoundEngine.PlaySound(ignoring);
    check('IgnoreNew conserva o limite', c > 0 && SoundEngine.PlaySound(ignoring) === 0);
    check('posicao distante continua muda', SoundEngine.PlaySound(styles[0], Vector2.new(player.Center.X + 30000, player.Center.Y)) === 0);
    SoundEngine.StopSound(b);
    SoundEngine.StopSound(c);
    if (asynchronous) {
        check('stop cancela o pedido imediatamente', SoundEngine.GetSoundState(b) === 4 && SoundEngine.FindActiveSound(replacing) === 0);
        check('som nao carregado e recusado', bl.sounds.enqueue(100000, 2147483647, 1, 1, 1, 1, false) === 0);
    }

    savedItem = { type: player.inventory[0].type, stack: player.inventory[0].stack, selected: player.selectedItemState.selected };
    player.inventory[0]['void SetDefaults(int Type, ItemVariant variant)'](Terraria.ID.ItemID.CopperShortsword, null);
    player.inventory[0].UseSound = replacing;
    player.selectedItemState.selected = 0;
}

function restoreItem(player) {
    if (!savedItem) return;
    player.inventory[0]['void SetDefaults(int Type, ItemVariant variant)'](savedItem.type, null);
    player.inventory[0].stack = savedItem.stack;
    player.selectedItemState.selected = savedItem.selected;
    savedItem = null;
}

function finishPhase() {
    const load = phases[phase];
    const after = soundStats();
    const calls = after.calls - before.calls;
    const js = after.js - before.js;
    const jsMs = after.jsMs - before.jsMs;
    const expectedMarker = load.marker * SAMPLES;
    const queueAfter = asynchronous ? bl.sounds.queueStats() : null;
    check(load.name + ' chamadas nativas alcancam o hook', calls >= (load.vanilla + load.marker) * SAMPLES);
    check(load.name + ' entradas JS esperadas', variant !== 'baseline' ? js === expectedMarker : js === calls);
    check(load.name + ' NPCs continuam ativos', owned.every(npc => npc.active));
    if (queueAfter) {
        check(load.name + ' fila e historico limitados', queueAfter.pending <= 32 && queueAfter.highWater <= 32 && queueAfter.retained <= 256);
        check(load.name + ' worker sem excecoes', queueAfter.backendErrors === 0);
        if (load.name === 'custom-normal' && load.direct === 1) {
            check('carga normal aceita todos os pedidos', queueAfter.accepted - queueBefore.accepted === SAMPLES && queueAfter.rejected === queueBefore.rejected);
            check('carga normal inicia sem falha ou expiracao', queueAfter.failed === queueBefore.failed && queueAfter.expired === queueBefore.expired);
        }
    }

    log('RESULT ' + JSON.stringify({
        variant, phase: load.name, samples: SAMPLES, calls, js, jsMs,
        requestedVanilla: load.vanilla * SAMPLES,
        requestedMarker: expectedMarker,
        requestedDirect: load.direct * (load.burst ? SAMPLES / 30 : SAMPLES),
        batchMs: summary(batch), updateMs: summary(updates), frameGapMs: summary(gaps),
        elapsedMs: performance.now() - measurementStarted,
        activeNpcs: owned.filter(npc => npc.active).length,
        musicPlaying: MusicLoader.IsMusicPlaying(music),
        backend: config.profile ? profileResult() : null,
        queueBefore,
        queueAfter,
    }));
}

Terraria.Player['void ItemCheckWrapped(int i)'].hook((original, player, index) => {
    if (forceUse && index === Main.myPlayer) player.controlUseItem = true;
    original(player, index);
});

Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (done || Main.gameMenu || index !== Main.myPlayer) return;
    player.immune = true;
    player.immuneTime = 60;
    player.statLife = player.statLifeMax2;
    frames++;

    try {
        if (forceUse && SoundEngine.FindActiveSound(replacing) > 0) itemSoundObserved = true;
        if (frames === 30) setup(player);
        if (frames === 60) forceUse = true;
        if (frames === 100) {
            forceUse = false;
            check('UseSound de item usado pelo jogo', itemSoundObserved);
            restoreItem(player);
            if (lifecycleStyle) lifecycleHandle = SoundEngine.PlaySound(lifecycleStyle);
        }
        if (lifecycleHandle && !lifecycleReady && SoundEngine.GetSoundState(lifecycleHandle) === 2) {
            lifecycleReady = performance.now();
            log('LIFECYCLE_READY handle=' + lifecycleHandle);
        }
        if (lifecycleHandle && lifecycleReady && performance.now() - lifecycleReady > 6000) {
            check('pausa Android preserva pedido alem da duracao original', [2, 3].includes(SoundEngine.GetSoundState(lifecycleHandle)));
            check('worker registrou pelo menos seis segundos de pausa', bl.sounds.queueStats().pausedMs >= 6000);
            SoundEngine.StopSound(lifecycleHandle);
            check('stop apos retomar cancela o mesmo pedido', SoundEngine.GetSoundState(lifecycleHandle) === 4);
            lifecycleHandle = 0;
        }
        if (frames === 240) {
            check('FindActiveSound expira', SoundEngine.FindActiveSound(ignoring) === 0);
            check('musica customizada toca junto', MusicLoader.IsMusicPlaying(music));
            phase = 0;
        }
        if (phase < 0 || phaseFrames >= SETTLE + SAMPLES) return;

        phaseFrames++;
        if (phaseFrames === SETTLE + 1) {
            before = soundStats();
            measurementStarted = performance.now();
            batch = [];
            updates = [];
            gaps = [];
            if (config.profile) resetProfile();
            queueBefore = asynchronous ? bl.sounds.queueStats() : null;
        }

        const load = phases[phase];
        const started = performance.now();
        for (let i = 0; i < load.vanilla; i++) stylePlay(vanillaStyle, -1, -1, 0, 0.02);
        for (let i = 0; i < load.marker; i++) stylePlay(styles[i % styles.length], -1, -1, 0, 1);
        const direct = load.burst && phaseFrames % 30 !== 0 ? 0 : load.direct;
        for (let i = 0; i < direct; i++) SoundEngine.PlaySound(load.ignore ? ignoring : styles[i % styles.length]);
        if (phaseFrames > SETTLE) {
            batch.push(performance.now() - started);
            measureUpdate = true;
        }
    }
    catch (error) {
        failures++;
        done = true;
        forceUse = false;
        restoreItem(player);
        for (const npc of owned) npc.active = false;
        log('FAIL execucao: ' + error + ' ' + error.stack);
        log('FIM variant=' + variant + ' checks=' + checks + ' falhas=' + failures);
    }
});

Main['void DoUpdate(GameTime gameTime)'].hook((original, main, time) => {
    const started = performance.now();
    measureUpdate = false;
    original(main, time);
    if (!done && measureUpdate) updates.push(performance.now() - started);
});

Main['void DoDraw(GameTime gameTime)'].hook((original, main, time) => {
    const started = performance.now();
    original(main, time);
    if (done || phase < 0 || Main.gameMenu) return;
    if (phaseFrames > SETTLE && lastDraw) gaps.push(started - lastDraw);
    lastDraw = started;
    if (phaseFrames < SETTLE + SAMPLES) return;

    finishPhase();
    phase++;
    phaseFrames = 0;
    if (phase < phases.length) return;

    done = true;
    for (const npc of owned) npc.active = false;
    check('NPCs temporarios removidos', owned.every(npc => !npc.active));
    log('FIM variant=' + variant + ' checks=' + checks + ' falhas=' + failures);
});

log('carregado variant=' + variant);
export default class TestSoundFilter extends Mod {}
