// ModPylon: dois pilares de mod, A e B (tipos de pilar novos, 11 e 12), no
// alto, longe do spawn.
//   - colocados pelo caminho do jogador, cada um ganha a TEModdedPylon;
//   - um segundo A é recusado (CanPlacePylon: um por tipo);
//   - a lista do jogo (Main.PylonSystem.Pylons) tem os dois;
//   - o jogador ao lado de A está "perto de um pilar";
//   - o teletransporte (HandleTeleportRequest) leva de A até B;
//   - com o mapa aberto, o ícone do mod é desenhado (log "modpylon pose");
//   - quebrados, saem da lista.
// Loga "modpylon <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileObjectData } = Terraria.ObjectData;
const STONE = Terraria.ID.TileID.Stone;

let mapIcons = 0;

export class TestPylonEntity extends TEModdedPylon {}

export class TestPylon extends ModPylon {
    Texture = 'Textures/TestPylon';

    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileLighted[this.Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance(TestPylonEntity).PlacementHook(false);
        TileObjectData.addTile(this.Type);
        this.AddMapEntry(Color.White, 'Test Pylon');
        this.crystal = ModContent.Request('Textures/TestPylon_Crystal');
    }

    ValidTeleportCheck_NPCCount(pylonInfo, defaultNecessaryNPCCount) { return true; }
    KillMultiTile(i, j, frameX, frameY) { ModContent.GetInstance(TestPylonEntity).Kill(i, j); }
    SpecialDraw(i, j, spriteBatch) {
        this.DefaultDrawPylonCrystal(spriteBatch, i, j, this.crystal, this.crystal, Vector2.new(0, -12), Color.new(25, 25, 25, 25), Color.White, 1, 8);
    }
    DrawMapIcon(context, mouseOverText, pylonInfo, isNearPylon, drawColor, deselectedScale, selectedScale) {
        mapIcons++;
        super.DrawMapIcon(context, mouseOverText, pylonInfo, isNearPylon, drawColor, deselectedScale, selectedScale);
    }
}

export class TestPylonB extends TestPylon {}

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modpylon ' + label + ': ok');
        else { fails++; bl.log('modpylon ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modpylon ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const A = () => ModContent.GetInstance(TestPylon);
const B = () => ModContent.GetInstance(TestPylonB);

function area() {
    const floor = Main.spawnTileY - 40;
    return { floor, ax: Main.spawnTileX - 100, bx: Main.spawnTileX - 130 };
}

function clear() {
    const { floor, ax, bx } = area();
    for (const x0 of [ax, bx]) {
        for (let i = x0 - 6; i <= x0 + 6; i++) for (let j = floor - 8; j <= floor; j++) if (bl.tiles.typeAt(i, j) >= 0) kill(i, j);
    }
}

// Como o jogador coloca: o alvo na origem (o meio de baixo do 3x4).
function placeAsPlayer(x, y, type) {
    Terraria.Player.tileTargetX = x;
    Terraria.Player.tileTargetY = y;
    const data = new Ref();
    const can = Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'](
        x, y, type, 0, 1, data, false, null);
    if (!can) return false;
    Main.player[Main.myPlayer]['TileObject PlaceThing_Tiles_PlaceIt(bool newObjectType, TileObject data, int tileToCreate)'](true, data.value, type);
    return bl.tiles.typeAt(x, y) === type;
}

function listed() {
    const list = Main.PylonSystem.Pylons;
    const out = [];
    for (let k = 0; k < list.Count; k++) {
        const info = list.get_Item(k);
        out.push({ x: info.PositionInTiles.X, y: info.PositionInTiles.Y, type: info.TypeOfPylon, info });
    }
    return out;
}

function stand(x) {
    const p = Main.player[Main.myPlayer];
    p.position.X = x * 16;
    p.position.Y = (area().floor - 3) * 16;
    p.velocity.X = 0;
    p.velocity.Y = 0;
    p.fallStart = Math.floor(p.position.Y / 16);
}

let eA = null, eB = null;
function build() {
    const { floor, ax, bx } = area();
    clear();
    for (const x0 of [ax, bx]) for (let i = x0 - 5; i <= x0 + 5; i++) placeTile(i, floor, STONE);
    check('A e B colocados pelo jogador, cada um com a TEModdedPylon', () => {
        if (!placeAsPlayer(ax, floor - 1, A().Type) || !placeAsPlayer(bx, floor - 1, B().Type)) return 'não colocou';
        eA = ModTileEntity.TryGet(ax, floor - 1, TestPylonEntity);
        eB = ModTileEntity.TryGet(bx, floor - 1, TestPylonEntity);
        return (eA && eB && eA.TryGetModPylon() === A() && eB.TryGetModPylon() === B()) || `A ${!!eA}, B ${!!eB}`;
    });
    check('tipos de pilar novos (11 e 12)', () => (A().PylonType === 11 && B().PylonType === 12) || `${A().PylonType}, ${B().PylonType}`);
    check('um segundo A é recusado (CanPlacePylon)', () => {
        const can = Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'](
            ax + 4, floor - 1, A().Type, 0, 1, new Ref(), true, null);
        return can === false || 'aceitou';
    });
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    const { floor, ax, bx } = area();
    if (frames === 1) check('preparo', build);
    if (frames < 15) stand(ax + 3);
    if (frames === 10) {
        check('a lista do jogo tem A e B', () => {
            const l = listed();
            const a = l.find((p) => p.type === 11), b = l.find((p) => p.type === 12);
            return (a && b && a.x === eA.Position.X && b.y === eB.Position.Y) || JSON.stringify(l.map(({ x, y, type }) => ({ x, y, type })));
        });
        check('ao lado de A, o jogador está perto de um pilar', () =>
            Terraria.GameContent.TeleportPylonsSystem['bool IsPlayerNearAPylon(Player player)'](Main.player[Main.myPlayer]) === true || 'não');
    }
    if (frames === 15) {
        check('o teletransporte leva até B', () => {
            const b = listed().find((p) => p.type === 12);
            Main.PylonSystem['void HandleTeleportRequest(TeleportPylonInfo info, int playerIndex)'](b.info, Main.myPlayer);
            const p = Main.player[Main.myPlayer];
            const dx = p.position.X / 16 - (b.x + 1), dy = p.position.Y / 16 - (b.y + 1);
            return Math.hypot(dx, dy) < 6 || `jogador em ${Math.round(p.position.X / 16)},${Math.round(p.position.Y / 16)}, B em ${b.x},${b.y}`;
        });
    }
    if (frames > 15 && frames < 600) {
        stand(bx + 3);
        if (frames >= 60) {
            Main.mapFullscreen = true;
            Main.mapFullscreenScale = 4;
            Main.mapFullscreenPos = Vector2.new(bx + 1, floor - 2);
        }
    }
    if (frames === 120) bl.log('modpylon pose');
    if (frames === 600) {
        Main.mapFullscreen = false;
        check('o mapa desenhou o ícone do mod', () => mapIcons > 0 || 'DrawMapIcon não foi chamado');
        clear();
    }
    if (frames === 615) {
        check('quebrados, saem da lista', () => {
            const l = listed().filter((p) => p.type >= 11);
            return (l.length === 0 && ModTileEntity.TryGet(ax, floor - 1) === null) || JSON.stringify(l.map(({ x, y, type }) => ({ x, y, type })));
        });
        const p = Main.player[Main.myPlayer];
        p.position.X = Main.spawnTileX * 16;
        p.position.Y = (Main.spawnTileY - 3) * 16;
        p.fallStart = Math.floor(p.position.Y / 16);
        done = true;
        bl.log('modpylon FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modpylon: carregado');

export default class TestModPylon extends Mod {}
