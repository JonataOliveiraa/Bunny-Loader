// Teste da ancora dos wrappers: um objeto do jogo que SO o JS segura tem de
// sobreviver ao coletor do IL2CPP. Se a ancora falhar, o coletor recolhe o
// objeto, a memoria e reusada por outros Items e os campos mudam (ou o jogo
// cai). Loga "wrappers <caso>: ok | FALHOU".
const Main = Terraria.Main;
const Item = Terraria.Item;
const collect = System.GC['void Collect(int generation)'];
const SetDefaults = Item['void SetDefaults(int Type, ItemVariant variant)'];

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('wrappers ' + label + ': ok');
        else { fails++; bl.log('wrappers ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('wrappers ' + label + ': FALHOU com ' + e);
    }
}
function info(label, v) { bl.log('wrappers ' + label + ': ' + v); }

function newItem(type) {
    const it = Item.new();
    it['void .ctor()']();
    SetDefaults(it, type, null);
    return it;
}

/** Forca coletas e enche o heap de Items novos, para reusar memoria solta. */
function churn() {
    for (let r = 0; r < 3; r++) {
        collect(2);
        for (let i = 0; i < 400; i++) newItem(1);   // lixo: sai do escopo na hora
    }
    collect(2);
}

// Itens que so o JS segura, marcados com um dano unico.
const kept = [];
// Itens que so o HOOK guardou (o JS que os criou ja os soltou).
const escaped = [];
let catching = false;

SetDefaults.hook((o, self, type, variant) => {
    o(self, type, variant);
    if (catching) escaped.push(self);
});

function run() {
    check('criar 200 itens so no JS', () => {
        for (let i = 0; i < 200; i++) {
            const it = newItem(4);          // Espada de Ferro
            it.damage = 1000 + i;
            kept.push(it);
        }
    });
    check('self guardado pelo hook', () => {
        catching = true;
        for (let i = 0; i < 50; i++) newItem(3507);   // o JS solta; so o hook guarda
        catching = false;
        return escaped.length === 50 || 'guardados=' + escaped.length;
    });
    check('array so no JS', () => {
        const arr = System.Array['Array CreateInstance(Type elementType, int length)'](kept[0]['Type GetType()'](), 3);
        arr[0] = kept[0]; arr[2] = kept[1];
        kept.arr = arr;
    });

    churn();

    check('200 itens intactos depois do coletor', () => {
        for (let i = 0; i < kept.length; i++) {
            const it = kept[i];
            if (it.damage !== 1000 + i || it.type !== 4) return 'item ' + i + ': dano=' + it.damage + ' tipo=' + it.type;
        }
    });
    check('itens guardados pelo hook intactos', () => {
        for (let i = 0; i < escaped.length; i++) {
            if (escaped[i].type !== 3507) return 'item ' + i + ': tipo=' + escaped[i].type;
        }
    });
    check('array intacto', () => (kept.arr.length === 3 && kept.arr[0].damage === 1000 && kept.arr[1] === null && kept.arr[2].damage === 1001) || 'errado');
    check('metodo num item sobrevivente', () => {
        const c = kept[5]['Item Clone()']();
        return (c.damage === 1005 && c.type === 4) || 'dano=' + c.damage;
    });

    // Mapa de wrappers sob estresse: 400 objetos distintos, criados e soltos
    // em sequencia, varias voltas. Se a tabela devolver o wrapper de outro
    // objeto (ou um ja destruido), whoAmI nao bate — ou o jogo cai.
    check('mapa de wrappers: 8 voltas em Main.item', () => {
        const items = Main.item;
        for (let volta = 0; volta < 8; volta++) {
            for (let i = 0; i < items.length; i++) {
                const w = items[i];
                if (w.whoAmI !== i) return 'volta ' + volta + ' item ' + i + ': whoAmI=' + w.whoAmI;
                if (i % 7 === 0 && items[i] !== w) return 'identidade perdida no item ' + i;
            }
        }
    });

    // Identidade: so vale a partir do passo 2 (um wrapper por objeto).
    info('identidade Main.player[0] === Main.player[0]', Main.player[0] === Main.player[0]);
    info('identidade kept[0] === array[0]', kept[0] === kept.arr[0]);

    bl.log('wrappers FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer || Main.gameMenu) return;
    done = true;
    run();
});
bl.log('wrappers: carregado');
