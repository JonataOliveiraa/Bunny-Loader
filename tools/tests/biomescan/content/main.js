// Etapa B0 do ModBiome (docs/local/PLANO-MODBIOME.md): medir, antes de escrever
// a API, como o celular varre os blocos em volta do jogador.
//   1. O tamanho de cada SceneMetrics._tileCounts do jogo. O ScanTiles faz
//      _tileCounts[tipo]++ SEM conferir o limite (disasm): um array que não
//      cresceu com os tiles de mod é escrita fora dele.
//   2. A ordem e a frequência: Player.UpdateSceneMetrics, Main.UpdateSceneMetrics,
//      SceneMetrics.AggregateTileCounts (o fim da varredura, onde o tModLoader
//      chama o TileCountsAvailable) e Player.UpdateBiomes, com a thread de cada um.
//   3. A contagem de um bloco de mod (ExampleTile): 0, 39, 40, 41 e 0 de novo
//      depois de tirar, e quantos quadros até a contagem mudar.
// Loga "biomescan <caso>: ok | FALHOU" e as medições em "biomescan medida ...".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const SceneMetrics = Terraria.SceneMetrics;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('biomescan ' + label + ': ok');
        else { fails++; bl.log('biomescan ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('biomescan ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
const measure = (label, value) => bl.log('biomescan medida ' + label + ': ' + value);

const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const tid = () => { try { return System.Threading.Thread.CurrentThread.ManagedThreadId; } catch (e) { return '?'; } };

let TILE = -1;

// Os SceneMetrics que o jogo tem, pelo nome de quem guarda.
function knownMetrics() {
    const out = [];
    const add = (name, get) => {
        try { out.push([name, get()]); } catch (e) { out.push([name, 'erro: ' + e]); }
    };
    add('LocalUserGameState._playerSceneMetrics', () => LocalUserGameState.Instance._playerSceneMetrics);
    add('LocalUserGameState._cameraSceneMetrics', () => LocalUserGameState.Instance._cameraSceneMetrics);
    add('Main.SceneMetrics', () => Main.SceneMetrics);
    add('Main.PlayerSceneMetrics', () => Main.PlayerSceneMetrics);
    add('WorldItem._sceneMetrics', () => Terraria.WorldItem._sceneMetrics);
    add('Main.PylonSystem._sceneMetrics', () => Main.PylonSystem && Main.PylonSystem._sceneMetrics);
    return out;
}

function nameOf(sm) {
    for (const [name, m] of knownMetrics()) if (m === sm) return name;
    return 'outro';
}

// ---- 2. ordem e frequência ----
let frame = 0;
let trace = null;                 // [evento] nos quadros gravados
const counts = new Map();         // evento -> vezes, nos quadros 60..119
const threads = new Map();        // evento -> threads vistas
function event(name) {
    const t = tid();
    if (!threads.has(name)) threads.set(name, new Set());
    threads.get(name).add(t);
    if (trace) trace.push(frame + ':' + name);
    if (frame >= 60 && frame < 120) counts.set(name, (counts.get(name) || 0) + 1);
}

// A contagem do bloco logo depois de cada varredura, por instância.
const lastCount = new Map();      // nome -> { frame, count, center }

Terraria.Player['void UpdateSceneMetrics()'].hook((original, self) => {
    event('Player.UpdateSceneMetrics(' + self.whoAmI + ')');
    original(self);
});
Terraria.Player['void UpdateBiomes()'].hook((original, self) => {
    original(self);
    event('Player.UpdateBiomes(' + self.whoAmI + ')');
});
Main['void UpdateSceneMetrics(Rectangle visualScanArea)'].hook((original, area) => {
    event('Main.UpdateSceneMetrics');
    original(area);
});
SceneMetrics['void AggregateTileCounts()'].hook((original, self) => {
    original(self);
    if (Main.gameMenu) return;
    const name = nameOf(self);
    event('AggregateTileCounts[' + name + ']');
    if (TILE >= 0) {
        lastCount.set(name, { frame, count: self['int GetTileCount(ushort tileId)'](TILE), center: self.TileCenter.X + ',' + self.TileCenter.Y });
    }
});

// ---- 1. tamanhos ----
function checkSizes() {
    const expected = Main.tileSolid.length;
    measure('tipos de tile (Main.tileSolid.length)', expected);
    const bad = [];
    for (const [name, m] of knownMetrics()) {
        if (m === null || m === undefined) { measure(name, 'nulo'); continue; }
        if (typeof m === 'string') { measure(name, m); continue; }
        const len = m._tileCounts.length;
        measure(name + '._tileCounts.length', len);
        if (len < expected) bad.push(name + '=' + len);
    }
    check('todo _tileCounts do jogo cabe os tiles de mod', () => bad.length === 0 || bad.join(' '));

    check('SceneMetrics criado agora nasce com o tamanho novo', () => {
        const fresh = SceneMetrics.new();
        fresh['void .ctor()']();
        return fresh._tileCounts.length >= expected || fresh._tileCounts.length;
    });
    const size = SceneMetrics.ZoneScanSize;
    measure('ZoneScanSize', size.X + 'x' + size.Y);
}

// ---- 3. contagem ----
// Células vazias acima do jogador, dentro da área da varredura.
let cells = [], placed = [];
function freeCells(n) {
    const p = Main.LocalPlayer;
    const px = Math.floor(p.Center.X / 16), py = Math.floor(p.Center.Y / 16);
    const out = [];
    for (let dy = 6; dy <= 16 && out.length < n; dy++) {
        for (let dx = -25; dx <= 25 && out.length < n; dx++) {
            if (bl.tiles.typeAt(px + dx, py - dy) < 0) out.push([px + dx, py - dy]);
        }
    }
    return out;
}

function setPlaced(n) {
    while (placed.length > n) { const [x, y] = placed.pop(); kill(x, y); }
    while (placed.length < n) {
        const c = cells[placed.length];
        if (!place(c[0], c[1], TILE)) throw new Error('PlaceTile falhou em ' + c);
        placed.push(c);
    }
}

const STEPS = [39, 40, 41, 0];
let baseline = 0, step = -1, stepFrame = 0, changedAt = -1, before = 0;

function playerCount() {
    return Main.PlayerSceneMetrics['int GetTileCount(ushort tileId)'](TILE);
}

function startCount() {
    TILE = ModContent.TileType('examplemod/ExampleTile');
    check('tipo do ExampleTile', () => bl.tiles.isModTile(TILE) || TILE);
    baseline = playerCount();
    measure('contagem antes de colocar', baseline);
    cells = freeCells(41);
    check('41 células livres acima do jogador', () => cells.length === 41 || cells.length);
}

// Um passo: põe/tira os blocos e espera a contagem mudar (até 60 quadros).
function tickCount() {
    if (step < 0 || step >= STEPS.length) return;
    const want = baseline + STEPS[step];
    const now = playerCount();
    if (changedAt < 0 && now !== before) changedAt = frame - stepFrame;
    if (now === want || frame - stepFrame >= 60) {
        const n = STEPS[step];
        const agg = lastCount.get('LocalUserGameState._playerSceneMetrics');
        check('contagem com ' + n + ' blocos', () => now === want || `${now}, esperado ${want}`);
        measure(n + ' blocos: quadros até mudar', changedAt);
        if (agg) measure(n + ' blocos: última varredura', `quadro ${agg.frame}, contagem ${agg.count}, centro ${agg.center}`);
        nextStep();
    }
}

function nextStep() {
    step++;
    if (step >= STEPS.length) return;
    before = playerCount();
    changedAt = -1;
    stepFrame = frame;
    setPlaced(STEPS[step]);
}

let done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frame++;
    if (frame === 1) check('tamanhos', checkSizes);
    if (frame === 30) trace = [];
    if (frame === 33) {
        measure('ordem nos quadros 30-32', trace.join(' | '));
        trace = null;
    }
    if (frame === 120) {
        measure('vezes em 60 quadros', [...counts].map(([k, v]) => k + '=' + v).join(', '));
        measure('threads', [...threads].map(([k, v]) => k + '=' + [...v].join('/')).join(', '));
        measure('thread do Player.Update', tid());
        check('preparo da contagem', startCount);
        check('primeiro passo', nextStep);
    }
    if (frame > 120 && step < STEPS.length) check('passo da contagem', tickCount);
    if (step >= STEPS.length) {
        done = true;
        setPlaced(0);
        bl.log('biomescan FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('biomescan: carregado');

export default class TestBiomescan extends Mod {}
