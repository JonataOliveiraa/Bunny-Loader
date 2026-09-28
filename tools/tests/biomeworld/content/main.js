// Etapa B2 do ModBiome (docs/local/PLANO-MODBIOME.md), num jogador só:
//   - teleporte: 40 ExampleTile longe daqui; o Teleport varre e chama o
//     UpdateBiomes na hora (UpdateBiomesIfMovedEnoughForBlackFade), então o
//     bioma por blocos liga no próprio quadro e desliga na volta;
//   - saída do mundo (WorldGen.SaveAndQuit, a do menu de pausa): OnLeave de
//     quem estava dentro, flags zeradas, cena vazia e ResetNearbyTileEffects.
// SAI DO MUNDO no fim: rodar sozinho, não junto com outros testes.
// Loga "biomeworld <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('biomeworld ' + label + ': ok');
        else { fails++; bl.log('biomeworld ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('biomeworld ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const seen = (cls) => cls.seen || (cls.seen = { enter: 0, inside: 0, leave: 0 });
class Counted extends ModBiome {
    static Autoload = false;
    SetStaticDefaults() { this.Music = -1; }
    OnEnter(player) { seen(this.constructor).enter++; }
    OnInBiome(player) { seen(this.constructor).inside++; }
    OnLeave(player) { seen(this.constructor).leave++; }
}

export class TestTileCount extends ModSystem {
    count = -1;
    resets = 0;
    tile = -1;
    ResetNearbyTileEffects() { this.resets++; }
    TileCountsAvailable(tileCounts) {
        if (this.tile > 0) this.count = tileCounts[this.tile];
    }
}

export class BlockBiome extends Counted {
    IsBiomeActive(player) { return ModContent.GetInstance(TestTileCount).count >= 40; }
}

export class ManualBiome extends Counted {
    static on = false;
    IsBiomeActive(player) { return ManualBiome.on; }
}

const me = () => Main.LocalPlayer;
const sys = () => ModContent.GetInstance(TestTileCount);
const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const teleport = (x, y) => me()['void Teleport(Vector2 newPos, int Style, int extraInfo)'](Vector2.new(x, y), 0, 0);

let placed = [], home = null;
function cleanup() {
    for (const [x, y] of placed) kill(x, y);
    placed = [];
}

// Os blocos em volta de um ponto a 250 tiles daqui, e o teleporte até lá e de volta.
function teleportCase() {
    const s = sys();
    if (s.count >= 40) return 'já havia ' + s.count + ' ExampleTile por perto';

    const p = me();
    home = { x: p.position.X, y: p.position.Y };
    const px = Math.floor(p.Center.X / 16), py = Math.floor(p.Center.Y / 16);
    const tx = px + 250 < Main.maxTilesX - 100 ? px + 250 : px - 250;
    for (let dy = 6; dy <= 55 && placed.length < 40; dy++) {
        for (let dx = -30; dx <= 30 && placed.length < 40; dx++) {
            if (bl.tiles.typeAt(tx + dx, py - dy) < 0 && place(tx + dx, py - dy, s.tile)) placed.push([tx + dx, py - dy]);
        }
    }
    if (placed.length !== 40) return 'só ' + placed.length + ' blocos postos';

    const before = s.count;
    teleport(tx * 16 - p.width / 2, home.y);
    const there = { count: s.count, in: me().InModBiome(BlockBiome), seen: { ...seen(BlockBiome) } };
    teleport(home.x, home.y);
    const back = { count: s.count, in: me().InModBiome(BlockBiome), seen: { ...seen(BlockBiome) } };
    cleanup();

    check('teleporte: contagem e bioma na chegada, no mesmo quadro', () =>
        (there.in && there.count >= 40 && there.seen.enter === 1) || JSON.stringify({ before, there }));
    check('teleporte: fora na volta, com OnLeave, no mesmo quadro', () =>
        (!back.in && back.count === before && back.seen.leave === 1) || JSON.stringify({ before, back }));
}

let frame = 0, done = false, leaving = null;
function exitCase() {
    check('antes de sair: ManualBiome dentro', () => (me().InModBiome(ManualBiome) && seen(ManualBiome).enter === 1) || JSON.stringify(seen(ManualBiome)));
    leaving = { resets: sys().resets, leave: seen(ManualBiome).leave };
    bl.log('biomeworld: saindo do mundo (SaveAndQuit)');
    W['void SaveAndQuit()']();
}

// Por fora do hook do BiomeLoader ou por dentro: nos dois casos o LeaveWorld
// já rodou quando o original volta.
W['void SaveAndQuit()'].hook((original) => {
    original();
    if (!leaving || done) return;
    done = true;
    ManualBiome.on = false;
    const p = me();
    check('saída: OnLeave de quem estava dentro, uma vez', () => seen(ManualBiome).leave === leaving.leave + 1 || JSON.stringify(seen(ManualBiome)));
    check('saída: flags zeradas', () => (!p.InModBiome(ManualBiome) && !p.InModBiome(BlockBiome)) || Array.from(p.ModBiomeFlags).join(''));
    check('saída: cena vazia', () => !(p.CurrentSceneEffect && p.CurrentSceneEffect.anyActive) || 'ainda ativa');
    check('saída: ResetNearbyTileEffects', () => sys().resets > leaving.resets || JSON.stringify({ antes: leaving.resets, agora: sys().resets }));
    bl.log('biomeworld FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || leaving) return;
    frame++;
    if (frame === 1) sys().tile = ModContent.TileType('examplemod/ExampleTile');
    // A primeira contagem com o tipo certo vem em até 5 quadros.
    if (frame === 10) {
        check('teleporte', () => {
            try { return teleportCase(); } finally { cleanup(); }
        });
        ManualBiome.on = true;
    }
    if (frame === 12) check('saída', exitCase);
});
bl.log('biomeworld: carregado');

export default class TestBiomeworld extends Mod {}
