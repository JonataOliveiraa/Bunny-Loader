// Receitas: cloneResized, array JS no lugar de int[], grupos (do jogo, do
// Example Mod e deste teste), ModSystem e a tabela que cresce alem do fim.
// Precisa do Example Mod ligado. Loga "recipes <caso>: ok | FALHOU".
const Main = Terraria.Main;
const Recipe = Terraria.Recipe;
const { ItemID, TileID } = Terraria.ID;

let gems = null;
let systemRan = { groups: 0, recipes: 0 };

let vanillaRecipes = 0;

class TestRecipes extends ModSystem {
    AddRecipeGroups() {
        vanillaRecipes = Recipe.numRecipes;
        systemRan.groups++;
        gems = ModRecipe.CreateRecipeGroup('TestGems', [ItemID.Ruby, ItemID.Sapphire, ItemID.Emerald]);
    }
    AddRecipes() {
        systemRan.recipes++;
        new ModRecipe()
            .SetResult(ItemID.Amber)
            .AddRecipeGroup('TestGems', 3)
            .Register();
    }
}
ModSystem.register(TestRecipes);

let crafted = 0;
class TestCraft extends ModItem {
    SetDefaults() {
        this.Item.width = 10;
        this.Item.height = 10;
        this.Item.maxStack = 99;
    }
    OnCraft(item, player, recipe) {
        crafted++;
        item.stack = 7;
    }
}
const CRAFT = ModItem.register(TestCraft);

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('recipes ' + label + ': ok');
        else { fails++; bl.log('recipes ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('recipes ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const me = () => Main.player[Main.myPlayer];

// Item de outro mod pelo nome no jogo (getTypeByName so ve os deste mod).
function itemType(name) {
    for (let t = bl.items.vanillaCount; t < bl.items.vanillaCount + 400; t++) {
        if (!bl.items.isModItem(t)) break;
        if (Terraria.Lang['string GetItemNameValue(int id)'](t) === name) return t;
    }
    return -1;
}

function findRecipe(result, pred = () => true) {
    for (let i = 0; i < Recipe.numRecipes; i++) {
        const r = Main.recipe[i];
        if (r && r.createItem.type === result && pred(r)) return i;
    }
    return -1;
}

function acceptsGroup(r, g) {
    for (let i = 0; i < r.acceptedGroups.length; i++) {
        if (r.acceptedGroups[i] === g.RegisteredId) return true;
    }
    return false;
}

// O menu de criacao: o que o jogador pode fazer agora, com `items` no slot 49.
function craftable(recipeIndex, type, stack) {
    const p = me();
    const slot = p.inventory[49];
    const saved = { type: slot.type, stack: slot.stack };
    slot['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    slot.stack = stack;
    Recipe['void FindRecipes(bool canDelayCheck)'](false);
    const avail = Main['int[] get_availableRecipe()']();
    const n = Main['int get_numAvailableRecipes()']();
    let found = false;
    for (let i = 0; i < n && !found; i++) found = avail[i] === recipeIndex;
    slot['void SetDefaults(int Type, ItemVariant variant)'](saved.type, null);
    slot.stack = saved.stack;
    Recipe['void FindRecipes(bool canDelayCheck)'](false);
    return found;
}

function run() {
    bl.log('recipes: jogo ' + Recipe.numRecipes + ' receitas, tabela ' + Main.recipe.length);

    check('cloneResized: referencia, maior', () => {
        const a = Main.recipe;
        const b = a.cloneResized(a.length + 2);
        if (b.length !== a.length + 2) return 'length ' + b.length;
        if (b[0] !== a[0] || b[a.length - 1] !== a[a.length - 1]) return 'copia errada';
        return b[a.length] === null || 'resto ' + b[a.length];
    });

    check('cloneResized: valor, menor', () => {
        const a = ItemID.Sets.IsAMaterial;
        const b = a.cloneResized(20);
        if (b.length !== 20) return 'length ' + b.length;
        for (let i = 0; i < 20; i++) if (b[i] !== a[i]) return 'posicao ' + i;
        b[0] = !a[0];
        return b[0] !== a[0] || 'nao e copia';
    });

    check('estatico de referencia: atribui e le de volta', () => {
        const Hold = Terraria.Main;
        const saved = Hold.recipe;
        const copy = saved.cloneResized(saved.length);
        Hold.recipe = copy;
        const back = Hold.recipe;
        Hold.recipe = saved;
        if (back.length !== saved.length) return 'length ' + back.length;
        return (back[0] === saved[0] && Hold.recipe === saved) || 'nao voltou';
    });

    check('array JS vira int[]', () => {
        if (!gems) return 'grupo nao criado';
        const n = gems.ValidItems.Count;
        return (n === 3 && gems.Contains(ItemID.Emerald) && !gems.Contains(ItemID.Diamond)) || 'ValidItems ' + n;
    });

    check('ModSystem: grupos antes das receitas', () =>
        (systemRan.groups === 1 && systemRan.recipes === 1) || JSON.stringify(systemRan));

    check('grupo do jogo por nome', () => {
        const g = ModRecipe.GetGroup('IronBar');
        return (!!g && g.RegisteredId === Terraria.ID.RecipeGroups.IronBar.RegisteredId) || 'IronBar ' + g;
    });

    check('grupo: nome no menu', () => {
        const g = ModRecipe.GetGroup('ExampleItem');
        if (!g) return 'sem o grupo do Example Mod';
        const text = g['string ToString()']();
        return /Item de Exemplo|Example Item/.test(text) || 'texto "' + text + '"';
    });

    check('Example Mod: receitas', () => {
        const rod = findRecipe(ItemID.RodofDiscord, (r) => r.requiredItem[0].type === ItemID.ChaosFish);
        const spiky = findRecipe(ItemID.SpikyBall, (r) => r.needSnowBiome && acceptsGroup(r, ModRecipe.GetGroup('IronBar')));
        const life = findRecipe(ItemID.LifeCrystal, (r) => r.requiredItem[0].stack === 50);
        return (rod >= 0 && spiky >= 0 && life >= 0) || `rod ${rod}, spiky ${spiky}, life ${life}`;
    });

    check('grupo deste teste: craftavel com outro item do grupo', () => {
        const idx = findRecipe(ItemID.Amber, (r) => acceptsGroup(r, gems));
        if (idx < 0) return 'receita nao achada';
        if (Main.recipe[idx].requiredItem[0].type !== ItemID.Ruby) return 'modelo ' + Main.recipe[idx].requiredItem[0].type;
        if (!craftable(idx, ItemID.Emerald, 3)) return 'com 3 esmeraldas nao aparece';
        return !craftable(idx, ItemID.Emerald, 2) || 'com 2 esmeraldas aparece';
    });

    check('Example Mod: espada pelo grupo (almas)', () => {
        const sword = itemType('Espada');
        const soul = itemType('Alma de Exemplo');
        if (sword < 0 || soul < 0) return `espada ${sword}, alma ${soul}`;
        const ex = ModRecipe.GetGroup('ExampleItem');
        const idx = findRecipe(sword, (r) => acceptsGroup(r, ex));
        if (idx < 0) return 'receita nao achada';
        if (idx >= 3600) return 'receita na posicao ' + idx + ' (fora do menu)';
        return craftable(idx, soul, 50) || 'com 50 almas nao aparece';
    });

    check('OnCraft: antes de entregar', () => {
        const recipe = Main.recipe[0];
        const result = Terraria.Item.new();
        result['void .ctor()']();
        result['void SetDefaults(int Type, ItemVariant variant)'](CRAFT, null);
        const mouse = Main['Item get_mouseItem()']();
        if (mouse.type !== 0) return 'mouse ocupado: ' + mouse.type;
        Main['void CraftItem_GrantItem(Recipe recipe, Item result, bool quickCraft)'](recipe, result, false);
        const got = Main['Item get_mouseItem()']();
        const ok = crafted === 1 && got.type === CRAFT && got.stack === 7;
        const empty = Terraria.Item.new();
        empty['void .ctor()']();
        Main['void set_mouseItem(Item value)'](empty);
        return ok || `crafted ${crafted}, mouse ${got.type} x${got.stack}`;
    });

    check('vanilla: ultimas receitas', () => {
        const parts = [];
        for (let i = Math.max(0, vanillaRecipes - 3); i < vanillaRecipes; i++) {
            const r = Main.recipe[i];
            parts.push(i + ':' + r.createItem.type + (r.needMechdusa ? '(mechdusa)' : '') + '@' + r.requiredTile);
        }
        bl.log('recipes: fim do jogo ' + parts.join(' '));
        return true;
    });

    // Por ultimo: enche a tabela ate passar do fim (so se faltar pouco).
    check('Register alem do fim da tabela', () => {
        const len = Main.recipe.length;
        const free = len - Recipe.numRecipes;
        if (free > 80) { bl.log('recipes: ' + free + ' posicoes livres; crescimento nao testado'); return true; }
        for (let i = 0; i <= free; i++) {
            new ModRecipe().SetResult(ItemID.DirtBlock).AddIngredient(ItemID.StoneBlock, 999).AddTile(TileID.Anvils).Register();
        }
        Recipe['void CreateRequiredItemQuickLookups()']();
        if (Main.recipe.length !== len + 1) return 'tabela ' + Main.recipe.length;
        if (Recipe.numRecipes !== len + 1) return 'numRecipes ' + Recipe.numRecipes;
        const last = Main.recipe[len];
        Recipe['void FindRecipes(bool canDelayCheck)'](false);
        return (last && last.createItem.type === ItemID.DirtBlock) || 'ultima ' + last;
    });

    bl.log('recipes FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames === 90) run();
});
bl.log('recipes: carregado');
