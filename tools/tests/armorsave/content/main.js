// Armadura de mod no save do personagem (content/items/ModItemSave.cpp). Cada
// rodada num processo novo (tools/bench/run.sh reabre o jogo):
//   1a: veste o capacete, as asas e o fantasia no slot de vaidade e salva;
//   2a: confere que voltaram no lugar e que o jogo desenha os slots de mod,
//       tira tudo e salva de novo (o personagem de teste fica como estava).
// Precisa do Example Mod ligado. Loga "armorsave <caso>: ok | FALHOU".
const Main = Terraria.Main;
const T = (name) => ModContent.ItemType(name);
const sample = (type) => Terraria.ID.ContentSamples.ItemsByType.get_Item(type);

// Casa da armadura -> item.
const OUTFIT = [[0, 'ExampleHelmet'], [3, 'ExampleWings'], [13, 'ExampleCostume']];

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('armorsave ' + label + ': ok');
        else { fails++; bl.log('armorsave ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('armorsave ' + label + ': FALHOU com ' + e);
    }
}

function setArmor(p, slot, type) {
    p.armor[slot]['void SetDefaults(int Type, ItemVariant variant)'](type, null);
}

function savePlayer() {
    Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](
        Main.ActivePlayerFileData, false, true);
}

let secondRound = false;
function start() {
    const p = Main.player[Main.myPlayer];
    secondRound = p.armor[0].type === T('ExampleHelmet');
    if (secondRound) return;

    for (const [slot, name] of OUTFIT) setArmor(p, slot, T(name));
    savePlayer();
    bl.log('armorsave FIM: vestido e salvo (1a rodada); rode de novo');
}

// Uns quadros depois de entrar: o PlayerFrame já montou os slots desenhados.
function verify() {
    const p = Main.player[Main.myPlayer];
    check('as peças voltaram no lugar', () =>
        OUTFIT.every(([slot, name]) => p.armor[slot].type === T(name)) ||
        OUTFIT.map(([slot]) => slot + '=' + p.armor[slot].type).join(', '));
    check('o jogo desenha os slots de mod depois de carregar', () =>
        (p.wings === sample(T('ExampleWings')).wingSlot &&
         p.head === EquipLoader.GetEquipSlot('ExampleCostume', EquipType.Head)) ||
        `wings ${p.wings}, head ${p.head}`);

    for (const [slot] of OUTFIT) setArmor(p, slot, 0);
    savePlayer();
    bl.log('armorsave FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (!self.dead) self.statLife = self.statLifeMax2;
    ++frames;
    if (frames === 60) {
        start();
        if (!secondRound) done = true;
    }
    if (frames === 120 && secondRound) { verify(); done = true; }
});
bl.log('armorsave: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class ArmorSaveTestMod extends Mod {}
