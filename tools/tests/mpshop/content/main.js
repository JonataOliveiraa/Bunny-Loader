// A loja de morador de mod (NPCShop) no multijogador. Loga "mps <papel> <caso>".
//
// Host: quando o cliente entra, poe a Pessoa do Example Mod ao lado dele e
// fica olhando, pelo que chega da rede, o jogador do cliente: com quem ele
// conversa, o item comprado e o que saiu do bolso dele.
//
// Cliente: recebe 1 platina e 30 Exemplos de Item (a moeda propria da loja),
// conversa com a Pessoa, toca no botao Loja pelo caminho do jogo
// (GUINPCDialogue.Option1Clicked -> OnChatButtonClicked -> NPCShop.Open) e
// compra como o dedo compra (GUIShop.PurchasePressedAndHeld): a Espada de
// Exemplo em moedas e a Espada de Energia em Exemplos de Item. Nada disso e
// mandado a mao: o que o host ve chega pela sincronizacao do proprio jogo.
const Main = Terraria.Main;
const COIN_VALUE = { 71: 1, 72: 100, 73: 10000, 74: 1000000 };

let role = '', fails = 0, done = false;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mps ' + role + ' ' + label + ': ok');
        else { fails++; bl.log('mps ' + role + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mps ' + role + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
function finish() {
    done = true;
    bl.log('mps ' + role + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

const sendData = Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'];
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const GUIInstance = bl.classOf('', 'GUIInstance');

// Os tipos do Example Mod pelo nome da classe (o getTypeByName so procura no
// mod de quem chama).
function modItemType(name) {
    for (let t = bl.items.vanillaCount; bl.items.isModItem(t); t++) {
        const m = ModItem.getModItem(t);
        if (m && m.constructor.name === name) return t;
    }
    return -1;
}
function modNpcType(name) {
    for (let t = bl.npcs.vanillaCount; bl.npcs.isModNpc(t); t++) {
        const m = ModNPC.getModNPC(t);
        if (m && m.constructor.name === name) return t;
    }
    return -1;
}
let T = null;
const types = () => T || (T = {
    person: modNpcType('ExamplePerson'),
    sword: modItemType('ExampleMeleeWeapon'),
    energy: modItemType('ExampleSwingingEnergySword'),
    currency: modItemType('ExampleItem'),
});

// Todo o inventario, com o 58 (o item no cursor, que o jogo sincroniza ali).
function coins(p) {
    let v = 0;
    for (let i = 0; i < 59; i++) {
        const it = p.inventory[i];
        if (COIN_VALUE[it.type]) v += COIN_VALUE[it.type] * it.stack;
    }
    return v;
}
function countOf(p, type) {
    let n = 0;
    for (let i = 0; i < 59; i++) if (p.inventory[i].type === type) n += p.inventory[i].stack;
    if (Main.myPlayer === p.whoAmI && Main.mouseItem.type === type) n += Main.mouseItem.stack;
    return n;
}
function setSlot(p, slot, type, stack) {
    p.inventory[slot]['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    if (type > 0) p.inventory[slot].stack = stack;
    sendData(5, -1, -1, null, p.whoAmI, slot, p.inventory[slot].prefix, 0, 0, 0, 0);
}

// ------------------------------- cliente -------------------------------
let cStep = 0, cAt = 0, person = -1, saved = null, shop = null;

function nearestPerson(p) {
    let best = -1, bestD = 1e9;
    for (let i = 0; i < 200; i++) {
        const n = Main.npc[i];
        if (!n.active || n.type !== types().person) continue;
        const d = Math.abs(n.Center.X - p.Center.X) + Math.abs(n.Center.Y - p.Center.Y);
        if (d < bestD) { best = i; bestD = d; }
    }
    return best;
}
function buy(p, type, label) {
    const items = Main.instance.shop[Main.npcShop].item;
    let slot = -1;
    for (let i = 0; i < items.length; i++) if (items[i].type === type) { slot = i; break; }
    if (slot < 0) return { error: 'nao esta na loja' };
    const before = { coins: coins(p), currency: countOf(p, types().currency), have: countOf(p, type) };
    const gui = GUIInstance.Active.GUIShop;
    gui._selectedItem = slot;
    gui['void PurchasePressedAndHeld(bool delayedPurchase)'](false);
    // A compra poe o item no cursor e comeca um arraste: o dedo solta o item
    // num espaco do inventario. Sem esse toque o arraste termina fora de
    // qualquer espaco e o item some (o jogo so resolve o soltar num ItemOver).
    let dropped = -1;
    if (Main.mouseItem.type === type) {
        for (let i = 10; i < 50 && dropped < 0; i++) if (p.inventory[i].type === 0) dropped = i;
        if (dropped >= 0) Terraria.UI.ItemSlot['void LeftClick(Item[] inv, int context, int slot)'](p.inventory, 0, dropped);
    }
    const after = { coins: coins(p), currency: countOf(p, types().currency), have: countOf(p, type) };
    const where = dropped >= 0 && p.inventory[dropped].type === type ? 'solto no espaco ' + dropped : 'cursor ' + Main.mouseItem.type;
    bl.log(`mps cliente ${label}: moedas ${before.coins} -> ${after.coins}, moeda propria ${before.currency} -> ${after.currency}, item ${before.have} -> ${after.have} (${where})`);
    return { before, after };
}

function clientTick(frames, p) {
    const t = types();
    if (cStep === 0 && frames >= 150) {
        person = nearestPerson(p);
        if (person < 0) {
            if (frames % 120 === 0) bl.log('mps cliente: esperando a Pessoa do host');
            if (frames > 3000) { check('a Pessoa do host chegou', () => 'nao chegou'); finish(); }
            return;
        }
        saved = { s50: [p.inventory[50].type, p.inventory[50].stack], s1: [p.inventory[1].type, p.inventory[1].stack] };
        setSlot(p, 50, 74, 1);                 // 1 platina
        setSlot(p, 1, t.currency, 30);         // 30 Exemplos de Item
        cStep = 1; cAt = frames;
        return;
    }
    // A conversa fica aberta meio segundo antes do toque em Loja (o host precisa
    // ve-la pela rede). O teste nao abre a janela de conversa de verdade, e o
    // jogo solta o talkNPC sozinho: mantem a cada quadro. Tocar em Loja com o
    // talkNPC em -1 derruba o jogo (Main.npc[-1] no Option1Clicked, sem
    // checagem de limite); um dedo nao consegue fazer isso.
    if (cStep === 1 && frames - cAt >= 30 && frames - cAt <= 60) p['void SetTalkNPC(int npcIndex)'](person);
    if (cStep === 1 && frames - cAt === 60) {
        check('conversa e botao Loja abrem a loja do NPCShop', () => {
            GUIInstance.Active.GUINPCDialogue['void Option1Clicked(int healCost)'](0);
            shop = NPCShop.get(t.person, 'Shop');
            if (!shop) return 'NPCShop da Pessoa nao registrada';
            if (Main.npcShop !== shop.Index) return `npcShop ${Main.npcShop}, esperado ${shop.Index}`;
            const got = Main.instance.shop[Main.npcShop].item;
            const has = (type) => { for (let i = 0; i < got.length; i++) if (got[i].type === type) return true; return false; };
            return (has(t.sword) && has(t.energy)) || 'itens da loja faltando';
        });
        cStep = 2; cAt = frames;
        return;
    }
    if (cStep === 2 && frames - cAt === 30) {
        check('compra em moedas (Espada de Exemplo)', () => {
            const r = buy(p, t.sword, 'Espada de Exemplo');
            if (r.error) return r.error;
            return (r.after.coins < r.before.coins && r.after.have === r.before.have + 1) || 'nao comprou';
        });
        cStep = 3; cAt = frames;
        return;
    }
    if (cStep === 3 && frames - cAt === 60) {
        check('compra em moeda propria (Espada de Energia por 10 Exemplos de Item)', () => {
            const r = buy(p, t.energy, 'Espada de Energia');
            if (r.error) return r.error;
            return (r.after.currency === r.before.currency - 10 && r.after.have === r.before.have + 1 &&
                    r.after.coins === r.before.coins) || 'nao comprou certo';
        });
        cStep = 4; cAt = frames;
        return;
    }
    if (cStep === 4 && frames - cAt === 60) {
        check('os dois itens comprados ficam no inventario', () =>
            (countOf(p, t.sword) === 1 && countOf(p, t.energy) === 1) || `Espada ${countOf(p, t.sword)}, Energia ${countOf(p, t.energy)}, cursor ${Main.mouseItem.type}`);
    }
    // Espera o host confirmar pela rede antes de desfazer o bolso.
    if (cStep === 4 && frames - cAt === 600) {
        p['void SetTalkNPC(int npcIndex)'](-1);
        Main.npcShop = 0;
        if (Main.mouseItem.type > 0) Main.mouseItem['void SetDefaults(int Type, ItemVariant variant)'](0, null);
        for (let i = 0; i < 59; i++) {
            const ty = p.inventory[i].type;
            if (ty === t.sword || ty === t.energy) setSlot(p, i, 0, 0);
        }
        setSlot(p, 50, saved.s50[0], saved.s50[1]);
        setSlot(p, 1, saved.s1[0], saved.s1[1]);
        finish();
    }
}

// -------------------------------- host --------------------------------
let lifeChecked = false;
function checkLifeMax() {
    if (lifeChecked || hPerson < 0) return;
    lifeChecked = true;
    const n = Main.npc[hPerson];
    check('a Pessoa mantem a vida maxima com a IA de morador rodando', () =>
        (n.active && n.lifeMax === n.defLifeMax && n.lifeMax >= 250) || `vida ${n.life}/${n.lifeMax}, defLifeMax ${n.defLifeMax}`);
}
let hJoined = -1, hPerson = -1, hBase = null, sawTalk = false, sawSword = null, sawEnergy = null;

function remoteIndex() {
    for (let i = 0; i < 255; i++) if (i !== Main.myPlayer && Main.player[i].active) return i;
    return -1;
}
function hostTick(frames) {
    const r = remoteIndex();
    if (r < 0) return;
    const remote = Main.player[r];
    const t = types();
    if (hJoined < 0) {
        hJoined = frames;
        bl.log('mps host: cliente entrou (' + remote.name + ')');
    }
    if (hPerson < 0 && frames - hJoined === 120) {
        const src = Terraria.DataStructures.EntitySource_DebugCommand.new();
        hPerson = newNpc(src, Math.floor(remote.Center.X) + 64, Math.floor(remote.Bottom.Y), t.person, 0, 0, 0, 0, 0, r);
        bl.log('mps host: Pessoa posta ao lado do cliente (npc ' + hPerson + ')');
    }
    if (hPerson < 0) return;
    // A IA de morador faz lifeMax = defLifeMax a cada quadro: com o defLifeMax
    // do NPC de mod em 0, a vida maxima zerava e o cliente perdia a Pessoa.
    if (frames - hJoined === 420) checkLifeMax();
    const c = coins(remote), cur = countOf(remote, t.currency);
    if (!hBase && c >= 1000000 && cur >= 30) {
        hBase = { coins: c, currency: cur };
        bl.log(`mps host: bolso do cliente chegou: ${c} de cobre, ${cur} Exemplos de Item`);
    }
    if (remote.talkNPC >= 0 && Main.npc[remote.talkNPC].type === t.person) sawTalk = true;
    if (hBase && !sawSword && countOf(remote, t.sword) > 0) sawSword = { coins: c, frame: frames };
    if (hBase && !sawEnergy && countOf(remote, t.energy) > 0) sawEnergy = { currency: cur, coins: c, frame: frames };

    const late = frames - hJoined > 3000;
    if ((sawSword && sawEnergy && frames - Math.max(sawSword.frame, sawEnergy.frame) > 60) || late) {
        checkLifeMax();
        check('o host ve o cliente conversando com a Pessoa', () => sawTalk || 'talkNPC nunca apontou para a Pessoa');
        check('a Espada de Exemplo chegou ao inventario do cliente, paga em moedas', () =>
            !sawSword ? 'nao chegou' : sawSword.coins < hBase.coins || `moedas ${hBase.coins} -> ${sawSword.coins}`);
        check('a Espada de Energia chegou, paga com 10 Exemplos de Item', () =>
            !sawEnergy ? 'nao chegou' : cur === hBase.currency - 10 || `Exemplos de Item ${hBase.currency} -> ${cur}`);
        const n = Main.npc[hPerson];
        if (n.active && n.type === t.person) {
            n.active = false;
            sendData(23, -1, -1, null, hPerson, 0, 0, 0, 0, 0, 0);
        }
        finish();
    }
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    ++frames;
    if (frames === 1) {
        const mode = Main.netMode;
        role = (mode & 2) ? 'host' : mode === 1 ? 'cliente' : '';
        bl.log(`mps: netMode ${mode}, papel ${role || 'nenhum'}`);
    }
    if (!role || done) return;
    if (!self.dead) { self.statLife = self.statLifeMax2; self.immune = true; self.immuneTime = 10; }
    if (role === 'cliente') clientTick(frames, self);
    else hostTick(frames);
});
bl.log('mps: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestMpshop extends Mod {}
