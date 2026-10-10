// Entrada fria × quente no JS. No MuMu, a primeira entrada de uma rajada
// custava ~80-100 µs e as seguintes ~2 µs (docs da auditoria de hooks): tirar
// um hook de todo quadro só passava o frio para o próximo. Aqui, com callbacks
// vazios (só original()), três posições no quadro:
//   - isolada: Main.DoUpdate_AnimateCursorColors, uma vez por quadro;
//   - logo depois: Main.DoUpdate_AnimateVisualPlayerAura, chamada em seguida;
//   - rajada: WorldItem.UpdateItem, 400 seguidas por quadro.
// O tempo é o do bl.hookStats (o JS de cada hook, sem o método do jogo). Os
// hooks só entram com a chave 'jsentry.on', ligada durante a medida.
// Só lê: não cria nada no mundo. Loga "jsentry ..." e o resumo no chat.
const Main = Terraria.Main;
const SETTLE = 120, SAMPLES = 300;

const say = (text) => {
    bl.log(text);
    try {
        Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'](text, 255, 240, 120, false);
    } catch (e) {
        bl.log('jsentry chat: ' + e);
    }
};

const TARGETS = [
    ['isolada', 'Main.DoUpdate_AnimateCursorColors', Terraria.Main['void DoUpdate_AnimateCursorColors()']],
    ['logo depois', 'Main.DoUpdate_AnimateVisualPlayerAura', Terraria.Main['void DoUpdate_AnimateVisualPlayerAura()']],
    ['rajada', 'WorldItem.UpdateItem', Terraria.WorldItem['void UpdateItem(int i)']],
];
TARGETS[0][2].hook((original) => original(), { flag: 'jsentry.on' });
TARGETS[1][2].hook((original) => original(), { flag: 'jsentry.on' });
TARGETS[2][2].hook((original, self, i) => original(self, i), { flag: 'jsentry.on' });

function snapshot() {
    const m = new Map();
    for (const h of bl.hookStats()) {
        const t = TARGETS.find(([, name]) => h.name.includes(name));
        // Outro mod pode ter hook no mesmo método: soma os elos.
        if (t) {
            const prev = m.get(t[1]) || { calls: 0, js: 0, jsMs: 0 };
            m.set(t[1], { calls: prev.calls + h.calls, js: prev.js + h.js, jsMs: prev.jsMs + h.jsMs });
        }
    }
    return m;
}

let frames = 0, before = null, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer || Main.gameMenu) return;
    frames++;
    if (frames === SETTLE) {
        bl.hookFlags.set('jsentry.on', true);
        before = snapshot();
    } else if (frames === SETTLE + SAMPLES) {
        bl.hookFlags.set('jsentry.on', false);
        done = true;
        const after = snapshot();
        say(`jsentry: ${SAMPLES} quadros, callbacks vazios`);
        for (const [label, name] of TARGETS) {
            const a = after.get(name), b = before.get(name);
            if (!a || !b) { say(`jsentry ${label} (${name}): sem estatística`); continue; }
            const js = a.js - b.js, ms = a.jsMs - b.jsMs;
            const perFrame = (js / SAMPLES).toFixed(1);
            const us = js > 0 ? (ms * 1000 / js).toFixed(2) : '-';
            say(`jsentry ${label} (${name}): ${perFrame} entradas/quadro, ${us} µs por entrada, ${(ms / SAMPLES).toFixed(3)} ms/quadro`);
        }
        bl.log('jsentry FIM');
    }
});
bl.log('jsentry: carregado');

export default class TestJsEntry extends Mod {}
