// Prefixos de mod no multijogador: cada lado põe no inventário itens com os
// prefixos do Example Mod e manda os slots (NetMessage 5, que leva o prefixo
// como byte); o outro lado recebe o número e aplica o ModPrefix sozinho (o
// dano tem de sair igual ao de um item novo com o mesmo prefixo). O host
// confere também o acessório do cliente: +4 de defesa enquanto ele usa.
// Instale no host e no cliente, com o Example Mod. Loga "mpp <papel> <caso>".
const Main = Terraria.Main;
const { ItemID } = Terraria.ID;

let role = '', fails = 0, done = false;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mpp ' + role + ' ' + label + ': ok');
        else { fails++; bl.log('mpp ' + role + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mpp ' + role + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
function finish() {
    done = true;
    bl.log('mpp ' + role + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

const sendData = Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'];
const ARMOR0 = Terraria.ID.PlayerItemSlotID.Armor0;
const T = (name) => ModContent.ItemType(name);
const P = (name) => ModContent.PrefixType('examplemod/' + name);

const PLAN = () => [
    [ItemID.NightsEdge, 'ExamplePrefix'],
    [T('ExampleMultiplePrefixCategoryWeapon'), 'ExampleDerivedPrefix'],
];

function fresh(type, prefix) {
    const it = Terraria.Item.new();
    it['void .ctor()']();
    it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    it['bool Prefix(int prefixWeWant)'](prefix);
    return it;
}
function put(item, type, prefix) {
    item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    if (prefix) item['bool Prefix(int prefixWeWant)'](prefix);
}
function remoteIndex() {
    for (let i = 0; i < 255; i++) if (i !== Main.myPlayer && Main.player[i].active) return i;
    return -1;
}

const used = [];   // [slot do inventário]
let accSaved = null;
function dress() {
    const p = Main.player[Main.myPlayer];
    for (const [type, name] of PLAN()) {
        let slot = -1;
        for (let i = 10; i < 50; i++) if (p.inventory[i].type === 0 && !used.includes(i)) { slot = i; break; }
        if (slot < 0) { check('lugar vazio no inventário', () => 'nenhum'); continue; }
        used.push(slot);
        put(p.inventory[slot], type, P(name));
        sendData(5, -1, -1, null, p.whoAmI, slot, p.inventory[slot].prefix, 0, 0, 0, 0);
    }
    accSaved = { type: p.armor[3].type, prefix: p.armor[3].prefix };
    put(p.armor[3], ItemID.CloudinaBottle, P('ExampleAccessoryPrefix'));
    sendData(5, -1, -1, null, p.whoAmI, ARMOR0 + 3, p.armor[3].prefix, 0, 0, 0, 0);
    bl.log('mpp ' + role + ': itens com prefixo postos');
}
function takeOffAccessory() {
    const p = Main.player[Main.myPlayer];
    put(p.armor[3], accSaved.type, accSaved.prefix);
    sendData(5, -1, -1, null, p.whoAmI, ARMOR0 + 3, p.armor[3].prefix, 0, 0, 0, 0);
}
function undress() {
    const p = Main.player[Main.myPlayer];
    for (const slot of used) {
        put(p.inventory[slot], 0, 0);
        sendData(5, -1, -1, null, p.whoAmI, slot, 0, 0, 0, 0, 0);
    }
}

// O outro jogador, como este aparelho o vê.
const remoteReady = (r) => Main.player[r].armor[3].prefix === P('ExampleAccessoryPrefix');
function remoteChecks(r) {
    const other = Main.player[r];
    for (const [type, name] of PLAN()) {
        check(`recebe ${name} e aplica o prefixo`, () => {
            let it = null;
            for (let i = 0; i < 50 && !it; i++) if (other.inventory[i].type === type) it = other.inventory[i];
            if (!it) return 'item nao chegou';
            const want = fresh(type, P(name));
            return (it.prefix === P(name) && it.damage === want.damage && it.value === want.value) ||
                `prefixo ${it.prefix}/${P(name)}, dano ${it.damage}/${want.damage}, valor ${it.value}/${want.value}`;
        });
    }
}

// ------------------------------- cliente -------------------------------
let clientSaw = -1;
function clientStep(frames) {
    if (frames === 120) dress();
    if (frames < 130) return;

    const r = remoteIndex();
    if (clientSaw < 0 && r >= 0 && remoteReady(r)) clientSaw = frames;
    if (clientSaw >= 0 && frames - clientSaw === 30) remoteChecks(r);
    // O host mede a defesa do acessório antes e depois de sair.
    if (clientSaw >= 0 && frames - clientSaw === 90) takeOffAccessory();
    if (clientSaw >= 0 && frames - clientSaw === 240) { undress(); finish(); }
    if (clientSaw < 0 && frames > 1800) { check('vê o host com os itens', () => 'nunca viu'); undress(); finish(); }
}

// -------------------------------- host --------------------------------
let hostStart = -1, hostSaw = -1, defWith = -1, offAt = -1;
function hostStep(frames) {
    const r = remoteIndex();
    if (r < 0) return;
    if (hostStart < 0) { hostStart = frames; bl.log('mpp host: cliente entrou (' + Main.player[r].name + ')'); }
    const other = Main.player[r];

    if (hostSaw < 0 && remoteReady(r)) {
        hostSaw = frames;
        dress();
    }
    if (hostSaw >= 0 && frames - hostSaw === 30) {
        remoteChecks(r);
        defWith = other.statDefense;
    }
    if (hostSaw >= 0 && defWith >= 0 && offAt < 0 && other.armor[3].prefix !== P('ExampleAccessoryPrefix')) offAt = frames;
    if (offAt >= 0 && frames - offAt === 30) {
        const defWithout = other.statDefense;
        check('acessório do cliente: +4 de defesa no host', () =>
            defWith - defWithout === 4 || `${defWith} com, ${defWithout} sem`);
        takeOffAccessory();
        undress();
        finish();
    }
    if (hostSaw < 0 && frames - hostStart > 1800) { check('vê o cliente com os itens', () => 'nunca viu'); finish(); }
    if (hostSaw >= 0 && offAt < 0 && frames - hostSaw > 1800) { check('o cliente tira o acessório', () => 'nunca tirou'); undress(); finish(); }
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 1) {
        const mode = Main.netMode;
        role = (mode & 2) ? 'host' : mode === 1 ? 'cliente' : '';
        bl.log(`mpp: netMode ${mode}, papel ${role || 'nenhum'}`);
        if (!role) { done = true; return; }
    }
    if (!self.dead) self.statLife = self.statLifeMax2;
    if (role === 'cliente') clientStep(frames);
    else hostStep(frames);
});
bl.log('mpp: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestMpprefix extends Mod {}
