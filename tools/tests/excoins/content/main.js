// A tela das moedas do Example Mod (Common/UI/ExampleCoinsUI), no jogo: abre
// pelo /coins, pega moedas (o contador sobe), toca no painel (não usa o item),
// fecha no X e confere que o mesmo toque depois vai para o mundo.
// Loga "excoins ...", TAP/SHOT/UISCALE para o roteiro e "excoins FIM".
const Main = Terraria.Main;
const log = (s) => bl.log('excoins ' + s);
let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + (detail === undefined ? '' : ' (' + detail + ')'));
}
const modErrors = [];
const blError = bl.error;
bl.error = (text) => {
    modErrors.push(String(text).split('\n')[0]);
    return blError(text);
};

const player = () => Main.player[Main.myPlayer];
const caller = { CommandType: CommandType.Chat, get Player() { return player(); }, Reply(text) { log('resposta: ' + text); } };
const NewItem = Terraria.Item['int NewItem(int X, int Y, int Width, int Height, int Type, int Stack, bool noBroadcast, int pfix, NewItemOwnership ownership)'];
const tap = (x, y) => log('TAP ' + x + ' ' + y);
const state = {};

const phases = [
    {
        name: 'abrir', frames: 30,
        start() {
            check('o comando /coins existe e roda', CommandLoader.Handle('/coins', caller) === true);
        },
        // A largura da tela de UI só é conhecida depois do primeiro desenho.
        end() { log('UISCALE ' + UserInterface.ActiveInstance.GetDimensions().Width); },
    },
    {
        name: 'moedas', frames: 90,
        start() {
            const p = player();
            NewItem(Math.floor(p.Center.X), Math.floor(p.Center.Y), 1, 1, Terraria.ID.ItemID.GoldCoin, 3, false, 0, 0);
        },
        end() {
            log('SHOT excoins');
        },
    },
    {
        name: 'painel', frames: 120,
        start() {
            state.maxAnim = 0;
            tap(430, 160);   // dentro do painel (400,100 170x70), fora dos botões
        },
        tick() { state.maxAnim = Math.max(state.maxAnim, player().itemAnimation); },
        end() { check('toque no painel aberto nao usa o item', state.maxAnim === 0, state.maxAnim); },
    },
    {
        name: 'fechar', frames: 120,
        start() { tap(551, 121); },   // o X (140,10 22x22 dentro do painel)
    },
    {
        name: 'fechado', frames: 120,
        start() {
            state.maxAnim = 0;
            tap(430, 160);
        },
        tick() { state.maxAnim = Math.max(state.maxAnim, player().itemAnimation); },
        end() {
            check('depois de fechar, o mesmo toque vai para o mundo', state.maxAnim > 0, state.maxAnim);
            check('nenhum erro de mod', modErrors.length === 0, modErrors.join(' | '));
        },
    },
];

let frame = 0, index = 0, local = 0, done = false;
function step() {
    if (frame < 90) return;
    if (index >= phases.length) {
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        done = true;
        return;
    }
    const p = phases[index];
    if (local === 0) {
        log('fase ' + p.name);
        if (p.start) p.start();
    }
    if (p.tick) p.tick(local);
    local++;
    if (local >= p.frames) {
        if (p.end) p.end();
        index++;
        local = 0;
    }
}

Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (!done && !Main.gameMenu) {
        frame++;
        const p = player();
        p.statLife = p.statLifeMax2;
        try {
            step();
        } catch (e) {
            fails++;
            log('erro no quadro ' + frame + ' (fase ' + (phases[index] ? phases[index].name : '?') + '): ' + e + (e.stack ? ' ' + e.stack : ''));
            index++;
            local = 0;
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestExCoins extends Mod {}
