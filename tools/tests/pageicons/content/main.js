// Os ícones que o celular tira de tabelas fixas, com o conteúdo do Example Mod
// (precisa dele ligado). Num chão de pedra no alto, longe do spawn:
//   - página do baú: o ícone do ExampleChest é a textura do item dele; o da
//     ExampleDresser aberta como baú, a do item dela; sem baú, o do jogo;
//   - página da cômoda (Category.Dresser): o item da ExampleDresser;
//   - página da loja: conversando com a ExamplePerson, a cabeça dela;
//   - placa: o quadro da ExampleSign mostra o ExampleSignItem e o nome dele.
// Abre o baú, a placa e a loja para as capturas ("pageicons pose <nome>").
// Loga "pageicons <caso>: ok | FALHOU" e "pageicons FIM".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const TextureAssets = Terraria.GameContent.TextureAssets;
const PlaceObject = W['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'];
const PlaceChest = W['int PlaceChest(int x, int y, ushort type, bool notNearOtherChests, int style)'];
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const ICON = 'Texture2D GetCategoryTexture(GUIPageIcons.Category category, bool selected)';
const CHEST = 3, SHOP = 4, DRESSER = 11;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('pageicons ' + label + ': ok');
        else { fails++; bl.log('pageicons ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('pageicons ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const EX = (name) => 'examplemod/' + name;
const me = () => Main.player[Main.myPlayer];
const gui = () => bl.classOf('', 'GUIInstance').Active;
const icon = (category) => gui().GUIPageIcons[ICON](category, false);
const address = (texture) => texture ? bl.addressOf(texture) : 0;
const itemTexture = (type) => TextureAssets.Item[type].Value;
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);

function area() {
    const x0 = Main.spawnTileX + 60, floor = Main.spawnTileY - 40;
    return { x0, floor };
}

function clearArea() {
    const { x0, floor } = area();
    for (let i = x0 - 4; i <= x0 + 20; i++) {
        for (let j = floor - 8; j <= floor + 1; j++) if (bl.tiles.typeAt(i, j) >= 0) kill(i, j);
    }
}

// A placa fecha com o jogador longe dela: ele fica ao lado do que está aberto.
let standX = 4;
function keepAlive() {
    const p = me(), { x0, floor } = area();
    p.position = Vector2.new((x0 + standX) * 16 - p.width / 2, floor * 16 - p.height);
    p.velocity = Vector2.new(0, 0);
    p.fallStart = floor - 3;
    p.statLife = p.statLifeMax2;
}

const state = {};

function build() {
    const { x0, floor } = area();
    clearArea();
    for (let i = x0 - 2; i <= x0 + 18; i++) placeTile(i, floor, 1);

    state.chest = PlaceChest(x0 + 1, floor - 1, ModContent.TileType(EX('ExampleChest')), false, 0);
    PlaceObject(x0 + 8, floor - 1, ModContent.TileType(EX('ExampleDresser')), true, 0, 0, -1, -1);
    state.dresser = { x: x0 + 7, y: floor - 2 };
    PlaceObject(x0 + 13, floor - 1, ModContent.TileType(EX('ExampleSign')), true, 0, 0, -1, -1);
    state.sign = Terraria.Sign['int ReadSign(int i, int j, bool CreateIfMissing)'](x0 + 13, floor - 1, true);
    if (state.sign >= 0) Terraria.Sign.TextSign(state.sign, 'pageicons');

    bl.log('pageicons montagem: baú ' + state.chest + ', cômoda ' + bl.tiles.typeAt(state.dresser.x, state.dresser.y) + ', placa ' + state.sign);
}

function checkIcons() {
    const p = me();
    const generic = address(icon(CHEST));

    check('baú de mod: o item ExampleChest', () => {
        const chest = Main.chest[state.chest];
        p.chest = state.chest;
        p.chestX = chest.x;
        p.chestY = chest.y;
        const got = address(icon(CHEST));
        p.chest = -1;
        return got === address(itemTexture(ModContent.ItemType(EX('ExampleChest')))) || 'textura ' + got.toString(16);
    });

    check('sem baú aberto: o ícone do jogo', () => generic !== 0 && generic !== address(itemTexture(ModContent.ItemType(EX('ExampleChest')))) || 'textura ' + generic);

    const dresserItem = address(itemTexture(ModContent.ItemType(EX('ExampleDresser'))));
    check('cômoda de mod aberta como baú: o item ExampleDresser', () => {
        p.chest = 0;
        p.chestX = state.dresser.x;
        p.chestY = state.dresser.y;
        const got = address(icon(CHEST));
        p.chest = -1;
        return got === dresserItem || 'textura ' + got.toString(16);
    });

    check('página da cômoda: o item ExampleDresser', () => {
        Main.interactedDresserTopLeftX = state.dresser.x;
        Main.interactedDresserTopLeftY = state.dresser.y;
        const got = address(icon(DRESSER));
        return got === dresserItem || 'textura ' + got.toString(16);
    });

    const type = ModContent.NPCType(EX('ExamplePerson'));
    state.npc = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.Center.X) + 40,
                       Math.floor(p.position.Y + p.height), type, 0, 0, 0, 0, 0, Main.myPlayer);
    check('loja do morador de mod: a cabeça dele', () => {
        p['void SetTalkNPC(int npcIndex)'](state.npc);
        const got = address(icon(SHOP));
        p['void SetTalkNPC(int npcIndex)'](-1);
        const head = bl.npcs.headSlot(type);
        return (head >= 0 && got === address(TextureAssets.NpcHead[head].Value)) || 'cabeça ' + head + ', textura ' + got.toString(16);
    });
}

// O que o DrawSign desenha, enquanto a placa está aberta.
const drawn = { names: [], textures: new Set() };
function watchSign() {
    const DrawSign = bl.classOf('', 'GUINPCDialogue')['void DrawSign()'];
    bl.classOf('', 'GUIString')['void Draw(String_Layout layout, string value)'].hook((original, layout, value) => {
        original(layout, value);
        if (drawn.names.length < 50) drawn.names.push(value);
    }, { whileIn: DrawSign });
    Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
        (original, self, texture, ...rest) => {
            original(self, texture, ...rest);
            drawn.textures.add(address(texture));
        }, { whileIn: DrawSign });
}

function openChest() {
    const chest = Main.chest[state.chest];
    me()['void OpenChest(int x, int y, int newChest)'](chest.x, chest.y, state.chest);
    bl.log('pageicons pose baú');
}

function closeAll() {
    const p = me();
    gui().GUIPageIcons['void Close(bool hideChat)'](true);
    p.chest = -1;
    p.sign = -1;
    p['void SetTalkNPC(int npcIndex)'](-1);
    Main.playerInventory = false;
    Main.npcChatText = '';
}

function openSign() {
    standX = 12;
    keepAlive();
    me()['void OpenSign(int s)'](state.sign);
    bl.log('pageicons pose placa');
}

function checkSign() {
    const item = ModContent.ItemType(EX('ExampleSignItem'));
    const name = Terraria.Lang.GetItemNameValue(item);
    check('placa de mod: nome do item no quadro', () => drawn.names.includes(name) || 'desenhou ' + JSON.stringify(drawn.names.slice(0, 6)));
    check('placa de mod: textura do item no quadro', () => drawn.textures.has(address(itemTexture(item))) || 'texturas ' + drawn.textures.size);
}

function openShop() {
    standX = 4;
    keepAlive();
    const p = me(), npc = Main.npc[state.npc];
    p['void SetTalkNPC(int npcIndex)'](state.npc);
    Main.npcChatText = npc.GetChat();
    gui().GUINPCDialogue['void Option1Clicked(int healCost)'](0);
    bl.log('pageicons pose loja');
}

function cleanup() {
    closeAll();
    const npc = Main.npc[state.npc];
    if (npc && npc.active) npc.active = false;
    if (state.sign >= 0) Terraria.Sign.KillSign(area().x0 + 13, area().floor - 2);
    clearArea();
    const p = me();
    p.position = Vector2.new(Main.spawnTileX * 16 - p.width / 2, Main.spawnTileY * 16 - p.height);
    p.fallStart = Main.spawnTileY;
}

// Chamadas do GetCategoryTexture e do DrawSign em 60 quadros (inventário fechado ou aberto).
let statsFrom = null;
function hookCounts() {
    const m = {};
    for (const h of bl.hookStats()) if (/GetCategoryTexture|DrawSign/.test(h.name)) m[h.name] = h.calls + '/' + h.js;
    return m;
}
function logStats(label) {
    const now = hookCounts();
    bl.log('pageicons custo ' + label + ': antes ' + JSON.stringify(statsFrom) + ', depois ' + JSON.stringify(now));
    statsFrom = now;
}

const steps = [
    [30, () => logStats('início')],
    [90 - 1, () => logStats('60 quadros fechado')],
    [90, () => { keepAlive(); check('montagem', build); }],
    [100, () => { check('ícones', checkIcons); check('vigia da placa', watchSign); openChest(); }],
    [279, () => logStats('baú aberto ~180 quadros')],
    [280, () => { closeAll(); openSign(); }],
    [460, () => { checkSign(); closeAll(); openShop(); }],
    [640, () => { closeAll(); cleanup(); bl.log('pageicons FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)')); }],
];

let frames = 0, next = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || next >= steps.length) return;
    frames++;
    if (frames >= 90) keepAlive();
    if (frames === steps[next][0]) {
        const run = steps[next++][1];
        try {
            run();
        } catch (e) {
            fails++;
            bl.log('pageicons passo ' + next + ': FALHOU com ' + e);
        }
    }
});
bl.log('pageicons: carregado');

export default class TestPageIcons extends Mod {}
