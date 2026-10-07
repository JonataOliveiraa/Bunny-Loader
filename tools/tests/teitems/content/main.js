// Item de mod com SaveData nos expositores (TileEntityItemHooks), um jogador.
// Rodada 1: monta uma parede com moldura, estante, travessa, jarro, manequim e
// cabideiro longe do spawn; coloca o item pela função do jogo (o da mão) ou
// direto no manequim/cabideiro; tira um pela DropItem; salva o mundo e deixa
// um marcador. Rodada 2 (com o marcador): confere que os dados voltaram com o
// mundo, desmonta, salva de novo e apaga o marcador. Loga "teitems ...".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const TE = Terraria.GameContent.Tile_Entities;
const TileEntity = Terraria.DataStructures.TileEntity;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('teitems ' + label + ': ok');
        else { fails++; bl.log('teitems ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('teitems ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

export class TEProbe extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    owner = '';

    SetDefaults(item) {
        item.width = item.height = 16;
        item.maxStack = 1;
        item.accessory = true;
        item.damage = 10;
        item.useStyle = 1;
        this.owner = '';
    }

    SaveData(tag) { tag.Set('owner', this.owner); }
    LoadData(tag) { this.owner = tag.GetString('owner'); }
}

const tileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const killWall = (x, y) => W['void KillWall(int i, int j, bool fail)'](x, y, false);
const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const placeWall = (x, y, type) => W['void PlaceWall(int i, int j, int type, bool mute)'](x, y, type, true);
const placeObject = (x, y, type) => W['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'](x, y, type, true, 0, 0, -1, 1);

const KINDS = [
    { name: 'moldura', cls: 'TEItemFrame', tile: 395, wall: true, place: 'void PlaceItemInFrame(Player player, int x, int y)' },
    { name: 'estante', cls: 'TEWeaponsRack', tile: 471, wall: true, place: 'void PlaceItemInFrame(Player player, int x, int y)' },
    { name: 'travessa', cls: 'TEFoodPlatter', tile: 520, place: 'void PlaceItemInFrame(Player player, int x, int y)' },
    { name: 'jarro', cls: 'TEDeadCellsDisplayJar', tile: 698, hang: true, place: 'void PlaceItemInJar(Player player, int x, int y)' },
    { name: 'manequim', cls: 'TEDisplayDoll', tile: 470, slot: (te) => [te._equip, 3] },
    { name: 'cabideiro', cls: 'TEHatRack', tile: 475, slot: (te) => [te._items, 0] },
    { name: 'moldura-drop', cls: 'TEItemFrame', tile: 395, wall: true, place: 'void PlaceItemInFrame(Player player, int x, int y)', drop: true },
];

function area() {
    const x0 = Main.spawnTileX - 80, top = Main.spawnTileY - 40;
    return { x0, x1: x0 + 34, top, floorY: top + 10 };
}

const marker = () => String(Main.worldPathName) + '.teitems.test';

function probe(owner) {
    const i = Terraria.Item.new();
    i['void .ctor()']();
    i['void SetDefaults(int Type, ItemVariant variant)'](ModContent.ItemType(TEProbe), null);
    i.ModItem.owner = owner;
    return i;
}

// Manequim e cabideiro com item não quebram: esvazia antes de desmontar.
function emptyDisplays(a) {
    for (let x = a.x0 - 1; x <= a.x1 + 1; x++) {
        for (let y = a.top - 1; y <= a.floorY; y++) {
            const key = (x & 0xFFFF) | (y << 16);
            if (!TileEntity.ByPosition.ContainsKey(key)) continue;
            const te = TileEntity.ByPosition.get_Item(key);
            const name = te.GetType().Name;
            const arrays = name === 'TEDisplayDoll' ? [te._equip, te._dyes, te._misc] : name === 'TEHatRack' ? [te._items, te._dyes] : [];
            for (const items of arrays) for (let i = 0; i < items.length; i++) items[i]['void TurnToAir()']();
        }
    }
}

function build() {
    const a = area();
    emptyDisplays(a);
    for (let x = a.x0 - 1; x <= a.x1 + 1; x++) {
        for (let y = a.top - 1; y <= a.floorY + 1; y++) {
            kill(x, y);
            killWall(x, y);
            if (y < a.floorY) placeWall(x, y, 1);
        }
        place(x, a.floorY, 1);
    }
    const placed = [];
    let x = a.x0 + 1;
    for (const k of KINDS) {
        const data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](k.tile, 0, 0);
        const w = data.Width, h = data.Height, ox = data.Origin.X, oy = data.Origin.Y;
        const topY = k.wall || k.hang ? a.floorY - 6 : a.floorY - h;
        if (k.hang) for (let i = 0; i < w; i++) place(x + i, topY - 1, 1);
        const px = x + ox, py = topY + oy;
        const ok = placeObject(px, py, k.tile);
        let id = ok ? TE[k.cls]['int Hook_AfterPlacement(int x, int y, int type, int style, int direction, int alternate)'](px, py, k.tile, 0, 1, 0) : -1;
        let te = id >= 0 && TileEntity.ByID.ContainsKey(id) ? TileEntity.ByID.get_Item(id) : null;
        // A estante 3x3 com direção: o canto de verdade sai dos quadros.
        if (te && k.tile === 471) {
            const t = tileAt(te.Position.X, te.Position.Y);
            const cx = te.Position.X - Math.floor((t.frameX % 54) / 18), cy = te.Position.Y - Math.floor((t.frameY % 54) / 18);
            if (cx !== te.Position.X || cy !== te.Position.Y) {
                const type = te.type;
                TileEntity['void Remove(TileEntity entity, bool ignorePosition)'](te, false);
                TileEntity['void PlaceEntityNet(int x, int y, int type)'](cx, cy, type);
                const key = (cx & 0xFFFF) | (cy << 16);
                te = TileEntity.ByPosition.ContainsKey(key) ? TileEntity.ByPosition.get_Item(key) : null;
                id = te ? te.ID : -1;
            }
        }
        placed.push({ k, x, y: topY, id, te });
        check('montou ' + k.name, () => (ok && !!te) || `colocou ${ok}, id ${id}, tile ${bl.tiles.typeAt(px, py)}`);
        x += w + 2;
    }
    return placed;
}

function phase1() {
    const player = Main.player[Main.myPlayer];
    const placed = build();
    const sel = player.selectedItem;
    const saved = player.inventory[sel];
    const result = [];
    for (const p of placed) {
        if (!p.te) continue;
        const owner = 'Teste ' + p.k.name;
        if (p.k.place) {
            player.inventory[sel] = probe(owner);
            player.itemTime = 0;
            player.itemAnimation = 0;
            TE[p.k.cls][p.k.place](player, p.te.Position.X, p.te.Position.Y);
            const te = TileEntity.ByID.get_Item(p.id);
            const t = tileAt(p.te.Position.X, p.te.Position.Y);
            bl.log(`teitems ${p.k.name}: expositor em ${p.te.Position.X},${p.te.Position.Y}, tile ${t.type} frame ${t.frameX},${t.frameY}`);
            check('colocou com a mão: ' + p.k.name, () =>
                (te.item.type === ModContent.ItemType(TEProbe) && te.item.ModItem.owner === owner) ||
                `tipo ${te.item.type}, dono "${te.item.ModItem && te.item.ModItem.owner}"`);
        } else {
            const [items, index] = p.k.slot(p.te);
            items[index] = probe(owner);
        }
        if (p.k.drop) {
            const te = TileEntity.ByID.get_Item(p.id);
            te.DropItem();
            check('tirou e o item caiu com os dados: ' + p.k.name, () => {
                for (let i = 0; i < 400; i++) {
                    const it = Main.item[i];
                    if (!it || !it.active || it.inner.type !== ModContent.ItemType(TEProbe)) continue;
                    if (it.inner.ModItem.owner === owner) {
                        it['void TurnToAir()']();
                        return true;
                    }
                }
                return 'nenhum item caído com o dono ' + owner;
            });
            continue;
        }
        result.push({ name: p.k.name, cls: p.k.cls, x: p.te.Position.X, y: p.te.Position.Y, owner });
    }
    player.inventory[sel] = saved;
    bl.file.write(marker(), JSON.stringify(result));
    Terraria.IO.WorldFile['void SaveWorld(bool useCloudSaving, bool resetTime, WorldFile.WorldSaveContext saveContext)'](false, false, 0);
    check('o .wld.bl.json tem os expositores', () => {
        const text = bl.file.read(String(Main.worldPathName) + '.bl.json') || '';
        return result.every((r) => text.includes(r.owner)) || text.slice(0, 300);
    });
    bl.log('teitems: rodada 1 pronta; rode de novo para conferir a volta');
}

function phase2(list) {
    for (const r of list) {
        check('voltou com o mundo: ' + r.name, () => {
            const key = (r.x & 0xFFFF) | (r.y << 16);
            if (!TileEntity.ByPosition.ContainsKey(key)) return 'sem o expositor em ' + r.x + ',' + r.y;
            const te = TileEntity.ByPosition.get_Item(key);
            let item = null;
            if (te.GetType().Name === 'TEDisplayDoll') item = te._equip[3];
            else if (te.GetType().Name === 'TEHatRack') item = te._items[0];
            else item = te.item;
            return (item.type === ModContent.ItemType(TEProbe) && item.ModItem.owner === r.owner) ||
                `tipo ${item.type}, dono "${item.ModItem && item.ModItem.owner}"`;
        });
    }
    const a = area();
    emptyDisplays(a);
    for (let x = a.x0 - 1; x <= a.x1 + 1; x++) {
        for (let y = a.top - 1; y <= a.floorY; y++) {
            kill(x, y);
            killWall(x, y);
        }
    }
    for (let i = 0; i < 400; i++) {
        const it = Main.item[i];
        if (it && it.active && it.inner.type === ModContent.ItemType(TEProbe)) it['void TurnToAir()']();
    }
    bl.file.delete(marker());
    Terraria.IO.WorldFile['void SaveWorld(bool useCloudSaving, bool resetTime, WorldFile.WorldSaveContext saveContext)'](false, false, 0);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames !== 90) return;
    done = true;
    const text = bl.file.read(marker());
    if (text) check('rodada 2', () => phase2(JSON.parse(text)));
    else check('rodada 1', phase1);
    bl.log('teitems FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('teitems: carregado');

export default class TestTEItems extends Mod {}
