// ModPlayer no multijogador: cada jogador com a propria instancia nos DOIS
// aparelhos. O cliente poe o escudo do Example Mod: o dash liga so para ele
// (no cliente e no host), e o host ve o impulso. Instale no host e no
// cliente, com o Example Mod. Loga "mpp <papel> <caso>: ok | FALHOU".
const Main = Terraria.Main;

const resets = new Map();
let forceDash = false, dashVelocity = null;

class MpTestPlayer extends ModPlayer {
    ResetEffects(player) { resets.set(player.whoAmI, (resets.get(player.whoAmI) || 0) + 1); }
    UpdateEquips(player) {
        if (forceDash && player.whoAmI === Main.myPlayer) {
            forceDash = false;
            player.GetModPlayer('ExampleDashPlayer').DashDir = 2;
            this.armed = true;
        }
    }
    // No PostUpdate, depois do quadro inteiro: este mod carrega ANTES do
    // Example Mod (ordem pelo uid), entao o UpdateMovement dele roda antes do dash.
    PostUpdate(player) {
        if (this.armed) { this.armed = false; dashVelocity = player.velocity.X; }
    }
}
ModPlayer.register(MpTestPlayer);

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
const dashOf = (p) => p.GetModPlayer('ExampleDashPlayer');

function shieldType() {
    for (let t = bl.items.vanillaCount; t < bl.items.vanillaCount + 400 && bl.items.isModItem(t); t++) {
        if (['Escudo de Exemplo', 'Example Shield'].includes(Terraria.Lang['string GetItemNameValue(int id)'](t))) return t;
    }
    return -1;
}
function remoteIndex() {
    for (let i = 0; i < 255; i++) if (i !== Main.myPlayer && Main.player[i].active) return i;
    return -1;
}
function setArmor(p, slot, type) {
    p.armor[slot]['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    sendData(5, -1, -1, null, p.whoAmI, ARMOR0 + slot, p.armor[slot].prefix, 0, 0, 0, 0);
}

// ------------------------------- cliente -------------------------------
let savedSlot = 0;
function clientEquip() {
    const p = Main.player[Main.myPlayer];
    savedSlot = p.armor[3].type;
    setArmor(p, 3, shieldType());
    bl.log('mpp cliente: escudo equipado');
}
function clientChecks() {
    const me = Main.player[Main.myPlayer];
    const r = remoteIndex();
    check('o meu escudo liga o MEU dash', () => dashOf(me).DashAccessoryEquipped === true || 'falso');
    check('o host tem instancia propria, sem dash', () => {
        if (r < 0) return 'sem o host na tela';
        const host = Main.player[r];
        const a = dashOf(me), b = dashOf(host);
        return (a !== b && b.Player === host && a.Player === me && b.DashAccessoryEquipped === false) ||
            `mesma ${a === b}, host.Player ${b.Player === host}, dash do host ${b.DashAccessoryEquipped}`;
    });
    check('ganchos rodam para o jogador remoto tambem', () =>
        (resets.get(r) || 0) > 0 || 'ResetEffects do host no cliente: ' + resets.get(r));
    forceDash = true;
}
function clientDash() {
    const me = Main.player[Main.myPlayer];
    check('dash no cliente', () => (dashVelocity >= 10 && me.eocDash > 0) || `vel ${dashVelocity}, eocDash ${me.eocDash}`);
}
function clientRestore() {
    setArmor(Main.player[Main.myPlayer], 3, savedSlot);
    finish();
}

// -------------------------------- host --------------------------------
let started = -1, sawShield = -1, startX = 0, maxMoved = 0;
function hostPoll(frames) {
    const r = remoteIndex();
    if (r < 0) return;
    if (started < 0) { started = frames; bl.log('mpp host: cliente entrou (' + Main.player[r].name + ')'); return; }
    const remote = Main.player[r];
    if (sawShield < 0 && dashOf(remote).DashAccessoryEquipped) {
        sawShield = frames;
        startX = remote.position.X;
        const me = Main.player[Main.myPlayer];
        check('o escudo do cliente liga o dash SO do cliente', () => {
            const a = dashOf(me), b = dashOf(remote);
            return (a !== b && a.DashAccessoryEquipped === false && b.Player === remote && a.Player === me) ||
                `mesma ${a === b}, dash do host ${a.DashAccessoryEquipped}`;
        });
        check('ganchos rodam para o jogador remoto no host', () =>
            (resets.get(r) || 0) > 0 || 'ResetEffects do cliente no host: ' + resets.get(r));
    }
    // A posicao do jogador remoto e o que a rede sincroniza sempre; parado,
    // ele so anda com o dash (~10 px por quadro, caindo).
    if (sawShield >= 0) maxMoved = Math.max(maxMoved, Math.abs(remote.position.X - startX));
    const timeout = frames - started > 1500;
    if ((sawShield >= 0 && maxMoved >= 48 && frames - sawShield > 30) || timeout) {
        if (sawShield < 0) check('o escudo do cliente liga o dash SO do cliente', () => 'o host nunca viu o escudo');
        check('o host ve o dash do cliente (posicao)', () => maxMoved >= 48 || 'andou ' + maxMoved + ' px');
        finish();
    }
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
    if (role === 'cliente') {
        if (frames === 120) clientEquip();
        if (frames === 180) clientChecks();
        if (frames === 186) clientDash();
        if (frames === 260) clientRestore();
    } else {
        hostPoll(frames);
    }
});
bl.log('mpp: carregado');
