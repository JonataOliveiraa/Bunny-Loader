// O sol e a lua do tema de mod (MenuLoader) com o filtro nativo 'menu.sky':
// no mundo o DrawSunAndMoon não entra no JS, e de volta aos menus a chave
// religa no primeiro GUILogo.Draw. Precisa do Example Mod (o tema dele tem sol
// e lua). Entra no mundo, confere, sai pelo SaveAndQuit e confere nos menus.
// Loga "menusky ...".
const Main = Terraria.Main;
let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('menusky ' + label + ': ok');
        else { fails++; bl.log('menusky ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('menusky ' + label + ': FALHOU com ' + e);
    }
}

// Quantas vezes o hook do DrawSunAndMoon chegou ao JS (bl.hookStats).
function skyJs() {
    for (const h of bl.hookStats()) if (h.name.startsWith('Main.DrawSunAndMoon')) return h.js;
    return -1;
}

let frames = 0, inWorld = null, quitting = false, menuFrames = 0, atQuit = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || quitting) return;
    frames++;
    if (frames === 60) inWorld = skyJs();
    if (frames === 180) {
        check('no mundo: chave desligada', () => bl.hookFlags.get('menu.sky') === false || 'ligada');
        check('no mundo: o DrawSunAndMoon não entra no JS', () => {
            const now = skyJs();
            return (now >= 0 && now === inWorld) || `${inWorld} -> ${now} em 120 quadros`;
        });
        atQuit = skyJs();
        quitting = true;
        bl.log('menusky: saindo do mundo (SaveAndQuit)');
        Terraria.WorldGen['void SaveAndQuit()']();
    }
});

// Nos menus o jogo desenha o céu (DrawSunAndMoon) a cada quadro.
GUILogo['void Draw()'].hook((original, self) => {
    original(self);
    if (!quitting || done || !Main.gameMenu) return;
    if (++menuFrames !== 120) return;
    done = true;
    check('de volta ao menu: chave religada', () => bl.hookFlags.get('menu.sky') === true || 'desligada');
    check('de volta ao menu: o DrawSunAndMoon entra no JS', () => {
        const now = skyJs();
        return now > atQuit || `${atQuit} -> ${now}`;
    });
    bl.log('menusky FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});

export default class TestMenuSky extends Mod {}
