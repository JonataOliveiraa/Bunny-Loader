// ModBuff.ModifyBuffText: o nome (Lang.GetBuffName, que o GUIBuffs desenha) e
// a dica (Main.GetBuffTooltip, que o GUIBuffs mostra ao tocar) saem mudados;
// um buff de mod sem o método e um do jogo ficam como estão.
// Loga "bufftext <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { BuffID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('bufftext ' + label + ': ok');
        else { fails++; bl.log('bufftext ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('bufftext ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

let stacks = 3;
const received = [];

export class GlowBuff extends ModBuff {
    DisplayName = 'Brilho';
    Description = 'Brilha';
    Texture = 'Gem';
    ModifyBuffText(buffName, tip, rare) {
        received.push(`${buffName.value}|${tip.value}|${rare.value}`);
        buffName.value += ' x' + stacks;
        tip.value = 'Brilha ' + stacks + ' vezes';
        rare.value = 2;
    }
}

export class PlainBuff extends ModBuff {
    DisplayName = 'Simples';
    Description = 'Nada demais';
    Texture = 'Gem';
}

const nameOf = (type) => Terraria.Lang['string GetBuffName(int id)'](type);
const tipOf = (type) => Main['string GetBuffTooltip(Player player, int buffType)'](Main.player[Main.myPlayer], type);

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames !== 30) return;

    const glow = ModContent.BuffType(GlowBuff), plain = ModContent.BuffType(PlainBuff);
    self['void AddBuff(int type, int time, bool fromNetPvP)'](glow, 600, false);
    check('o nome sai do ModifyBuffText', () => nameOf(glow) === 'Brilho x3' || nameOf(glow));
    check('a dica sai do ModifyBuffText', () => tipOf(glow) === 'Brilha 3 vezes' || tipOf(glow));
    check('o método recebe o nome, a dica e a raridade do jogo', () =>
        received.includes('Brilho|Brilha|0') || JSON.stringify(received.slice(0, 4)));
    stacks = 5;
    check('chamado de novo a cada pedido', () => (nameOf(glow) === 'Brilho x5' && tipOf(glow) === 'Brilha 5 vezes') || `${nameOf(glow)} / ${tipOf(glow)}`);
    check('buff de mod sem o método: como está', () => (nameOf(plain) === 'Simples' && tipOf(plain) === 'Nada demais') || `${nameOf(plain)} / ${tipOf(plain)}`);
    check('buff do jogo: como está', () => nameOf(BuffID.Regeneration).length > 0 || 'vazio');

    const buffs = self.buffType;
    for (let k = 0; k < buffs.length; k++) if (buffs[k] === glow) self['void DelBuff(int b)'](k);
    done = true;
    bl.log('bufftext FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('bufftext: carregado');

export default class TestBuffText extends Mod {}
