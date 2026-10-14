// Os Global do Example Mod (ShakeTrees, SunflowerChanges, ExampleGlobalBuff,
// ExampleGlobalBossBar, ExampleGlobalRecipe) e as receitas com condição e
// callbacks. Precisa do Example Mod ligado. Deixa o Olho de Cthulhu e o buff
// da Fogueira na tela para a captura ("exmodglobals diag: captura").
// Loga "exmodglobals <caso>: ok | FALHOU".
const Main = Terraria.Main;
const Recipe = Terraria.Recipe;
const { BuffID, ItemID, NPCID, TileID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('exmodglobals ' + label + ': ok');
        else { fails++; bl.log('exmodglobals ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('exmodglobals ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const names = (base) => ModContent.GetContent(base).map((g) => g.constructor.name);
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
let eye = null;

function testContent(p) {
    check('GlobalTile do Example Mod', () => (names(GlobalTile).includes('ShakeTrees') && names(GlobalTile).includes('SunflowerChanges')) || JSON.stringify(names(GlobalTile)));
    check('GlobalBuff do Example Mod', () => names(GlobalBuff).includes('ExampleGlobalBuff') || JSON.stringify(names(GlobalBuff)));
    check('GlobalBossBar do Example Mod', () => names(GlobalBossBar).includes('ExampleGlobalBossBar') || JSON.stringify(names(GlobalBossBar)));
    check('GlobalRecipe do Example Mod', () => names(GlobalRecipe).includes('ExampleGlobalRecipe') || JSON.stringify(names(GlobalRecipe)));

    const exampleTile = ModContent.Find(ModTile, 'examplemod/ExampleTile').Type;
    const data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](TileID.Sunflower, 0, 0);
    const valid = data.AnchorValidTiles;
    let has = false;
    for (let k = 0; k < valid.length; k++) has = has || valid[k] === exampleTile;
    check('o Girassol aceita o Bloco de Exemplo', () => has || `${exampleTile} fora de ${valid.length}`);

    const exampleItem = ModContent.Find(ModItem, 'examplemod/ExampleItem').Type;
    let conditioned = -1, chain = -1;
    for (let i = 0; i < Recipe.numRecipes; i++) {
        const r = Main.recipe[i];
        if (r.createItem.type === exampleItem && r.createItem.stack === 100) conditioned = i;
        if (r.createItem.type === ItemID.AlphabetStatueJ && r.requiredItem[1].type === ItemID.Chain) chain = i;
    }
    check('a receita com condições existe', () => conditioned >= 0 || 'não achei');
    check('a receita da Corrente existe', () => chain >= 0 || 'não achei');

    p['void AddBuff(int type, int time, bool fromNetPvP)'](BuffID.Regeneration, 3600, false);
    p['void AddBuff(int type, int time, bool fromNetPvP)'](BuffID.Campfire, 3600, false);
    const tip = Main['string GetBuffTooltip(Player player, int buffType)'](p, BuffID.Regeneration);
    check('ExampleGlobalBuff: tempo restante na dica de um buff do jogo', () => /Tempo restante|Remaining time/.test(tip) || tip);

    Main.dayTime = false;
    Main.time = 1000;
    const idx = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.Center.X) + 200,
                       Math.floor(p.Center.Y) - 200, NPCID.EyeofCthulhu, 0, 0, 0, 0, 0, Main.myPlayer);
    eye = Main.npc[idx];
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    self.statLife = self.statLifeMax2;
    if (eye && eye.active) {
        eye.damage = 0;
        eye.life = eye.lifeMax;
        eye.Center = Vector2.new(self.Center.X + 200, self.Center.Y - 200);
        eye.velocity = Vector2.new(0, 0);
    }

    if (frames === 60) check('parte testContent', () => { testContent(self); });
    if (frames === 180) bl.log('exmodglobals diag: captura');
    if (frames === 240) {
        if (eye && eye.active) eye.active = false;
        for (const buff of [BuffID.Regeneration, BuffID.Campfire]) {
            const k = self['int FindBuffIndex(int type)'](buff);
            if (k >= 0) self['void DelBuff(int b)'](k);
        }
        Main.dayTime = true;
        Main.time = 20000;
        done = true;
        bl.log('exmodglobals FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('exmodglobals: carregado');

export default class TestExmodGlobals extends Mod {}
