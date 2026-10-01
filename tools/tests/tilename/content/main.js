// O nome da estação de trabalho de uma receita, como o guia de criação o pede
// (Recipe.GetRequiredTileName -> Lang.GetMapObjectName(MapHelper.TileToLookup)),
// para os blocos do Example Mod. Loga "tilename <caso>: ok | FALHOU".
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('tilename ' + label + ': ok');
        else { fails++; bl.log('tilename ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('tilename ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

function run() {
    const Recipe = Terraria.Recipe;
    const Lang = Terraria.Lang;
    const MapHelper = Terraria.Map.MapHelper;
    const show = (tile) => {
        const style = Recipe['int GetRequiredTileStyle(int tileID)'](tile);
        const lookup = MapHelper.TileToLookup(tile, style);
        const mapName = Lang.GetMapObjectName(lookup);
        const required = Recipe['string GetRequiredTileName(int tileId)'](tile);
        bl.log('tilename: tile ' + tile + ' estilo ' + style + ' lookup ' + lookup + ' mapa "' + mapName + '" receita "' + required + '"');
        return required;
    };

    show(18);   // bancada do jogo
    for (const name of ['ExampleWorkbench', 'ExampleTable', 'ExampleTile']) {
        const tile = ModContent.TileType(name);
        check(name + ': a receita tem nome', () => {
            if (tile <= 0) return 'sem o tile';
            const text = show(tile);
            return (typeof text === 'string' && text.trim().length > 0) || JSON.stringify(text);
        });
    }
    // No idioma do jogo: a da bancada vem da localização do mod, a da mesa é a do jogo.
    const nameOf = (tile) => Recipe['string GetRequiredTileName(int tileId)'](tile);
    const gameText = (key) => Terraria.Localization.Language['string GetTextValue(string key)'](key);
    check('ExampleWorkbench: nome traduzido (MapObject.ExampleWorkbench do mod)', () => {
        const got = nameOf(ModContent.TileType('ExampleWorkbench'));
        const want = { 'pt-BR': 'Bancada de Exemplo', 'en-US': 'Example Workbench' }[ModLocalization.ActiveCultureName] ?? got;
        return got === want || `"${got}" / "${want}"`;
    });
    check('ExampleTable: nome do jogo (MapObject.Table)', () => {
        const got = nameOf(ModContent.TileType('ExampleTable')), want = gameText('MapObject.Table');
        return got === want || `"${got}" / "${want}"`;
    });

    // A tabela do celular tile -> item da estação (o ícone do GUICrafting.SetupCraftingIcon).
    const stations = Terraria.ID.TileID.Sets.CraftingStationItemId;
    const nonZero = [];
    for (let t = 0; t < Math.min(stations.length, bl.tiles.vanillaCount); t++) if (stations[t] !== 0) nonZero.push(t + '>' + stations[t]);
    bl.log('tilename: CraftingStationItemId (' + stations.length + ') do jogo: ' + nonZero.length + ' tiles; ' + nonZero.slice(0, 12).join(' '));
    check('ExampleWorkbench: item da estação (ícone no guia)', () => {
        const bench = ModContent.TileType('ExampleWorkbench');
        const item = bench < stations.length ? stations[bench] : 'fora da tabela';
        return item === ModContent.ItemType('ExampleWorkbench') || 'item ' + item;
    });

    // O próprio popup do guia (GUICraftGuidePopup.UpdateText) com a receita da ExampleLamp.
    check('popup do guia: a estação da receita tem nome', () => {
        const lamp = ModContent.ItemType('ExampleLamp');
        const bench = ModContent.TileType('ExampleWorkbench');
        let index = -1;
        for (let r = 0; r < Recipe.numRecipes; r++) {
            const recipe = Main.recipe[r];
            if (recipe.createItem.type === lamp && recipe.requiredTile === bench) { index = r; break; }
        }
        if (index < 0) return 'sem a receita da ExampleLamp';
        const pop = GUIInstance.Active.GUICraftGuidePopup;
        pop.availableGuideRecipe[0] = index;
        pop.numAvailableGuideRecipes = 1;
        pop.focusGuideRecipe = 0;
        pop['void UpdateText()']();
        const list = pop._requiredObjecsForCraftingText;
        const names = [];
        for (let k = 0; k < list.Count; k++) names.push(String(list.get_Item(k)));
        bl.log('tilename: popup lista [' + names.join(' | ') + '] texto "' + String(pop.displayString).replace(/\n/g, '\\n') + '"');
        return (names.length > 0 && names[0].trim().length > 0) || 'lista: ' + JSON.stringify(names);
    });
    bl.log('tilename FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames === 60) run();
});

export default class TestTileName extends Mod {}
