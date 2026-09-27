// Armaduras no multijogador: cada lado veste o conjunto de exemplo, as asas e
// a barba e manda os slots (NetMessage 5); o outro lado recebe os ITENS e
// monta sozinho os slots de mod (head/body/legs/wings/beard), que dependem de
// os dois terem os mesmos mods na mesma ordem. O bônus do conjunto roda para
// o jogador remoto também. Instale no host e no cliente, com o Example Mod.
// Loga "mpa <papel> <caso>: ok | FALHOU".
const Main = Terraria.Main;

let role = '', fails = 0, done = false;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mpa ' + role + ' ' + label + ': ok');
        else { fails++; bl.log('mpa ' + role + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mpa ' + role + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
function finish() {
    done = true;
    bl.log('mpa ' + role + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

const sendData = Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'];
const ARMOR0 = Terraria.ID.PlayerItemSlotID.Armor0;
const T = (name) => ModContent.ItemType(name);
const sample = (type) => Terraria.ID.ContentSamples.ItemsByType.get_Item(type);

// O que cada lado veste: slot da armadura -> item.
// O fantasia no slot de vaidade troca o que se desenha (FrameEffects do ModPlayer).
const OUTFIT = [[0, 'ExampleHelmet'], [1, 'ExampleBreastplate'], [2, 'ExampleLeggings'], [3, 'ExampleWings'], [4, 'ExampleBeard'],
                [13, 'ExampleCostume']];

function remoteIndex() {
    for (let i = 0; i < 255; i++) if (i !== Main.myPlayer && Main.player[i].active) return i;
    return -1;
}
function setArmor(p, slot, type) {
    p.armor[slot]['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    sendData(5, -1, -1, null, p.whoAmI, ARMOR0 + slot, p.armor[slot].prefix, 0, 0, 0, 0);
}

const saved = [];
function dress() {
    const p = Main.player[Main.myPlayer];
    for (const [slot, name] of OUTFIT) {
        saved.push([slot, p.armor[slot].type]);
        setArmor(p, slot, T(name));
    }
    bl.log('mpa ' + role + ': vestido');
}
function undress() {
    const p = Main.player[Main.myPlayer];
    for (const [slot, type] of saved) setArmor(p, slot, type);
}

// O outro jogador, como este aparelho o vê.
function remoteDressed(r) {
    const other = Main.player[r];
    return other.armor[0].type === T('ExampleHelmet') && other.armor[3].type === T('ExampleWings');
}
function remoteChecks(r) {
    const other = Main.player[r];
    check('recebe os itens do outro', () =>
        OUTFIT.every(([slot, name]) => other.armor[slot].type === T(name)) ||
        OUTFIT.map(([slot]) => other.armor[slot].type).join(','));
    check('o outro aparece com o fantasia (FrameEffects do jogador remoto)', () =>
        (other.head === EquipLoader.GetEquipSlot('ExampleCostume', EquipType.Head) &&
         other.body === EquipLoader.GetEquipSlot('ExampleCostume', EquipType.Body) &&
         other.legs === EquipLoader.GetEquipSlot('ExampleCostume', EquipType.Legs)) ||
        `head ${other.head}, body ${other.body}, legs ${other.legs}`);
    check('asas e barba do outro', () =>
        (other.wings === sample(T('ExampleWings')).wingSlot && other.beard === sample(T('ExampleBeard')).beardSlot) ||
        `wings ${other.wings}, beard ${other.beard}`);
    check('bônus do conjunto para o jogador remoto', () =>
        (/20/.test(other.setBonus) && other.meleeDamage > 1.15) || `setBonus "${other.setBonus}", melee ${other.meleeDamage}`);
}

// ------------------------------- cliente -------------------------------
let clientSaw = -1;
function clientStep(frames) {
    if (frames === 120) dress();
    if (frames < 130) return;

    const r = remoteIndex();
    if (clientSaw < 0 && r >= 0 && remoteDressed(r)) clientSaw = frames;
    // Espera o host se vestir (ele se veste quando vê o cliente vestido).
    if (clientSaw >= 0 && frames - clientSaw === 30) {
        remoteChecks(r);
        check('o meu conjunto no cliente', () => /20/.test(Main.player[Main.myPlayer].setBonus) || 'setBonus vazio');
    }
    // Uns 5 s vestidos dos dois lados: o desenho do outro jogador roda.
    if (clientSaw >= 0 && frames - clientSaw === 330) { undress(); finish(); }
    if (clientSaw < 0 && frames > 1800) { check('vê o host vestido', () => 'nunca viu'); undress(); finish(); }
}

// -------------------------------- host --------------------------------
let hostStart = -1, hostSaw = -1;
function hostStep(frames) {
    const r = remoteIndex();
    if (r < 0) return;
    if (hostStart < 0) { hostStart = frames; bl.log('mpa host: cliente entrou (' + Main.player[r].name + ')'); }

    if (hostSaw < 0 && remoteDressed(r)) {
        hostSaw = frames;
        dress();
    }
    if (hostSaw >= 0 && frames - hostSaw === 30) remoteChecks(r);
    if (hostSaw >= 0 && frames - hostSaw === 360) { undress(); finish(); }
    if (hostSaw < 0 && frames - hostStart > 1800) { check('vê o cliente vestido', () => 'nunca viu'); finish(); }
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 1) {
        const mode = Main.netMode;
        role = (mode & 2) ? 'host' : mode === 1 ? 'cliente' : '';
        bl.log(`mpa: netMode ${mode}, papel ${role || 'nenhum'}`);
        if (!role) { done = true; return; }
    }
    if (!self.dead) self.statLife = self.statLifeMax2;
    if (role === 'cliente') clientStep(frames);
    else hostStep(frames);
});
bl.log('mpa: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestMparmor extends Mod {}
