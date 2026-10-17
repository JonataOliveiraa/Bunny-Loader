// ModResourceOverlay no celular (GUIHearts/GUIMana): o que chega em cada
// desenho, o Draw() por cima (magenta no coração 0), o Pre que esconde o
// coração 1, e o drawText = false da vida. Loga "resoverlay ..." e FIM.
const Main = Terraria.Main;
const log = (s) => bl.log('resoverlay ' + s);
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

const seen = { life: new Map(), mana: new Map(), pre: 0, post: 0, displayPre: 0, displayPost: 0, sets: new Set(), maxLife: -1, maxMana: -1, snapshot: null };
let magenta = null;
const name = (t) => {
    try {
        return bl.isTextureAsset(t) ? t.Name : 'Texture2D ' + t.Width + 'x' + t.Height;
    } catch (e) {
        return '?';
    }
};

export class TestOverlay extends ModResourceOverlay {
    PreDrawResource(context) {
        seen.pre++;
        seen.sets.add(context.DisplaySet.Name);
        const map = context.drawingLife ? seen.life : seen.mana;
        const key = name(context.texture) + (context.source ? ' src ' + context.source.Width + 'x' + context.source.Height : '');
        map.set(key, (map.get(key) || 0) + 1);
        if (context.drawingLife) seen.maxLife = Math.max(seen.maxLife, context.resourceNumber);
        else seen.maxMana = Math.max(seen.maxMana, context.resourceNumber);
        seen.snapshot = context.snapshot;
        // O coração 1 some (todos os desenhos dele).
        return !(context.drawingLife && context.resourceNumber === 1);
    }

    PostDrawResource(context) {
        seen.post++;
        if (context.drawingLife && context.resourceNumber === 0 && context.source) {
            magenta ??= ModContent.Request('Magenta');
            context.texture = magenta;
            context.source = Rectangle.new(0, 0, 8, 8);
            context.color = Color.White;
            context.Draw();
        }
    }

    PreDrawResourceDisplay(snapshot, displaySet, drawingLife, textColor, drawText) {
        seen.displayPre++;
        if (drawingLife) drawText.value = false;
        return true;
    }

    PostDrawResourceDisplay() {
        seen.displayPost++;
    }
}

let frame = 0, done = false;
Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    original(self, gameTime);
    if (done || Main.gameMenu) return;
    frame++;
    const p = Main.player[Main.myPlayer];
    p.statLife = p.statLifeMax2;
    if (frame === 150) log('SHOT overlay');
    if (frame !== 240) return;
    done = true;
    try {
        log('vida: ' + [...seen.life].map(([k, v]) => k + ' x' + v).join(' | '));
        log('mana: ' + [...seen.mana].map(([k, v]) => k + ' x' + v).join(' | '));
        log('conjuntos: ' + [...seen.sets].join(',') + ' snapshot ' + JSON.stringify(seen.snapshot));
        check('coracoes passam pelo overlay', seen.life.size > 0 && seen.maxLife >= 4, 'ultimo indice ' + seen.maxLife);
        check('estrelas passam pelo overlay', seen.mana.size > 0, 'ultimo indice ' + seen.maxMana);
        check('Pre e Post para cada desenho', seen.pre === seen.post && seen.pre > 0, seen.pre + '/' + seen.post);
        check('PreDrawResourceDisplay e Post (vida e mana)', seen.displayPre > 0 && seen.displayPre === seen.displayPost, seen.displayPre + '/' + seen.displayPost);
        check('snapshot com os coracoes', seen.snapshot && seen.snapshot.AmountOfLifeHearts === Math.trunc(p.statLifeMax2 / 20), seen.snapshot && seen.snapshot.AmountOfLifeHearts);
        check('nenhum erro de mod', modErrors.length === 0, modErrors.join(' | '));
    } catch (e) {
        fails++;
        log('erro: ' + e + ' ' + e.stack);
    }
    log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
log('carregado');

export default class TestResOverlay extends Mod {}
