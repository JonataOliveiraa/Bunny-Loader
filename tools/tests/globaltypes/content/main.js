// GlobalTile, GlobalBuff, GlobalRecipe e GlobalBossBar com o conteúdo do jogo:
// buffs do jogador e de um NPC, quebra/queda/explosão/fio de tiles, AdjTiles,
// atualização aleatória e animação, sacudir árvore, receitas com condição,
// consumo e OnCraft, e a barra do Olho de Cthulhu.
// Loga "globaltypes <caso>: ok | FALHOU".
const Main = Terraria.Main;
const WorldGen = Terraria.WorldGen;
const Recipe = Terraria.Recipe;
const { BuffID, ItemID, NPCID, TileID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('globaltypes ' + label + ': ok');
        else { fails++; bl.log('globaltypes ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('globaltypes ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const seen = {
    setup: 0, merge: 0,
    buffPlayer: new Set(), buffNpc: new Set(), buffIndexIsRef: false,
    kill: [], canKill: 0, drop: [], dust: new Map(), sound: 0, explode: 0, wire: [],
    random: 0, randomTypes: new Set(), animate: 0, preShake: [], shake: [],
    available: 0, consume: [], craft: [], callbackCraft: 0,
    bar: [], barTexture: false, buffDraw: 0,
};

export class TestGlobalBuff extends GlobalBuff {
    UpdatePlayer(type, player, buffIndex) {
        seen.buffPlayer.add(type);
        if (buffIndex instanceof Ref && Number.isInteger(buffIndex.value)) seen.buffIndexIsRef = true;
    }
    UpdateNPC(type, npc, buffIndex) { seen.buffNpc.add(type); }
    ReApplyPlayer(type, player, time, buffIndex) { return type === BuffID.Swiftness; }
    ReApplyNPC(type, npc, time, buffIndex) { return type === npcBuff; }
    ModifyBuffText(type, buffName, tip, rare) { buffName.value += ' [g]'; }
    PreDraw(spriteBatch, type, buffIndex, drawParams) { seen.buffDraw++; return true; }
}

export class TextBuff extends ModBuff {
    DisplayName = 'Brilho';
    Description = 'Brilha';
    Texture = 'Gem';
    ModifyBuffText(buffName, tip, rare) { buffName.value += ' x1'; }
}

export class TestGlobalTile extends GlobalTile {
    SetStaticDefaults() { seen.setup++; }
    PostSetupTileMerge() { seen.merge++; }
    CanKillTile(i, j, type, blockDamaged) { seen.canKill++; return type !== TileID.Glass; }
    KillTile(i, j, type, fail, effectOnly, noItem) { seen.kill.push(type); }
    NumDust(i, j, type, fail, num) { if (type === TileID.Stone) num.value = 2; }
    CreateDust(i, j, type, dustType) { seen.dust.set(type, (seen.dust.get(type) || 0) + 1); return true; }
    KillSound(i, j, type, fail) { seen.sound++; return true; }
    CanDrop(i, j, type) { return type !== TileID.Glass; }
    Drop(i, j, type) { seen.drop.push(type); }
    CanExplode(i, j, type) { seen.explode++; return type !== TileID.Glass; }
    HitWire(i, j, type) { seen.wire.push(type); }
    RandomUpdate(i, j, type, underground) { seen.random++; seen.randomTypes.add(type); }
    AnimateTile() { seen.animate++; }
    AdjTiles(type) { return [TileID.Glass, TileID.Grass, TileID.Dirt, TileID.Stone].includes(type) ? [TileID.Anvils] : []; }
    PreShakeTree(x, y, treeType) { seen.preShake.push(treeType); }
    ShakeTree(x, y, treeType) { seen.shake.push(treeType); return true; }
}

let allowTopaz = false;
let npcBuff = -1;
export class TestGlobalRecipe extends GlobalRecipe {
    RecipeAvailable(recipe) { seen.available++; return recipe.createItem.type !== ItemID.Torch; }
    ConsumeItem(recipe, type, amount) {
        seen.consume.push(type + ':' + amount.value);
        if (recipe.createItem.type === ItemID.Amethyst && type === ItemID.Gel) amount.value = 0;
    }
    OnCraft(item, recipe) { seen.craft.push(item.type); }
}

export class TestRecipes extends ModSystem {
    AddRecipes() {
        new ModRecipe()
            .SetResult(ItemID.Topaz)
            .AddIngredient(ItemID.Wood, 1)
            .AddCondition('Topaz liberado', () => allowTopaz)
            .Register();

        new ModRecipe()
            .SetResult(ItemID.Amethyst)
            .AddIngredient(ItemID.Wood, 5)
            .AddIngredient(ItemID.Gel, 2)
            .AddConsumeIngredientCallback((recipe, type, amount) => { if (type === ItemID.Wood) amount.value = 1; })
            .AddOnCraftCallback(() => { seen.callbackCraft++; })
            .Register();
    }
}

export class TestGlobalBossBar extends GlobalBossBar {
    PreDraw(spriteBatch, npc, drawParams) {
        seen.bar.push(npc.type);
        const c = drawParams.BarCenter;
        if (c.X > 0 && c.Y > 0 && c.X < Main.screenWidth * 2 && c.Y < Main.screenHeight * 2) seen.barTexture = true;
        drawParams.BarColor = Color.new(255, 80, 80);
        return true;
    }
}

const P = () => Main.player[Main.myPlayer];
const nameOf = (type) => Terraria.Lang['string GetBuffName(int id)'](type);
const typeAt = (i, j) => bl.tiles.typeAt(i, j);
const placeTile = WorldGen['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'];
const killTile = WorldGen['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'];
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

function buffTime(entity, type) {
    const index = entity['int FindBuffIndex(int type)'](type);
    return index < 0 ? -1 : entity.buffTime[index];
}

// Uma célula vazia perto do jogador (dx, dy em relação aos pés).
function freeCell(dx, dy) {
    const p = P();
    const x = Math.floor((p.position.X + p.width / 2) / 16), y = Math.floor((p.position.Y + p.height) / 16);
    for (let k = 0; k < 20; k++) {
        const i = x + dx + k, j = y + dy;
        if (typeAt(i, j) < 0) return { i, j };
    }
    return null;
}

// Uma célula vazia no alcance do AdjTiles (4 de lado, 3 acima), fora do corpo do jogador.
function nearCell() {
    const p = P();
    const x = Math.floor((p.position.X + p.width / 2) / 16), y = Math.floor((p.position.Y + p.height) / 16);
    for (const dx of [2, -2, 3, -3]) {
        for (const dy of [-1, -2, -3, 0, 1]) {
            if (typeAt(x + dx, y + dy) < 0) return { i: x + dx, j: y + dy };
        }
    }
    return null;
}

function availableIndices() {
    const avail = Main['int[] get_availableRecipe()']();
    const n = Main['int get_numAvailableRecipes()']();
    const list = [];
    for (let k = 0; k < n; k++) list.push(avail[k]);
    return list;
}

function findRecipe(result) {
    for (let i = 0; i < Recipe.numRecipes; i++) {
        if (Main.recipe[i].createItem.type === result) return i;
    }
    return -1;
}

function countItem(p, type) {
    let n = 0;
    for (let k = 0; k < 58; k++) if (p.inventory[k].type === type) n += p.inventory[k].stack;
    if (Main.mouseItem && Main.mouseItem.type === type) n += Main.mouseItem.stack;
    return n;
}

function setSlot(p, slot, type, stack) {
    const item = p.inventory[slot];
    item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    item.stack = stack;
}

let slime = null, eye = null;
const saved = [];

function testBuffs(p) {
    p['void AddBuff(int type, int time, bool fromNetPvP)'](BuffID.Regeneration, 600, false);
    p['void AddBuff(int type, int time, bool fromNetPvP)'](BuffID.Swiftness, 600, false);
    p['void AddBuff(int type, int time, bool fromNetPvP)'](BuffID.Ironskin, 600, false);
    p['void AddBuff(int type, int time, bool fromNetPvP)'](ModContent.BuffType(TextBuff), 600, false);

    const swift = p['int FindBuffIndex(int type)'](BuffID.Swiftness);
    const iron = p['int FindBuffIndex(int type)'](BuffID.Ironskin);
    p.buffTime[swift] = 100;
    p.buffTime[iron] = 100;
    p['void AddBuff(int type, int time, bool fromNetPvP)'](BuffID.Swiftness, 600, false);
    p['void AddBuff(int type, int time, bool fromNetPvP)'](BuffID.Ironskin, 600, false);
    check('ReApplyPlayer true: o tempo não renova', () => buffTime(p, BuffID.Swiftness) <= 100 || buffTime(p, BuffID.Swiftness));
    check('ReApplyPlayer false: o jogo renova', () => buffTime(p, BuffID.Ironskin) === 600 || buffTime(p, BuffID.Ironskin));

    check('ModifyBuffText num buff do jogo', () => nameOf(BuffID.Regeneration).endsWith(' [g]') || nameOf(BuffID.Regeneration));
    check('ModifyBuffText: o do ModBuff e depois o Global', () => nameOf(ModContent.BuffType(TextBuff)) === 'Brilho x1 [g]' || nameOf(ModContent.BuffType(TextBuff)));

    const idx = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.Center.X) + 20 * 16,
                       Math.floor(p.position.Y) - 10 * 16, NPCID.BlueSlime, 0, 0, 0, 0, 0, Main.myPlayer);
    slime = Main.npc[idx];
    slime.damage = 0;
    slime['void AddBuff(int type, int time, bool quiet)'](BuffID.OnFire, 600, true);
    for (const buff of [BuffID.Confused, BuffID.Midas, BuffID.Ichor, BuffID.Poisoned]) {
        slime['void AddBuff(int type, int time, bool quiet)'](buff, 600, true);
        if (slime['int FindBuffIndex(int type)'](buff) >= 0) { npcBuff = buff; break; }
    }
    if (npcBuff < 0) return check('um debuff que o slime aceita', () => 'nenhum');
    slime.buffTime[slime['int FindBuffIndex(int type)'](npcBuff)] = 50;
    slime['void AddBuff(int type, int time, bool quiet)'](npcBuff, 600, true);
    check('ReApplyNPC true: o tempo não renova', () => buffTime(slime, npcBuff) <= 50 || buffTime(slime, npcBuff));
}

function testTiles(p) {
    check('SetStaticDefaults e PostSetupTileMerge', () => (seen.setup === 1 && seen.merge === 1) || `${seen.setup}/${seen.merge}`);

    const dirt = freeCell(3, -6);
    if (!dirt) return check('célula livre', () => 'nenhuma');
    placeTile(dirt.i, dirt.j, TileID.Dirt, true, true, -1, 0);
    killTile(dirt.i, dirt.j, false, false, false);
    check('KillTile num tile do jogo (terra)', () => seen.kill.includes(TileID.Dirt) || JSON.stringify(seen.kill));
    check('Drop depois dos itens da terra', () => seen.drop.includes(TileID.Dirt) || JSON.stringify(seen.drop));
    check('CreateDust na terra', () => (seen.dust.get(TileID.Dirt) || 0) > 0 || 'nenhuma');

    placeTile(dirt.i, dirt.j, TileID.Stone, true, true, -1, 0);
    killTile(dirt.i, dirt.j, false, false, true);
    check('NumDust 2 na pedra', () => (seen.dust.get(TileID.Stone) || 0) <= 2 || seen.dust.get(TileID.Stone));
    check('KillSound chamado', () => seen.sound > 0 || 0);

    placeTile(dirt.i, dirt.j, TileID.Glass, true, true, -1, 0);
    const damaged = new Ref(false);
    check('CanKillTile false no vidro', () => WorldGen['bool CanKillTile(int i, int j, out bool blockDamaged)'](dirt.i, dirt.j, damaged) === false || 'pode');
    check('CanExplode false no vidro', () => Main.projectile[0]['bool CanExplodeTile(int x, int y, bool explodeHardmodeOres)'](dirt.i, dirt.j, false) === false || 'explode');
    const before = seen.drop.length;
    WorldGen['void KillTile_DropItems(int x, int y, Tile tileCache, bool includeLargeObjectDrops)'](dirt.i, dirt.j, Main.tile['Tile get_Item(int x, int y)'](dirt.i, dirt.j), false);
    check('CanDrop false: sem Drop no vidro', () => seen.drop.length === before || JSON.stringify(seen.drop));
    Terraria.Wiring['void HitWireSingle(int i, int j)'](dirt.i, dirt.j);
    check('HitWire num tile do jogo', () => seen.wire.includes(TileID.Glass) || JSON.stringify(seen.wire));

    // O vidro ao lado do jogador conta como bigorna (AdjTiles).
    const glass = nearCell();
    if (glass) {
        placeTile(glass.i, glass.j, TileID.Glass, true, true, -1, 0);
        saved.push(glass);
    }
    saved.push(dirt);
}

function testShake(p) {
    const x0 = Math.floor(p.Center.X / 16), y0 = Math.floor(p.Center.Y / 16);
    const trunks = [];
    for (let dx = 0; dx < 300 && trunks.length < 40; dx++) {
        for (const sx of dx ? [x0 + dx, x0 - dx] : [x0]) {
            for (let y = y0 - 60; y < y0 + 40; y++) {
                if (typeAt(sx, y) === TileID.Trees && typeAt(sx, y + 1) !== TileID.Trees) { trunks.push({ i: sx, j: y }); break; }
            }
        }
    }
    if (!trunks.length) return bl.log('globaltypes diag: nenhuma árvore perto para sacudir');

    const shake = WorldGen['void ShakeTree(int i, int j)'];
    let trunk = null;
    for (const t of trunks) {
        WorldGen.numTreeShakes = 0;
        shake(t.i, t.j);
        if (seen.shake.length) { trunk = t; break; }
    }
    bl.log(`globaltypes diag: ${trunks.length} árvores, PreShakeTree ${JSON.stringify(seen.preShake)}, ShakeTree ${JSON.stringify(seen.shake)}`);
    if (!trunk) return bl.log('globaltypes diag: nenhuma com copa livre (o jogo não sacode)');

    check('PreShakeTree e ShakeTree com o tipo da árvore', () => (seen.preShake.length >= 1 && seen.shake[0] > 0) || JSON.stringify(seen.preShake));
    check('a sacudida ficou registrada', () => WorldGen.numTreeShakes === 1 || WorldGen.numTreeShakes);
    shake(trunk.i, trunk.j);
    check('a mesma árvore não sacode de novo', () => seen.shake.length === 1 || seen.shake.length);
}

function testRecipes(p) {
    for (const slot of [46, 47, 48, 49]) saved.push({ slot, type: p.inventory[slot].type, stack: p.inventory[slot].stack });
    setSlot(p, 46, 0, 0);
    setSlot(p, 47, 0, 0);
    setSlot(p, 48, ItemID.Wood, 20);
    setSlot(p, 49, ItemID.Gel, 20);

    const torch = findRecipe(ItemID.Torch), topaz = findRecipe(ItemID.Topaz), amethyst = findRecipe(ItemID.Amethyst);
    Recipe['void FindRecipes(bool canDelayCheck)'](false);
    let list = availableIndices();
    check('RecipeAvailable false tira a Tocha', () => (torch >= 0 && !list.includes(torch)) || `tocha ${torch} em ${list.length}`);
    check('as outras ficam (Ametista)', () => list.includes(amethyst) || `${amethyst} fora de ${list.length}`);
    check('condição falsa: Topázio fora', () => !list.includes(topaz) || 'aparece');
    allowTopaz = true;
    Recipe['void FindRecipes(bool canDelayCheck)'](false);
    list = availableIndices();
    check('condição verdadeira: Topázio dentro', () => list.includes(topaz) || `${topaz} fora`);
    check('Condition.Description', () => new Condition('Conditions.TimeNight', () => true).Description.length > 0 || 'vazia');

    const wood = countItem(p, ItemID.Wood), gel = countItem(p, ItemID.Gel), have = countItem(p, ItemID.Amethyst);
    Terraria.GameContent.CraftingRequests['void CraftItem(Recipe recipe, int qty, bool quickCraft)'](Main.recipe[amethyst], 1, true);
    check('callback de consumo: 1 madeira em vez de 5', () => countItem(p, ItemID.Wood) === wood - 1 || `${wood} -> ${countItem(p, ItemID.Wood)}`);
    check('GlobalRecipe.ConsumeItem: gel não sai', () => countItem(p, ItemID.Gel) === gel || `${gel} -> ${countItem(p, ItemID.Gel)}`);
    check('a Ametista foi criada', () => countItem(p, ItemID.Amethyst) === have + 1 || `${have} -> ${countItem(p, ItemID.Amethyst)}`);
    check('OnCraft do Global e callback', () => (seen.craft.includes(ItemID.Amethyst) && seen.callbackCraft === 1) || `${JSON.stringify(seen.craft)} / ${seen.callbackCraft}`);
    bl.log('globaltypes diag: ConsumeItem ' + JSON.stringify(seen.consume));
}

function cleanup(p) {
    for (const s of saved) {
        if (s.slot !== undefined) setSlot(p, s.slot, s.type, s.stack);
        else if (typeAt(s.i, s.j) >= 0) killTile(s.i, s.j, false, false, true);
    }
    for (let k = 0; k < 58; k++) {
        const t = p.inventory[k].type;
        if ((t === ItemID.Amethyst || t === ItemID.Topaz) && !saved.some((s) => s.slot === k)) setSlot(p, k, 0, 0);
    }
    if (Main.mouseItem && Main.mouseItem.type === ItemID.Amethyst) Main.mouseItem['void TurnToAir()']();
    for (const npc of [slime, eye]) if (npc && npc.active) npc.active = false;
    for (const buff of [BuffID.Regeneration, BuffID.Swiftness, BuffID.Ironskin, ModContent.BuffType(TextBuff)]) {
        const k = p['int FindBuffIndex(int type)'](buff);
        if (k >= 0) p['void DelBuff(int b)'](k);
    }
    Recipe['void FindRecipes(bool canDelayCheck)'](false);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 1) { Main.dayTime = true; Main.time = 20000; }
    if (eye && eye.active) {
        eye.damage = 0;
        eye.life = eye.lifeMax;
        eye.Center = Vector2.new(self.Center.X + 200, self.Center.Y - 200);
        eye.velocity = Vector2.new(0, 0);
    }
    self.statLife = self.statLifeMax2;

    if (frames === 60) {
        for (const part of [testBuffs, testTiles, testShake, testRecipes]) check('parte ' + part.name, () => { part(self); });
        Main.dayTime = false;
        Main.time = 1000;
        const idx = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(self.Center.X) + 200,
                           Math.floor(self.Center.Y) - 200, NPCID.EyeofCthulhu, 0, 0, 0, 0, 0, Main.myPlayer);
        eye = Main.npc[idx];
        eye.damage = 0;
    }

    if (frames === 200) bl.log('globaltypes diag: captura');
    if (frames === 240) {
        check('UpdatePlayer: buff do jogo, índice num Ref', () => (seen.buffPlayer.has(BuffID.Regeneration) && seen.buffIndexIsRef) || JSON.stringify([...seen.buffPlayer]));
        check('UpdateNPC: buffs do slime', () => seen.buffNpc.has(BuffID.OnFire) || JSON.stringify([...seen.buffNpc]));
        // No celular o jogo só chama o AdjTiles com o inventário aberto.
        self['void AdjTiles()']();
        const ground = [TileID.Glass, TileID.Grass, TileID.Dirt, TileID.Stone].filter((t) => self.adjTile[t]);
        check('AdjTiles: o chão perto conta como bigorna', () => (ground.length > 0 && self.adjTile[TileID.Anvils] === true) || `perto ${JSON.stringify(ground)}, bigorna ${self.adjTile[TileID.Anvils]}`);
        const system = Main.BigBossProgressBar;
        const stats = bl.hookStats().filter((st) => /FancyBar|BigProgressBar|HealthText|GUIBossBar|DrawBacking|Region|AdjTiles/.test(st.name)).map((st) => `${st.name} ${st.calls}/${st.js}`);
        bl.log(`globaltypes diag: olho ${eye ? eye.active + ' ' + eye.type + ' #' + eye.whoAmI : 'nenhum'}, barra atual ${system ? !!system._currentBar : '?'} mira ${system ? system._info.npcIndexToAimAt : '?'}, ganchos ${JSON.stringify(stats)}`);
        check('AnimateTile uma vez por quadro', () => seen.animate >= 150 || seen.animate);
        check('RandomUpdate em tiles do jogo', () => seen.random > 0 || 0);
        check('GlobalBossBar no Olho de Cthulhu', () => seen.bar.includes(NPCID.EyeofCthulhu) || JSON.stringify(seen.bar.slice(0, 5)));
        check('BarCenter dentro da tela', () => seen.barTexture || 'fora');
        bl.log(`globaltypes diag: RandomUpdate ${seen.random} (${seen.randomTypes.size} tipos), barra ${seen.bar.length}, ícones de buff ${seen.buffDraw}, RecipeAvailable ${seen.available}`);
        cleanup(self);
        Main.dayTime = true;
        Main.time = 20000;
        done = true;
        bl.log('globaltypes FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('globaltypes: carregado');

export default class TestGlobalTypes extends Mod {}
