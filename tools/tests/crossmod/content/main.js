// Conversa entre mods (ModLoader.TryGetMod + Mod.Call), com o par
// tools/tests/crossmodtarget. O uid deste e menor, entao ele carrega ANTES do
// alvo: no topo, o alvo existe mas o main.js dele ainda nao rodou; no
// PostSetupContent, ja. O alvo, no topo dele, chama este (que ja carregou) e
// guarda o resultado. Loga "crossmod ...".
const TARGET = 'test-crossmod-alvo';
const TARGET_UUID = 'ffc0ffee-7e57-4c0d-9a11-000000000002';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('crossmod ' + label + ': ok');
        else { fails++; bl.log('crossmod ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('crossmod ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

let early = null;
check('TryGetMod de mod que ainda nao carregou', () => {
    const r = new Ref();
    if (!ModLoader.TryGetMod(TARGET, r)) return 'devolveu false';
    if (!(r.value instanceof Mod)) return 'value ' + r.value;
    early = r.value;
    if (early.id !== TARGET || early.uuid !== TARGET_UUID) return early.id + ' ' + early.uuid;
    try {
        early.Call('soma', 1, 2);
        return 'Call nao lancou';
    } catch (e) {
        return String(e).includes('PostSetupContent') || String(e);
    }
});

check('TryGetMod de mod ausente', () => {
    const r = new Ref(123);
    if (ModLoader.TryGetMod('nao-existe', r) !== false) return 'devolveu true';
    return (r.value === null && !ModLoader.HasMod('nao-existe')) || 'value ' + r.value;
});

check('GetMod de mod ausente lanca', () => {
    try {
        ModLoader.GetMod('nao-existe');
        return 'nao lancou';
    } catch (e) {
        return String(e).includes('TryGetMod') || String(e);
    }
});

check('id que nao e texto lanca', () => {
    try {
        ModLoader.HasMod(42);
        return 'nao lancou';
    } catch (e) {
        return e instanceof TypeError || String(e);
    }
});

check('new de um Mod sem register lanca', () => {
    class Solto extends Mod {}
    try {
        new Solto();
        return 'nao lancou';
    } catch (e) {
        return e instanceof TypeError || String(e);
    }
});

let loadRan = false;
let postSetupRan = false;
class CrossMod extends Mod {
    constructor() {
        super();
        this.calls = 0;
    }
    Load() { loadRan = true; }
    PostSetupContent() { postSetupRan = true; }
    Call(what) {
        this.calls++;
        if (what === 'quem') return bl.mod.id;
        return undefined;
    }
}
const me = Mod.register(CrossMod);

check('Mod.register e bl.mod', () => {
    if (me !== bl.mod) return 'register devolveu outro objeto';
    if (!(me instanceof CrossMod) || !(me instanceof Mod)) return 'classe';
    if (me.id !== 'test-crossmod' || me.version !== '1.0.0') return me.id + ' ' + me.version;
    if (me.name !== 'Teste: conversa entre mods') return 'name ' + me.name;
    if (ModLoader.GetMod('test-crossmod') !== me || !ModLoader.HasMod('test-crossmod')) return 'GetMod/HasMod';
    if (me.calls !== 0) return 'campo do construtor ' + me.calls;
    return loadRan || 'Load nao rodou';
});

check('Mod.register duas vezes lanca', () => {
    class Outro extends Mod {}
    try {
        Mod.register(Outro);
        return 'nao lancou';
    } catch (e) {
        if (!String(e).includes('um Mod por pacote')) return String(e);
        return (bl.mod instanceof CrossMod && !(bl.mod instanceof Outro)) || 'o Mod mudou de classe';
    }
});

check('pelo uuid', () => ModLoader.GetMod(TARGET_UUID) === early || 'outro objeto');

check('ModLoader nao aceita troca', () => {
    try {
        ModLoader.TryGetMod = () => true;
        return 'aceitou';
    } catch (e) {
        return e instanceof TypeError || String(e);
    }
});

class Checks extends ModSystem {
    PostSetupContent() {
        const r = new Ref();
        check('TryGetMod no PostSetupContent', () => ModLoader.TryGetMod(TARGET, r) || 'false');
        const alvo = r.value;
        check('o mesmo objeto de antes da carga, agora da classe do alvo', () =>
            (alvo === early && alvo instanceof Mod && alvo.constructor.name === 'AlvoMod') ||
            String(alvo && alvo.constructor.name));
        check('Call com numeros', () => alvo.Call('soma', 2, 3) === 5 || 'soma');
        check('Call com objeto e funcao', () => {
            const obj = { n: 1 };
            if (alvo.Call('devolve', obj) !== obj) return 'objeto copiado';
            return alvo.Call('chama', (x) => x * 2, 21) === 42 || 'funcao';
        });
        check('bl.mod dentro do Call e o alvo', () => alvo.Call('quem') === TARGET || String(alvo.Call('quem')));
        check('o alvo recebe o nosso Mod', () => alvo.Call('registra', bl.mod) === 'test-crossmod' || 'registra');
        check('erro no Call do alvo sobe para quem chamou', () => {
            try {
                alvo.Call('erro');
                return 'nao lancou';
            } catch (e) {
                return String(e).includes('comando de teste') || String(e);
            }
        });
        check('comando que o alvo nao conhece', () => alvo.Call('nada') === undefined || 'devolveu algo');
        check('o alvo chamou este na carga dele', () =>
            (alvo.Call('resultadoDaCarga') === 'test-crossmod' && me.calls === 1) ||
            String(alvo.Call('resultadoDaCarga')) + ' calls ' + me.calls);
        check('ModLoader.Mods', () => {
            const ids = ModLoader.Mods.map((m) => m.id);
            const a = ids.indexOf('test-crossmod');
            const b = ids.indexOf(TARGET);
            return (a >= 0 && b > a) || ids.join(',');
        });
        check('PostSetupContent do Mod', () => postSetupRan || 'nao rodou');
        check('dataDirectory', () => bl.directory.exists(me.dataDirectory) || me.dataDirectory);
        bl.log('crossmod FIM ' + (fails ? fails + ' falha(s)' : 'ok'));
    }
}
ModSystem.register(Checks);
