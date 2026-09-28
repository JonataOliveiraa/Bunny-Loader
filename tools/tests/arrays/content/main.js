// Arrays do jogo criados pelo JS:
//   - lista.makeGeneric(tipo), como no TL Pro: o tipo pelo nome do C# ('int',
//     'byte', 'bool', 'string'...), por nome completo ('Terraria.Item'), pela
//     classe (Terraria.Item) ou por um ajudante com `.Type` (Vector2);
//   - num TypedArray do mesmo tipo (Uint8Array -> byte[]), numa cópia só;
//   - Classe.newArray(n) e Classe.newArray([valores]);
//   - o array sai do jogo de verdade (System.Array.Reverse mexe nele);
//   - erros: valor que não converte (diz a posição), tipo que não existe;
//   - makeGeneric não aparece no for-in dos arrays JS.
// Loga "arrays <caso>: ok | FALHOU".
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('arrays ' + label + ': ok');
        else { fails++; bl.log('arrays ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('arrays ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
const throws = (fn, part) => {
    try { fn(); } catch (e) { return String(e).includes(part) || 'erro sem "' + part + '": ' + e; }
    return 'não lançou';
};
const list = (arr) => Array.from({ length: arr.length }, (_, i) => arr[i]).join(',');

function run() {
    check("[1, 2, 3].makeGeneric('int')", () => {
        const a = [1, 2, 3].makeGeneric('int');
        return (String(a) === 'Int32[3]' && list(a) === '1,2,3') || String(a) + ' ' + list(a);
    });
    check("Uint8Array.makeGeneric('byte') (cópia direta)", () => {
        const a = new Uint8Array([1, 2, 255, 0]).makeGeneric('byte');
        return (String(a) === 'Byte[4]' && list(a) === '1,2,255,0') || String(a) + ' ' + list(a);
    });
    check('Uint8Array com deslocamento (subarray)', () => {
        const a = new Uint8Array([9, 8, 7, 6]).subarray(1, 3).makeGeneric('byte');
        return list(a) === '8,7' || list(a);
    });
    check("Float32Array.makeGeneric('float') e Int16Array.makeGeneric('int') (valor por valor)", () => {
        const f = new Float32Array([1.5, -2]).makeGeneric('float');
        const i = new Int16Array([-3, 4]).makeGeneric('int');
        return (list(f) === '1.5,-2' && String(i) === 'Int32[2]' && list(i) === '-3,4') || list(f) + ' ' + list(i);
    });
    check("'bool', 'short', 'ushort', 'double', 'long'", () => {
        const b = [true, false, 1].makeGeneric('bool');
        const s = [-1, 2].makeGeneric('short');
        const u = [65535].makeGeneric('ushort');
        const d = [0.25].makeGeneric('double');
        const l = [1234567890123].makeGeneric('long');
        return (list(b) === 'true,false,true' && list(s) === '-1,2' && u[0] === 65535 && d[0] === 0.25 && l[0] === 1234567890123) ||
            [list(b), list(s), u[0], d[0], l[0]].join(' | ');
    });
    check("['a', 'b'].makeGeneric('string')", () => {
        const a = ['a', 'b', null].makeGeneric('string');
        return (String(a) === 'String[3]' && a[1] === 'b' && a[2] === null) || String(a) + ' ' + list(a);
    });
    check('structs: pelo ajudante Vector2 e pela classe do jogo', () => {
        const a = [Vector2.new(1, 2), Vector2.new(3, 4)].makeGeneric(Vector2);
        const b = [Vector2.new(5, 6)].makeGeneric(Microsoft.Xna.Framework.Vector2);
        return (a[0].Y === 2 && a[1].X === 3 && b[0].X === 5) || `${a[0].Y} ${a[1].X} ${b[0].X}`;
    });
    check("objetos: pela classe e por 'Terraria.Item'", () => {
        const item = Main.LocalPlayer.inventory[0];   // Item (o Main.item do celular é WorldItem)
        const a = [item, null].makeGeneric(Terraria.Item);
        const b = [item].makeGeneric('Terraria.Item');
        return (String(a) === 'Item[2]' && a[0] === item && a[1] === null && b[0] === item) || String(a) + ' ' + String(b);
    });
    check("[].makeGeneric('byte').cloneResized(4)", () => {
        const a = [].makeGeneric('byte').cloneResized(4);
        return (a.length === 4 && list(a) === '0,0,0,0') || list(a);
    });
    check('newArray(n): Item[3] nulo, int[2] zerado, Vector2[2] zerado', () => {
        const items = Terraria.Item.newArray(3);
        const ints = System.Int32.newArray(2);
        const vs = Microsoft.Xna.Framework.Vector2.newArray(2);
        return (String(items) === 'Item[3]' && items[2] === null && list(ints) === '0,0' && vs[1].X === 0) ||
            `${items} ${list(ints)} ${vs[1].X}`;
    });
    check('newArray([valores])', () => {
        const a = System.Int32.newArray([4, 5, 6]);
        return (String(a) === 'Int32[3]' && list(a) === '4,5,6') || list(a);
    });
    check('é um array do jogo: System.Array.Reverse mexe nele', () => {
        const a = [1, 2, 3].makeGeneric('int');
        System.Array['void Reverse(Array array)'](a);
        return list(a) === '3,2,1' || list(a);
    });
    check('escrever depois: arr[i] = v', () => {
        const a = [0, 0].makeGeneric('int');
        a[1] = 7;
        return a[1] === 7 || a[1];
    });
    check('valor que não converte: o erro diz a posição', () => throws(() => [1, 'x'].makeGeneric('int'), 'posicao 1'));
    check('tipo que não existe', () => throws(() => [1].makeGeneric('nada'), "tipo 'nada'"));
    check('sem tipo', () => throws(() => [1].makeGeneric(), 'tipo do elemento'));
    check('newArray com tamanho inválido', () => throws(() => System.Int32.newArray(-1), 'invalido'));
    check('makeGeneric fora do for-in e do Object.keys', () => {
        const seen = [];
        for (const k in [10, 20]) seen.push(k);
        return (seen.join(',') === '0,1' && Object.keys([10]).join(',') === '0') || seen.join(',');
    });
    // ---- fill, empty, find ----
    check('fill(v): todas as posições, e devolve o mesmo array', () => {
        const a = System.Int32.newArray(5);
        const r = a.fill(7);
        return (r === a && list(a) === '7,7,7,7,7') || list(a);
    });
    check('fill(v, início, fim) e índice negativo, como no JS', () => {
        const a = System.Int32.newArray(5).fill(7);
        a.fill(1, 1, 3);
        a.fill(2, -2);
        return list(a) === '7,1,1,2,2' || list(a);
    });
    check('fill de struct, de objeto, de null e de texto', () => {
        const vs = Microsoft.Xna.Framework.Vector2.newArray(3).fill(Vector2.new(1, 2));
        const item = Main.LocalPlayer.inventory[0];
        const items = Terraria.Item.newArray(3).fill(item);
        const ok1 = vs[2].Y === 2 && vs[0].X === 1 && items[2] === item && items[0] === item;
        items.fill(null, 1);
        const texts = ['a', 'b'].makeGeneric('string').fill('z');
        return (ok1 && items[0] === item && items[2] === null && list(texts) === 'z,z') || `${vs[2].Y} ${items[2]} ${list(texts)}`;
    });
    check('fill num campo do jogo muda o jogo', () => {
        const p = Main.LocalPlayer;
        const before = Array.from(p.buffImmune);
        p.buffImmune.fill(true, 1, 3);
        const changed = p.buffImmune[1] === true && p.buffImmune[2] === true;
        before.forEach((v, i) => { p.buffImmune[i] = v; });
        return changed || 'não mudou';
    });
    check('fill com valor que não converte lança', () => throws(() => System.Int32.newArray(2).fill('x'), ''));
    check('empty(): do mesmo tipo, sem posições; o original não muda', () => {
        const a = [1, 2].makeGeneric('int');
        const e = a.empty();
        return (String(e) === 'Int32[0]' && e.length === 0 && a.length === 2 && e !== a) || String(e);
    });
    check('find: o primeiro que passa, ou undefined', () => {
        const a = [5, 8, 13].makeGeneric('int');
        return (a.find((x) => x > 6) === 8 && a.find((x) => x > 100) === undefined) || String(a.find((x) => x > 6));
    });
    check('find recebe (elemento, índice, array) e o this', () => {
        const a = [5, 8, 13].makeGeneric('int');
        const ctx = { limite: 12 };
        const byIndex = a.find((x, i, arr) => i === 2 && arr === a);
        const byThis = a.find(function (x) { return x > this.limite; }, ctx);
        return (byIndex === 13 && byThis === 13) || `${byIndex} ${byThis}`;
    });
    check('find num array do jogo de objetos', () => {
        const inv = Main.LocalPlayer.inventory;
        const first = inv[0];
        return inv.find((it) => it === first) === first || 'não achou';
    });
    check('find de struct devolve a vista (escrever muda o array)', () => {
        const vs = [Vector2.new(1, 2), Vector2.new(3, 4)].makeGeneric(Vector2);
        vs.find((v) => v.X === 3).Y = 9;
        return vs[1].Y === 9 || vs[1].Y;
    });
    check('find: erro dentro da função sai', () => throws(() => [1].makeGeneric('int').find(() => { throw new Error('de dentro'); }), 'de dentro'));
    check('find sem função lança', () => throws(() => [1].makeGeneric('int').find(3), 'funcao'));
    check('método do JS que o array do jogo não tem: o erro indica Array.from', () =>
        throws(() => [1].makeGeneric('int').map((x) => x), 'Array.from'));
    check('o array JS original não muda', () => {
        const js = [1, 2];
        const a = js.makeGeneric('int');
        a[0] = 9;
        return (js[0] === 1 && Array.isArray(js)) || js[0];
    });
}

let done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    done = true;
    run();
    bl.log('arrays FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('arrays: carregado');

export default class TestArrays extends Mod {}
