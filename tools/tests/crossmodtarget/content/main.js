// O par do tools/tests/crossmod: define um Call e, no topo, chama o do outro
// (o uid dele e menor, entao ele ja carregou). Loga "crossmodtarget ...".
let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('crossmodtarget ' + label + ': ok');
        else { fails++; bl.log('crossmodtarget ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('crossmodtarget ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

let loadResult;
class AlvoMod extends Mod {
    Call(what, ...args) {
        switch (what) {
            case 'soma': return args[0] + args[1];
            case 'devolve': return args[0];
            case 'chama': return args[0](args[1]);
            case 'quem': return bl.mod.id;
            case 'registra': return args[0] instanceof Mod ? args[0].id : 'nao e Mod';
            case 'erro': throw new Error('comando de teste que falha');
            case 'resultadoDaCarga': return loadResult;
        }
        return undefined;
    }
}
Mod.register(AlvoMod);

check('bl.mod no topo', () => (bl.mod.id === 'test-crossmod-alvo' && bl.mod.version === '2.1.0') || bl.mod.id);

check('chamar na carga um mod que ja carregou', () => {
    const r = new Ref();
    if (!ModLoader.TryGetMod('test-crossmod', r)) return 'nao achou';
    loadResult = r.value.Call('quem');
    return loadResult === 'test-crossmod' || String(loadResult);
});

bl.log('crossmodtarget FIM ' + (fails ? fails + ' falha(s)' : 'ok'));
