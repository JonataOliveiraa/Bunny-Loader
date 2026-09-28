// Etapa B4 do ModBiome (docs/local/PLANO-MODBIOME.md): as flags na rede.
// Loga "mpb <papel> <caso>".
//
// Host: liga o NetBiome no primeiro quadro, antes de o cliente existir; então
// o cliente só fica sabendo pela sincronização de quem entra (SyncOnePlayer).
// Depois olha o jogador do cliente: o NetBiome dele liga e desliga (é o que o
// spawn do servidor lê). O OtherBiome ninguém liga.
//
// Cliente: confere o bioma do host ao entrar, liga o próprio NetBiome 180
// quadros depois e desliga 240 depois disso. O host desliga o dele 300 quadros
// depois de ver o do cliente desligado, e o cliente confere que chegou.
//
// Nos dois: o OnEnter/OnLeave é só do jogador daqui.
const Main = Terraria.Main;

let role = '', fails = 0, done = false;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mpb ' + role + ' ' + label + ': ok');
        else { fails++; bl.log('mpb ' + role + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mpb ' + role + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
function finish() {
    done = true;
    NetBiome.on = false;
    check('OnEnter/OnLeave só do jogador daqui', () => remoteCalls === 0 || remoteCalls + ' chamada(s) com jogador remoto');
    bl.log('mpb ' + role + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let remoteCalls = 0;
const mine = (player) => { if (player.whoAmI !== Main.myPlayer) remoteCalls++; };

export class OtherBiome extends ModBiome {
    SetStaticDefaults() { this.Music = -1; }
    IsBiomeActive(player) { return false; }
}

export class NetBiome extends ModBiome {
    static on = false;
    SetStaticDefaults() { this.Music = -1; }
    IsBiomeActive(player) { return NetBiome.on; }
    OnEnter(player) { mine(player); }
    OnLeave(player) { mine(player); }
}

function remoteIndex() {
    for (let i = 0; i < 255; i++) if (i !== Main.myPlayer && Main.player[i].active) return i;
    return -1;
}

let joined = -1, sawOn = -1, sawOff = -1;
function hostTick(frames, self) {
    if (frames === 1) NetBiome.on = true;
    const r = remoteIndex();
    if (r < 0) return;
    const remote = Main.player[r];
    if (joined < 0) {
        joined = frames;
        bl.log('mpb host: cliente entrou (' + remote.name + ', índice ' + r + ')');
    }
    const on = remote.InModBiome(NetBiome);
    if (sawOn < 0 && on) sawOn = frames;
    if (sawOn >= 0 && sawOff < 0 && !on) sawOff = frames;
    if (remote.InModBiome(OtherBiome)) check('o OtherBiome do cliente continua desligado', () => 'ligou');

    if ((sawOff >= 0 && frames - sawOff === 300) || frames - joined > 3000) {
        check('o NetBiome do cliente chegou ligado', () => sawOn >= 0 || 'nunca');
        check('e chegou desligado', () => sawOff >= 0 || 'nunca');
        check('as flags do host não mudaram pelo que veio do cliente', () =>
            (self.InModBiome(NetBiome) && !self.InModBiome(OtherBiome)) || 'NetBiome ' + self.InModBiome(NetBiome));
        finish();
    }
}

let hostSeen = false;
function clientTick(frames, self) {
    const r = remoteIndex();
    if (r < 0) return;
    const host = Main.player[r];
    if (joined < 0) {
        joined = frames;
        bl.log('mpb cliente: no mundo com ' + host.name + ' (índice ' + r + ')');
    }
    const since = frames - joined;
    if (!hostSeen && host.InModBiome(NetBiome)) {
        hostSeen = true;
        check('o bioma do host chegou na entrada (' + since + ' quadros)', () => !host.InModBiome(OtherBiome) || 'OtherBiome ligado');
    }
    if (since === 180) {
        check('o bioma do host chegou na entrada', () => hostSeen || 'depois de 180 quadros, nada');
        NetBiome.on = true;
    }
    if (since === 181) check('o próprio NetBiome ligado', () => self.InModBiome(NetBiome) || 'desligado');
    if (since === 420) NetBiome.on = false;
    if (since === 600) {
        check('o próprio NetBiome desligado e o do host ainda ligado', () =>
            (!self.InModBiome(NetBiome) && host.InModBiome(NetBiome)) || `${self.InModBiome(NetBiome)} ${host.InModBiome(NetBiome)}`);
    }
    if (since > 600 && !host.InModBiome(NetBiome)) {
        check('o host desligou o dele e chegou (' + since + ' quadros)', () => true);
        finish();
    } else if (since === 1500) {
        check('o host desligou o dele e chegou', () => 'ainda ligado');
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
        bl.log(`mpb: netMode ${mode}, papel ${role || 'nenhum'}`);
    }
    if (!role || done) return;
    if (role === 'cliente') clientTick(frames, self);
    else hostTick(frames, self);
});
bl.log('mpb: carregado');

export default class TestMpbiome extends Mod {}
