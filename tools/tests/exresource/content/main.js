// O recurso de exemplo do Example Mod: com a arma do recurso na mão a barra
// aparece (captura), usar a arma gasta o recurso, e o recurso volta sozinho.
// Loga "exresource ...", SHOT e "exresource FIM". Precisa do Example Mod.
const Main = Terraria.Main;
const log = (s) => bl.log('exresource ' + s);
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
const state = {};
const phases = [
    {
        name: 'arma', frames: 120,
        start() {
            const p = player();
            state.slot = p.selectedItem;
            state.saved = p.inventory[state.slot].type;
            p.inventory[state.slot]['void SetDefaults(int Type, ItemVariant variant)'](ModContent.ItemType('ExampleCustomResourceWeapon'), null);
            check('a arma do recurso existe', p.inventory[state.slot].type > 0, p.inventory[state.slot].type);
        },
        tick(t) {
            if (t === 90) log('SHOT recurso');
        },
    },
    {
        name: 'fim', frames: 10,
        end() {
            const p = player();
            p.inventory[state.slot]['void SetDefaults(int Type, ItemVariant variant)'](state.saved, null);
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
            log('erro no quadro ' + frame + ': ' + e + (e.stack ? ' ' + e.stack : ''));
            index++;
            local = 0;
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestExResource extends Mod {}
