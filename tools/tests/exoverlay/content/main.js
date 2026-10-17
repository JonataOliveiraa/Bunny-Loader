// As frutas e cristais de exemplo do Example Mod: a vida e a mana máximas
// sobem (ModifyMaxStats) e os corações/estrelas ganham o desenho dos
// VanillaLifeOverlay/VanillaManaOverlay (captura). Loga "exoverlay ..." e FIM.
const Main = Terraria.Main;
const log = (s) => bl.log('exoverlay ' + s);
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

let frame = 0, done = false;
const state = {};
Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    original(self, gameTime);
    if (done || Main.gameMenu) return;
    frame++;
    const p = Main.player[Main.myPlayer];
    try {
        const stats = p.GetModPlayer('ExampleStatIncreasePlayer');
        if (frame === 90) {
            state.life = p.statLifeMax2;
            state.mana = p.statManaMax2;
            stats.exampleLifeFruits = 2;
            stats.exampleManaCrystals = 1;
        }
        if (frame === 150) {
            check('2 frutas: +20 de vida maxima', p.statLifeMax2 === state.life + 20, state.life + ' -> ' + p.statLifeMax2);
            check('1 cristal: +10 de mana maxima', p.statManaMax2 === state.mana + 10, state.mana + ' -> ' + p.statManaMax2);
            p.statLife = p.statLifeMax2;
            p.statMana = p.statManaMax2;
        }
        if (frame === 170) log('SHOT overlay');
        if (frame === 260) {
            stats.exampleLifeFruits = 0;
            stats.exampleManaCrystals = 0;
            check('nenhum erro de mod', modErrors.length === 0, modErrors.join(' | '));
            log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
            done = true;
        }
    } catch (e) {
        fails++;
        log('erro: ' + e + ' ' + e.stack);
        log('FIM: erro');
        done = true;
    }
});
log('carregado');

export default class TestExOverlay extends Mod {}
