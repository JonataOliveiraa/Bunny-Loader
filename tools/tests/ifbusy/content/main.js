// Hook com ifBusy (docs/mods/01): com o motor JS preso noutra thread, o
// 'skip' nao roda nada (fica valendo o quadro anterior) e o 'original' roda
// so o metodo do jogo, sem esperar — a thread do jogo segue.
//
// Prende o motor de proposito: o SaveData deste ModPlayer gira 1,5 s na
// thread do save que a morte do jogador dispara. Enquanto isso, confere (da
// propria thread do save) que os quadros andaram e que o newMusic ficou com a
// marca que o hook 'skip' poe. Rodar SEM o Example Mod: os hooks de todo
// quadro dele, sem ifBusy, esperariam o motor e parariam o jogo.
// Loga "ifbusy ...".
const Main = Terraria.Main;
const MARK = Terraria.ID.MusicID.Boss2;
const SPIN_MS = 1500;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('ifbusy ' + label + ': ok');
        else { fails++; bl.log('ifbusy ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('ifbusy ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

check("'skip' fora de metodo void e recusado", () => {
    try {
        Terraria.Projectile['bool CanCutTiles()'].hook((original, self) => original(self), { ifBusy: 'skip' });
    } catch (e) {
        return /void/.test(String(e)) || String(e);
    }
    return 'aceitou';
});
check('ifBusy desconhecido e recusado', () => {
    try {
        Main['void UpdateAudio_DecideOnTOWMusic()'].hook((original, self) => original(self), { ifBusy: 'depois' });
    } catch (e) {
        return /ifBusy/.test(String(e)) || String(e);
    }
    return 'aceitou';
});

let spin = null;   // { frames, musicKept } depois do giro

class BusyPlayer extends ModPlayer {
    SaveData(data) {
        if (!spin || spin.done) return;
        // Na thread do save, com o motor JS na mao.
        const from = Main.GameUpdateCount;
        const end = Date.now() + SPIN_MS;
        let kept = true;
        while (Date.now() < end) {
            if (Main.newMusic !== MARK) kept = false;
        }
        spin.frames = Main.GameUpdateCount - from;
        spin.musicKept = kept;
        spin.done = true;
    }
}
ModPlayer.register(BusyPlayer);

Main['void UpdateAudio_DecideOnNewMusic()'].hook((original, self) => {
    original(self);
    if (spin) Main.newMusic = MARK;
}, { ifBusy: 'skip' });

let frames = 0, finished = false;
Main['void UpdateAudio()'].hook((original, self) => {
    original(self);
    if (Main.gameMenu || finished) return;
    frames++;
    if (frames === 120) {
        spin = { done: false };
        bl.log('ifbusy: matando o jogador (o save gira ' + SPIN_MS + ' ms com o motor)');
        const p = Main.player[Main.myPlayer];
        p['void KillMe(PlayerDeathReason damageSource, double dmg, int hitDirection, bool pvp)'](
            Terraria.DataStructures.PlayerDeathReason.LegacyDefault(), 9999, 0, false);
    }
    if (spin && spin.done) {
        finished = true;
        // ~90 quadros em 1,5 s; parado, seriam 0 ou 1.
        check("'original': a thread do jogo seguiu com o motor preso", () =>
            spin.frames >= 20 || spin.frames + ' quadro(s) em ' + SPIN_MS + ' ms');
        check("'skip': o newMusic ficou com o que o hook pos", () =>
            spin.musicKept || 'o jogo decidiu a musica durante o giro');
        bl.log('ifbusy FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
    if (frames === 1200 && !finished) {
        finished = true;
        bl.log('ifbusy FIM: 1 falha(s) (o save nao chamou o SaveData)');
    }
}, { ifBusy: 'original' });
bl.log('ifbusy: carregado');
