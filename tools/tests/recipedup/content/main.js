// Receitas duplicadas no menu de criação. O jogo percorre o Main.recipe até
// o Recipe.maxRecipes; o RecipeLoader percorre do limite dele em diante. Se o
// limite do RecipeLoader for menor que o do jogo, as receitas no meio entram
// duas vezes. Põe no inventário os ingredientes de uma receita (e a estação)
// nessa faixa, roda o FindRecipes(false) e procura índice repetido.
// Loga "recipedup <caso>: ok | FALHOU".
const Main = Terraria.Main;
const Recipe = Terraria.Recipe;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('recipedup ' + label + ': ok');
        else { fails++; bl.log('recipedup ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('recipedup ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

function signature(r) {
    const parts = [r.createItem.type + 'x' + r.createItem.stack];
    for (let j = 0; j < r.requiredItem.length && r.requiredItem[j].type !== 0; j++) {
        parts.push(r.requiredItem[j].type + 'x' + r.requiredItem[j].stack);
    }
    for (let j = 0; j < r.requiredTile.length && r.requiredTile[j] !== -1; j++) parts.push('t' + r.requiredTile[j]);
    return parts.join(',');
}

function run() {
    const total = Recipe.numRecipes;
    let max = -1;
    try { max = Recipe.maxRecipes; } catch (e) { bl.log('recipedup diag: Recipe.maxRecipes ilegível: ' + e); }
    bl.log(`recipedup diag: numRecipes ${total}, Recipe.maxRecipes ${max}, limite antigo do RecipeLoader 3600`);

    // Receitas iguais no Main.recipe (registro duplicado).
    const seen = new Map();
    const twins = [];
    for (let i = 0; i < total; i++) {
        const s = signature(Main.recipe[i]);
        if (seen.has(s)) twins.push(seen.get(s) + '=' + i);
        else seen.set(s, i);
    }
    bl.log('recipedup diag: receitas iguais no Main.recipe: ' + twins.length + (twins.length ? ' ex. ' + twins.slice(0, 8).join(' ') : ''));

    // Uma receita sem estação, na faixa entre os dois limites (ou a última).
    const from = Math.min(3600, total - 1);
    let pick = -1;
    for (let i = from; i < total && pick < 0; i++) {
        const r = Main.recipe[i];
        if (r.requiredItem[0].type !== 0) pick = i;
    }
    check('achou receita a partir de ' + from, () => pick >= 0 || 'nenhuma');
    if (pick < 0) return;

    const p = Main.player[Main.myPlayer];
    const r = Main.recipe[pick];
    bl.log(`recipedup diag: receita ${pick}: ${signature(r)}`);
    const saved = [];
    for (let j = 0; j < r.requiredItem.length && r.requiredItem[j].type !== 0; j++) {
        const slot = 49 - j;
        saved.push([slot, p.inventory[slot].type, p.inventory[slot].stack]);
        p.inventory[slot]['void SetDefaults(int Type, ItemVariant variant)'](r.requiredItem[j].type, null);
        p.inventory[slot].stack = r.requiredItem[j].stack;
    }

    // A estação "por perto": o adjTile que o jogo recalcula a cada quadro.
    const tiles = [];
    for (let j = 0; j < r.requiredTile.length && r.requiredTile[j] !== -1; j++) tiles.push(r.requiredTile[j]);
    for (const t of tiles) p.adjTile[t] = true;

    Recipe['void FindRecipes(bool canDelayCheck)'](false);
    const n = Main.numAvailableRecipes;
    const list = [];
    for (let k = 0; k < n; k++) list.push(Main.availableRecipe[k]);
    const counts = new Map();
    for (const i of list) counts.set(i, (counts.get(i) || 0) + 1);
    const repeated = [...counts.entries()].filter(([, c]) => c > 1);
    bl.log(`recipedup diag: ${n} disponíveis; repetidos ${repeated.length}` +
           (repeated.length ? ' ex. ' + repeated.slice(0, 8).map(([i, c]) => i + 'x' + c).join(' ') : ''));

    check('a receita escolhida aparece', () => counts.has(pick) || 'não apareceu');
    check('nenhum índice duas vezes na lista de criação', () => repeated.length === 0 || repeated.length + ' repetidos');

    for (const [slot, type, stack] of saved) {
        p.inventory[slot]['void SetDefaults(int Type, ItemVariant variant)'](type, null);
        p.inventory[slot].stack = stack;
    }
    Recipe['void FindRecipes(bool canDelayCheck)'](false);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames < 60) return;

    done = true;
    try { run(); } catch (e) { fails++; bl.log('recipedup FALHOU com ' + e + ' | ' + (e.stack || '')); }
    bl.log('recipedup FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('recipedup: carregado');

export default class TestRecipeDup extends Mod {}
