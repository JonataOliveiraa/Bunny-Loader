const label = 'B';
const state = globalThis.__loadOrderTest || (globalThis.__loadOrderTest = {
    loaded: [], before: [], after: [], done: false,
});
state.loaded.push(label);
bl.log('loadorder LOAD ' + label);
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    const capture = !state.done && !Terraria.Main.gameMenu && i === Terraria.Main.myPlayer && self.active;
    if (capture) state.before.push(label);
    original(self, i);
    if (!capture) return;
    state.after.push(label);
    if (label !== state.loaded[0]) return;
    state.done = true;
    const valid = state.loaded.length === 4 &&
        state.before.join('') === state.loaded.join('') &&
        state.after.join('') === state.loaded.slice().reverse().join('');
    bl.log('loadorder FIM: ' + (valid ? 'ok' : 'FALHOU') +
        ' LOAD=' + state.loaded.join('>') + ' BEFORE=' + state.before.join('>') +
        ' AFTER=' + state.after.join('>'));
});
export default class LoadOrderBMod extends Mod {}
