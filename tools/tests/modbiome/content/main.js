// Etapa B1 do ModBiome (docs/local/PLANO-MODBIOME.md), num jogador só:
//   - registro (Type de bioma e de efeito de cena, ModContent.GetInstance);
//   - flags por jogador e player.InModBiome (classe, instância, Type);
//   - OnEnter e OnLeave uma vez por troca, OnInBiome a cada quadro dentro,
//     inclusive no da entrada;
//   - dois biomas ao mesmo tempo e a música da cena pela prioridade, com um
//     ModSceneEffect que não é bioma e o SpecialVisuals também quando inativo;
//   - IsBiomeActive que lança vale false e chama o OnLeave;
//   - a mesma classe (SameNameBiome) em dois mods, cada uma com a sua flag;
//   - bioma por contagem de blocos (TileCountsAvailable): 40 ExampleTile
//     põem, tirar desliga; ResetNearbyTileEffects antes de cada contagem.
// Loga "modbiome <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modbiome ' + label + ': ok');
        else { fails++; bl.log('modbiome ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modbiome ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

// O que cada bioma viu.
const seen = (cls) => cls.seen || (cls.seen = { enter: 0, inside: 0, leave: 0 });
class Counted extends ModBiome {
    static Autoload = false;
    OnEnter(player) { seen(this.constructor).enter++; }
    OnInBiome(player) { seen(this.constructor).inside++; }
    OnLeave(player) { seen(this.constructor).leave++; }
}

export class BiomeA extends Counted {
    static on = false;
    SetStaticDefaults() {
        this.Music = 5;
        this.Priority = SceneEffectPriority.BiomeHigh;
    }
    IsBiomeActive(player) { return BiomeA.on; }
}

// No tModLoader o bioma é getter: o padrão no protótipo tem de deixar.
export class BiomeB extends Counted {
    static on = false;
    get Music() { return 6; }
    IsBiomeActive(player) { return BiomeB.on; }
}

export class ThrowBiome extends Counted {
    static on = false;
    static boom = false;
    SetStaticDefaults() { this.Music = -1; }
    IsBiomeActive(player) {
        if (ThrowBiome.boom) throw new Error('de propósito');
        return ThrowBiome.on;
    }
}

export class SameNameBiome extends ModBiome {
    static on = false;
    SetStaticDefaults() { this.Music = -1; }
    IsBiomeActive(player) { return SameNameBiome.on; }
}

export class TestTileCount extends ModSystem {
    count = -1;
    resets = 0;
    counts = 0;
    outOfOrder = 0;
    #pending = false;
    tile = -1;

    ResetNearbyTileEffects() {
        this.resets++;
        this.#pending = true;
    }

    TileCountsAvailable(tileCounts) {
        if (!this.#pending) this.outOfOrder++;
        this.#pending = false;
        this.counts++;
        if (this.tile > 0) this.count = tileCounts[this.tile];
    }
}

export class BlockBiome extends Counted {
    SetStaticDefaults() { this.Music = -1; }
    IsBiomeActive(player) { return ModContent.GetInstance(TestTileCount).count >= 40; }
}

export class SceneOnly extends ModSceneEffect {
    static on = false;
    static visuals = { on: 0, off: 0 };
    SetStaticDefaults() {
        this.Music = 7;
        this.Priority = SceneEffectPriority.Event;
    }
    IsSceneEffectActive(player) { return SceneOnly.on; }
    SpecialVisuals(player, isActive) { SceneOnly.visuals[isActive ? 'on' : 'off']++; }
}

let other = null;   // o SameNameBiome do test-modbiome-b
let countsAt10 = 0;
// As varreduras do jogador que o próprio jogo fez (o ritmo muda entre versões:
// 12 por 60 quadros na 1.4.5.6, 6 na 1.4.5.8).
let gameScans = 0, gameScansAt10 = 0;
Terraria.SceneMetrics['void AggregateTileCounts()'].hook((original, self) => {
    original(self);
    if (!Main.gameMenu && self === Main.PlayerSceneMetrics) gameScans++;
});
const me = () => Main.LocalPlayer;
const scene = () => me().CurrentSceneEffect;
const flagsOf = (...list) => list.map((c) => me().InModBiome(c) ? 1 : 0).join('');

function registration() {
    const a = ModContent.GetInstance(BiomeA);
    check('ModContent.GetInstance devolve o modelo', () => (a instanceof ModBiome && a.Mod === bl.mod) || String(a));
    check('Type dos biomas em sequência, sem repetir', () => {
        const types = [BiomeA, BiomeB, ThrowBiome, SameNameBiome, BlockBiome].map((c) => ModContent.GetInstance(c).Type);
        return new Set(types).size === types.length || types.join(',');
    });
    check('padrões: Priority BiomeLow, Music 0; o getter do mod ganha do protótipo', () => {
        const b = ModContent.GetInstance(BiomeB);
        return (b.Priority === SceneEffectPriority.BiomeLow && b.Music === 6 && ModBiome.prototype.Music === 0) ||
            `${b.Priority} ${b.Music}`;
    });
    check('o bioma do outro mod (mesmo nome de classe) é outro', () => {
        const r = new Ref();
        if (!ModLoader.TryGetMod('test-modbiome-b', r)) return 'test-modbiome-b não carregou';
        other = r.value.Call('biome');
        const mine = ModContent.GetInstance(SameNameBiome), theirs = ModContent.GetInstance(other);
        return (theirs && theirs !== mine && theirs.Type !== mine.Type && theirs.Mod !== mine.Mod) || String(theirs);
    });
    check('tudo desligado no começo', () => flagsOf(BiomeA, BiomeB, ThrowBiome, SameNameBiome, other) === '00000' || flagsOf(BiomeA, BiomeB, ThrowBiome, SameNameBiome, other));
    check('InModBiome de algo que não é bioma lança', () => {
        try { me().InModBiome(TestTileCount); } catch (e) { return true; }
        return 'não lançou';
    });
}

// Passos por quadro: [quadro, ação].
const STEPS = [
    [1, registration],
    [10, () => { countsAt10 = ModContent.GetInstance(TestTileCount).counts; gameScansAt10 = gameScans; }],
    [10, () => { BiomeA.on = true; }],
    [11, () => check('A: entrou (OnEnter 1, OnInBiome 1, InModBiome por classe, instância e Type)', () => {
        const s = seen(BiomeA), inst = ModContent.GetInstance(BiomeA);
        return (s.enter === 1 && s.inside === 1 && me().InModBiome(BiomeA) && me().InModBiome(inst) && me().InModBiome(inst.Type)) || JSON.stringify(s);
    })],
    [20, () => {
        check('A: 10 quadros dentro, sem outro OnEnter', () => { const s = seen(BiomeA); return (s.enter === 1 && s.inside === 10 && s.leave === 0) || JSON.stringify(s); });
        BiomeB.on = true;
    }],
    [21, () => {
        check('A e B ao mesmo tempo', () => flagsOf(BiomeA, BiomeB) === '11' || flagsOf(BiomeA, BiomeB));
        check('a música da cena é a do mais prioritário (A, BiomeHigh)', () => {
            const m = scene().music;
            return (m.value === 5 && m.priority === SceneEffectPriority.BiomeHigh) || JSON.stringify({ value: m.value, priority: m.priority });
        });
    }],
    [24, () => { SceneOnly.on = true; }],
    [25, () => check('ModSceneEffect de Event ganha dos biomas; SpecialVisuals também inativo', () => {
        const m = scene().music, v = SceneOnly.visuals;
        return (m.value === 7 && m.priority === SceneEffectPriority.Event && v.on === 1 && v.off >= 20) || JSON.stringify({ value: m.value, v });
    })],
    [30, () => { BiomeA.on = false; BiomeB.on = false; SceneOnly.on = false; }],
    [31, () => {
        check('A e B saíram (OnLeave 1 cada)', () => {
            const a = seen(BiomeA), b = seen(BiomeB);
            return (a.leave === 1 && b.leave === 1 && flagsOf(BiomeA, BiomeB) === '00') || JSON.stringify({ a, b });
        });
        check('a cena não tem mais A, B nem SceneOnly', () => {
            const s = scene();
            return !s.active.some((e) => e === ModContent.GetInstance(BiomeA) || e === ModContent.GetInstance(BiomeB) || e === ModContent.GetInstance(SceneOnly)) || s.active.map((e) => e.constructor.name).join(',');
        });
    }],
    [35, () => { ThrowBiome.on = true; }],
    [36, () => check('ThrowBiome dentro', () => (seen(ThrowBiome).enter === 1 && me().InModBiome(ThrowBiome)) || JSON.stringify(seen(ThrowBiome)))],
    [40, () => { ThrowBiome.boom = true; }],
    [41, () => check('IsBiomeActive que lança: fora, com OnLeave', () =>
        (!me().InModBiome(ThrowBiome) && seen(ThrowBiome).leave === 1) || JSON.stringify(seen(ThrowBiome)))],
    [45, () => { SameNameBiome.on = true; }],
    [46, () => check('mesmo nome: o deste mod ligado, o do outro não', () => flagsOf(SameNameBiome, other) === '10' || flagsOf(SameNameBiome, other))],
    [50, () => { SameNameBiome.on = false; ModLoader.GetMod('test-modbiome-b').Call('set', true); }],
    [51, () => check('mesmo nome: agora só o do outro', () => flagsOf(SameNameBiome, other) === '01' || flagsOf(SameNameBiome, other))],
    [55, () => { ModLoader.GetMod('test-modbiome-b').Call('set', false); }],
];

// ---- blocos ----
const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
let placed = [], blockPhase = 0, blockFrame = 0, baseline = 0;

function startBlocks(frame) {
    const sys = ModContent.GetInstance(TestTileCount);
    sys.tile = ModContent.TileType('examplemod/ExampleTile');
    check('contagem: ResetNearbyTileEffects antes de cada TileCountsAvailable', () =>
        (sys.resets > 0 && sys.counts > 0 && sys.outOfOrder === 0) || JSON.stringify({ resets: sys.resets, counts: sys.counts, outOfOrder: sys.outOfOrder }));
    const mine = sys.counts - countsAt10, game = gameScans - gameScansAt10;
    check('contagem: uma por varredura do jogador, e só dela', () =>
        (game > 0 && mine === game) || `${mine} chamada(s) para ${game} varredura(s) do jogador`);
    blockPhase = 1;
    blockFrame = frame;
}

function tickBlocks(frame) {
    const sys = ModContent.GetInstance(TestTileCount);
    if (blockPhase === 1) {
        if (sys.count < 0) return;            // ainda sem contagem com o tipo certo
        baseline = sys.count;
        if (baseline >= 40) {
            check('contagem: menos de 40 ExampleTile por perto antes', () => 'já havia ' + baseline);
            blockPhase = 9;
            return;
        }
        const p = me();
        const px = Math.floor(p.Center.X / 16), py = Math.floor(p.Center.Y / 16);
        for (let dy = 6; dy <= 16 && placed.length < 40; dy++) {
            for (let dx = -25; dx <= 25 && placed.length < 40; dx++) {
                if (bl.tiles.typeAt(px + dx, py - dy) < 0 && place(px + dx, py - dy, sys.tile)) placed.push([px + dx, py - dy]);
            }
        }
        check('contagem: 40 ExampleTile postos', () => placed.length === 40 || placed.length);
        blockPhase = 2;
        blockFrame = frame;
    } else if (blockPhase === 2) {
        if (me().InModBiome(BlockBiome)) {
            check('bioma por blocos: entrou com ' + sys.count + ' (' + (frame - blockFrame) + ' quadros)', () => sys.count >= 40 && seen(BlockBiome).enter === 1 || JSON.stringify(seen(BlockBiome)));
            for (const [x, y] of placed) kill(x, y);
            placed = [];
            blockPhase = 3;
            blockFrame = frame;
        } else if (frame - blockFrame > 30) {
            check('bioma por blocos: entrou', () => 'contagem ' + sys.count + ' depois de 30 quadros');
            blockPhase = 9;
        }
    } else if (blockPhase === 3) {
        if (!me().InModBiome(BlockBiome)) {
            check('bioma por blocos: saiu ao tirar (' + (frame - blockFrame) + ' quadros)', () => (seen(BlockBiome).leave === 1 && sys.count === baseline) || `${sys.count} ${JSON.stringify(seen(BlockBiome))}`);
            blockPhase = 9;
        } else if (frame - blockFrame > 30) {
            check('bioma por blocos: saiu ao tirar', () => 'contagem ' + sys.count + ' depois de 30 quadros');
            blockPhase = 9;
        }
    }
}

let frame = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frame++;
    for (const [at, fn] of STEPS) if (at === frame) check('passo ' + at, fn);
    if (frame === 70) check('preparo dos blocos', () => startBlocks(frame));
    if (frame > 70 && blockPhase > 0 && blockPhase < 9) check('blocos', () => tickBlocks(frame));
    if (blockPhase === 9) {
        done = true;
        for (const [x, y] of placed) kill(x, y);
        bl.log('modbiome FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modbiome: carregado');

export default class TestModBiome extends Mod {}
