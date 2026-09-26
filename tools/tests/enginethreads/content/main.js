// O motor JS entre threads (JsSuspend, ScriptEngine.h): o original() de um
// hook solta o motor, e agora qualquer numero de threads pode estar la ao
// mesmo tempo, cada uma com a sua pilha de frames JS.
//
// O Thread.Sleep do .NET ganha um hook que so chama o original(): um Sleep
// chamado do JS vira um original() longo. A thread do jogo dorme 100 ms a
// cada quadro (fica estacionada quase o tempo todo) e, nisso, a thread do
// save da morte do jogador dorme 1,5 s no SaveData deste ModPlayer. Antes, so
// uma thread estacionava por vez: a do save segurava o motor o sono inteiro e
// o jogo parava. Confere que os quadros andaram e que, depois de acordar, a
// pilha JS de cada thread e a dela (Error.stack) e o try/catch funciona.
// Loga "enginethreads ...".
const Main = Terraria.Main;
const Thread = System.Threading.Thread;
const SLEEP = 'void Sleep(int millisecondsTimeout)';
const WORKER_MS = 1500;
const GAME_MS = 100;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('enginethreads ' + label + ': ok');
        else { fails++; bl.log('enginethreads ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('enginethreads ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

Thread[SLEEP].hook((original, ms) => original(ms));

/** A pilha JS depois de voltar do original(), e um throw pego ali. */
function afterWake(where) {
    let caught = false;
    try { throw new Error(where); } catch (e) { caught = e.message === where; }
    return { stack: new Error('pilha').stack || '', caught };
}

let worker = null;   // { frames, stack, caught } depois do sono do save

class ThreadsPlayer extends ModPlayer {
    SaveData(data) {
        if (!worker || worker.done) return;
        // As chamadas do hook na thread do jogo (o `frames`, lido daqui), e nao
        // o GameUpdateCount: com a janela do emulador sem foco o jogo pausa o
        // mundo, mas segue chamando o UpdateAudio. (O Time.frameCount da
        // Unity nao serve: so pode ser lido na thread principal.)
        const from = frames;
        Thread[SLEEP](WORKER_MS);
        worker.frames = frames - from;
        Object.assign(worker, afterWake('save'));
        worker.done = true;
    }
}
ModPlayer.register(ThreadsPlayer);

let frames = 0, gameWake = null, finished = false;
function gameTick() {
    Thread[SLEEP](GAME_MS);
    if (!gameWake) gameWake = afterWake('jogo');
}

Main['void UpdateAudio()'].hook((original, self) => {
    original(self);
    if (Main.gameMenu || finished) return;
    frames++;
    if (worker && !worker.done) gameTick();
    if (frames === 120) {
        worker = { done: false };
        bl.log('enginethreads: matando o jogador (o save dorme ' + WORKER_MS + ' ms, o jogo ' + GAME_MS + ' ms por quadro)');
        gameTick();
        const p = Main.player[Main.myPlayer];
        p['void KillMe(PlayerDeathReason damageSource, double dmg, int hitDirection, bool pvp)'](
            Terraria.DataStructures.PlayerDeathReason.LegacyDefault(), 9999, 0, false);
    }
    if (worker && worker.done) {
        finished = true;
        bl.log('enginethreads quadros do jogo durante o sono do save: ' + worker.frames);
        // ~13 quadros de 100 ms + o quadro; parado, 0 ou 1.
        check('o jogo seguiu enquanto o save dormia no original()', () =>
            worker.frames >= 6 || worker.frames + ' quadro(s) em ' + WORKER_MS + ' ms');
        check('a pilha JS do save voltou inteira', () =>
            (/SaveData/.test(worker.stack) && !/gameTick/.test(worker.stack)) || worker.stack.replace(/\n/g, ' | '));
        check('a pilha JS do jogo voltou inteira', () =>
            (gameWake && /gameTick/.test(gameWake.stack) && !/SaveData/.test(gameWake.stack)) ||
            (gameWake ? gameWake.stack.replace(/\n/g, ' | ') : 'o jogo nao dormiu'));
        check('try/catch depois de acordar, nas duas threads', () =>
            (worker.caught && gameWake && gameWake.caught) || `save ${worker.caught}, jogo ${gameWake && gameWake.caught}`);
        bl.log('enginethreads FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
    if (frames === 1500 && !finished) {
        finished = true;
        bl.log('enginethreads FIM: 1 falha(s) (o save nao chamou o SaveData)');
    }
});
bl.log('enginethreads: carregado');
