// Quanto custa, em ns, cada padrão de acesso que os hooks de todo quadro usam.
// Mede no mundo (quadro 120), 2000 repetições de cada, menos o laço vazio.
// Loga "opcost ...".
const Main = Terraria.Main;
const N = 2000;

function time(label, fn) {
    try {
        const t0 = performance.now();
        for (let i = 0; i < N; i++) fn(i);
        return [label, (performance.now() - t0) * 1e6 / N];
    } catch (e) {
        bl.log('opcost ' + label + ': indisponível (' + e + ')');
        return [label, NaN];
    }
}

function run() {
    const p = Main.player[Main.myPlayer];
    const head = Terraria.ID.ArmorIDs.Head.Sets;
    const fullHair = head.DrawFullHair;
    const extras = globalThis.__blExtraStatics;
    const ref = new Ref(false);
    const rows = [
        time('laço vazio', () => 0),
        time('campo int do jogador (p.head)', () => p.head),
        time('campo float do jogador (p.meleeDamage)', () => p.meleeDamage),
        time('Main.player[myPlayer]', () => Main.player[Main.myPlayer]),
        time('Main.myPlayer', () => Main.myPlayer),
        time('Main.GameUpdateCount', () => Main.GameUpdateCount),
        time('classe estática aninhada (ArmorIDs.Head.Sets)', () => Terraria.ID.ArmorIDs.Head.Sets),
        time('array estático: Sets.DrawFullHair', () => head.DrawFullHair),
        time('item de array estático: Sets.DrawFullHair[5]', () => head.DrawFullHair[5]),
        time('item de array guardado: arr[5]', () => fullHair[5]),
        time('Ref.value =', (i) => { ref.value = (i & 1) === 0; }),
        time('p.armor[0]', () => p.armor[0]),
        time('p.inventory[p.selectedItem]', () => p.inventory[p.selectedItem]),
        time('item.type (item guardado)', () => p.armor[0].type),
        time('bl.tiles.typeAt', () => bl.tiles.typeAt(p.position.X / 16 | 0, p.position.Y / 16 | 0)),
        time('p.position.X', () => p.position.X),
        time('try/catch com closure', () => { try { return (() => 1)(); } catch (e) { return 0; } }),
        time('new StatModifier()', () => new StatModifier()),
        time('Vector2.new(1, 2)', () => Vector2.new(1, 2)),
        time('Color.new(1, 2, 3)', () => Color.new(1, 2, 3)),
        time('new Map com 4 pares', () => new Map([[1, 1], [2, 2], [3, 3], [4, 4]])),
        time('ModLoader.Mods.length', () => ModLoader.Mods.length),
        time('__blExtraStatics[nome]', () => extras && extras['Terraria.ID.ArmorIDs.Body.Sets']),
    ];
    const base = rows[0][1];
    for (const [label, ns] of rows) if (!isNaN(ns)) bl.log('opcost ' + label + ': ' + Math.max(0, ns - (label === 'laço vazio' ? 0 : base)).toFixed(0) + ' ns');
}

let frames = 0;
Main['void DoUpdate(GameTime gameTime)'].hook((original, main, t) => {
    original(main, t);
    if (Main.gameMenu || ++frames !== 120) return;
    try { run(); } catch (e) { bl.log('opcost erro: ' + e + ' ' + (e && e.stack)); }
    bl.log('opcost FIM: tudo ok');
});
bl.log('opcost: carregado');

export default class TestOpCost extends Mod {}
