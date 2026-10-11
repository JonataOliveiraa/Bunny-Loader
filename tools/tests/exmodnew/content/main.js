// Os exemplos novos do Example Mod, no jogo (precisa dele ligado). Num chão de
// pedra no alto, longe do spawn:
//   - os tipos existem; o SceneMetrics dos pilares conta tiles de mod sem
//     escrever fora do array (o _tileCounts dele cresceu);
//   - ExamplePylonTile: colocado pelo caminho do jogador, ganha a
//     SimplePylonTileEntity e entra na lista; o bioma recusa sem Bloco de
//     Exemplo e aceita com 40+; a loja do Mercador ganha a entrada com
//     condição (fora do bioma, não vende); a do Mercador Viajante, não;
//   - BasicTileEntity: nasce com o barril, enche na chuva, o desenho segue o
//     nível, o toque cheio dá o balde e zera, sai com o barril;
//   - ExampleTree: a bolota no Bloco de Exemplo vira a ExampleSapling, que
//     cresce (pelo AttemptToGrow e pelo RandomUpdate dela); a folha é a gore do
//     mod com SpecialAI 3; sacudir dá Bloco de Exemplo; cortar, Exemplo de Item;
//   - ExampleBossBar: o Olho de ??? com a barra do mod, escudo até a metade da
//     vida e a cabeça da 2a fase;
//   - ExamplePickup: pego, some e dá a Defesa; a dica da Defesa ganha o tempo
//     restante (ModifyBuffText); de longe só com a Poção de Alcance Cardíaco;
//   - ExampleShortswordProjectile: a posição é a da IA (ShouldUpdatePosition);
//   - ExampleHookProjectile: volta a ~500 px a 18; no teto, só 2 presos.
// Loga "exmodnew <caso>: ok | FALHOU" e "exmodnew pose <nome>".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileID, ItemID, BuffID, NPCID } = Terraria.ID;
const NewItem = Terraria.Item['int NewItem(IEntitySource source, int X, int Y, int Width, int Height, int type, int stack, bool noBroadcast, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier)'];
const newProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const CanPlace = Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'];

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('exmodnew ' + label + ': ok');
        else { fails++; bl.log('exmodnew ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('exmodnew ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const EX = (name) => 'examplemod/' + name;
const T = (name) => ModContent.TileType(EX(name));
const I = (name) => ModContent.ItemType(EX(name));
const P = (name) => ModContent.ProjectileType(EX(name));
const me = () => Main.player[Main.myPlayer];
const kill = (x, y, fail = false, noItem = true) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, fail, false, noItem);
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);

// O chão: pedra de x0-20 a x0+40; o pilar em x0, o barril em x0+8, a árvore
// em x0+22 (num trecho de Bloco de Exemplo). Teto 14 acima, para o gancho.
function area() {
    const x0 = Main.spawnTileX - 150, floor = Main.spawnTileY - 45;
    return { x0, floor, ceiling: floor - 14, pylon: x0, barrel: x0 + 8, tree: x0 + 22, stand: x0 + 12 };
}

function clearArea() {
    const { x0, floor, ceiling } = area();
    for (let i = x0 - 22; i <= x0 + 42; i++) {
        for (let j = ceiling - 30; j <= floor + 4; j++) if (bl.tiles.typeAt(i, j) >= 0) kill(i, j);
    }
}

function clearItems() {
    const { x0, floor } = area();
    for (let k = 0; k < 400; k++) {
        const it = Main.item[k];
        if (!it.active) continue;
        if (Math.abs(it.position.X / 16 - (x0 + 10)) < 40 && Math.abs(it.position.Y / 16 - floor) < 45) it['void TurnToAir()']();
    }
}

function itemsNear(type, x, radius = 20) {
    const { floor } = area();
    let n = 0;
    for (let k = 0; k < 400; k++) {
        const it = Main.item[k];
        if (!it.active || it.type !== type) continue;
        if (Math.abs(it.position.X / 16 - x) <= radius && Math.abs(it.position.Y / 16 - (floor - 10)) <= 30) n += it.stack;
    }
    return n;
}

function placeAsPlayer(x, y, type) {
    Terraria.Player.tileTargetX = x;
    Terraria.Player.tileTargetY = y;
    const data = new Ref();
    if (!CanPlace(x, y, type, 0, 1, data, false, null)) return false;
    me()['TileObject PlaceThing_Tiles_PlaceIt(bool newObjectType, TileObject data, int tileToCreate)'](true, data.value, type);
    return bl.tiles.typeAt(x, y);
}

let standX = 0;
function stand() {
    const p = me(), { floor } = area();
    p.position = Vector2.new(standX * 16 - p.width / 2, floor * 16 - p.height);
    p.velocity = Vector2.new(0, 0);
    p.fallStart = floor - 3;
}

// ---- tipos e a contagem de bioma dos pilares ----
let pylon = null, barrelTile = null;
function types() {
    check('os tipos do Example Mod existem', () => {
        const got = {
            pylonTile: T('ExamplePylonTile'), pylonItem: I('ExamplePylonItem'), barrelTile: T('BasicTileEntityTile'),
            barrelItem: I('BasicTileEntityItem'), sapling: T('ExampleSapling'), pickup: I('ExamplePickup'),
            sword: I('ExampleShortsword'), swordProj: P('ExampleShortswordProjectile'), hook: P('ExampleHookProjectile'),
        };
        const missing = Object.keys(got).filter((k) => !(got[k] > 0));
        return missing.length === 0 || 'faltam ' + missing.join(', ') + ' ' + JSON.stringify(got);
    });
    pylon = ModContent.Find(ModTile, EX('ExamplePylonTile'));
    barrelTile = ModContent.Find(ModTile, EX('BasicTileEntityTile'));
    check('o pilar tem tipo de pilar novo', () => pylon.PylonType >= 11 || 'PylonType ' + pylon.PylonType);
    check('o SceneMetrics dos pilares conta todos os tiles (o _tileCounts cresceu)', () => {
        const counts = Main.PylonSystem._sceneMetrics._tileCounts;
        return counts.length >= Main.tileFrameImportant.length || `${counts.length} < ${Main.tileFrameImportant.length}`;
    });
}

// ---- pilar ----
let pylonInfo = null;
// O Scan do jogo pula a 2a varredura no mesmo quadro e no mesmo centro: a
// segunda deste teste vai 1 px ao lado (o mesmo bloco).
function scanBiome(info, nudge = 0) {
    const metrics = Main.PylonSystem._sceneMetrics;
    const settings = Terraria.SceneMetricsScanSettings.new();
    settings.BiomeScanCenterPositionInWorld = Vector2.new(info.PositionInTiles.X * 16 + 8 + nudge, info.PositionInTiles.Y * 16 + 8);
    metrics['void Scan(SceneMetricsScanSettings settings)'](settings);
    return metrics;
}

function pylonTests() {
    const { x0, floor, pylon: px } = area();
    check('pilar: colocado pelo jogador, com a SimplePylonTileEntity', () => {
        const placed = placeAsPlayer(px, floor - 1, pylon.Type);
        if (placed !== pylon.Type) return 'colocou ' + placed;
        const e = ModTileEntity.TryGet(px, floor - 1);
        return (e && e.constructor.name === 'SimplePylonTileEntity' && e.TryGetModPylon() === pylon) || 'entidade ' + (e && e.constructor.name);
    });
    check('pilar: na lista de pilares do jogo', () => {
        const list = Main.PylonSystem.Pylons;
        for (let k = 0; k < list.Count; k++) {
            const info = list.get_Item(k);
            if (info.TypeOfPylon === pylon.PylonType) { pylonInfo = info; return true; }
        }
        return list.Count + ' pilar(es), nenhum do tipo ' + pylon.PylonType;
    });
    if (!pylonInfo) return;
    check('pilar: sem Bloco de Exemplo, o bioma recusa', () => {
        const metrics = scanBiome(pylonInfo);
        return pylon.ValidTeleportCheck_BiomeRequirements(pylonInfo, metrics) === false || 'aceitou (' + metrics._tileCounts[T('ExampleTile')] + ' blocos)';
    });
    // 3 linhas de Bloco de Exemplo embaixo do chão: 75 blocos de mod na varredura.
    for (let i = x0 - 12; i <= x0 + 12; i++) for (let j = floor + 1; j <= floor + 3; j++) placeTile(i, j, T('ExampleTile'));
    check('pilar: com 40+ Blocos de Exemplo, aceita (e a varredura conta o tile de mod)', () => {
        const metrics = scanBiome(pylonInfo, 1);
        const n = metrics._tileCounts[T('ExampleTile')];
        return (n >= 40 && pylon.ValidTeleportCheck_BiomeRequirements(pylonInfo, metrics) === true) || 'contou ' + n;
    });
    check('loja: o Mercador ganha a entrada do pilar, com condição; fora do bioma não vende', () => {
        const store = Terraria.InventoryStorage.new();
        store['void .ctor()']();
        store['void SetupShop(int type)'](1);
        const entry = NPCShop.get(NPCID.Merchant).Entries.find((e) => e.type === I('ExamplePylonItem'));
        let selling = false;
        for (let k = 0; k < store.item.length; k++) if (store.item[k].type === I('ExamplePylonItem')) selling = true;
        return (entry && typeof entry.condition === 'function' && !selling) || `entrada ${!!entry}, vendendo ${selling}`;
    });
    check('loja: o Mercador Viajante não ganha o pilar', () => {
        const store = Terraria.InventoryStorage.new();
        store['void .ctor()']();
        store['void SetupShop(int type)'](19);
        const shop = NPCShop.get(NPCID.TravellingMerchant);
        return !shop || !shop.Entries.some((e) => e.type === I('ExamplePylonItem')) || 'tem o pilar';
    });
}

// ---- tile entity ----
let barrel = null;
function barrelTests() {
    const { floor, barrel: bx } = area();
    check('barril: colocado pelo jogador, com a BasicTileEntity vazia', () => {
        const placed = placeAsPlayer(bx, floor - 1, barrelTile.Type);
        if (placed !== barrelTile.Type) return 'colocou ' + placed;
        barrel = ModTileEntity.TryGet(bx, floor - 1);
        return (barrel && barrel.constructor.name === 'BasicTileEntity' && barrel.WaterFillPercentage === 0) || 'entidade ' + (barrel && barrel.constructor.name);
    });
}

let rainStart = 0, wasRaining = false;
// Chuva de verdade: sem rainTime, o jogo a desliga antes do Update das tile
// entities.
let oldRainTime = 0;
function barrelRain() {
    wasRaining = Main.raining;
    oldRainTime = Main.rainTime;
    rainStart = barrel ? barrel.WaterFillLevel : 0;
    Main.rainTime = 3600;
    Main.raining = true;
}

function barrelAfterRain() {
    const { floor, barrel: bx } = area();
    Main.raining = wasRaining;
    Main.rainTime = oldRainTime;
    if (!barrel) return;
    check('barril: enche na chuva (Update)', () => barrel.WaterFillLevel - rainStart >= 20 || `${rainStart} -> ${barrel.WaterFillLevel}`);
    check('barril: o desenho segue o nível (SetDrawPositions)', () => {
        barrel.WaterFillLevel = BasicTileEntityMax();
        const frameY = new Ref(0);
        barrelTile.SetDrawPositions(bx, floor - 2, new Ref(16), new Ref(0), new Ref(16), new Ref(0), frameY);
        return frameY.value === 4 * 38 || 'quadro Y ' + frameY.value;
    });
    check('barril: cheio, o toque dá o balde, zera e o barril fica', () => {
        clearItems();
        barrelTile.RightClick(bx, floor - 1);
        const buckets = itemsNear(ItemID.WaterBucket, bx, 4);
        return (buckets === 1 && barrel.WaterFillLevel === 0 && bl.tiles.typeAt(bx, floor - 1) === barrelTile.Type) ||
            `baldes ${buckets}, nível ${barrel.WaterFillLevel}, bloco ${bl.tiles.typeAt(bx, floor - 1)}`;
    });
}
const BasicTileEntityMax = () => barrel.constructor.MaxFill;

function barrelGone() {
    const { floor, barrel: bx } = area();
    check('barril: quebrado, a entidade sai', () => {
        kill(bx, floor - 1);
        return (bl.tiles.typeAt(bx, floor - 1) < 0 && !ModTileEntity.TryGet(bx, floor - 1)) || 'ficou';
    });
    // O objeto quebrado solta o item mesmo com noItem na célula.
    clearItems();
}

// ---- árvore ----
function treeTop(x, floor) {
    let y = floor - 1;
    while (bl.tiles.typeAt(x, y - 1) === TileID.Trees) y--;
    return y;
}

function clearTree() {
    const { floor, tree: tx } = area();
    for (let i = tx - 3; i <= tx + 3; i++) for (let j = floor - 35; j < floor; j++) if (bl.tiles.typeAt(i, j) >= 0) kill(i, j);
}

function treeTests() {
    const { floor, tree: tx } = area();
    for (let i = tx - 2; i <= tx + 2; i++) { kill(i, floor); placeTile(i, floor, T('ExampleTile')); }
    check('árvore: a bolota no Bloco de Exemplo vira a ExampleSapling', () => {
        const placed = placeAsPlayer(tx, floor - 1, TileID.Saplings);
        return placed === T('ExampleSapling') || 'colocou ' + placed;
    });
    check('árvore: a muda cresce na ExampleTree, com copa', () => {
        let grew = false, leafy = false, tries = 0;
        while (!(grew && leafy) && tries < 20) {
            tries++;
            if (grew) clearTree();
            if (bl.tiles.typeAt(tx, floor - 1) !== T('ExampleSapling')) placeAsPlayer(tx, floor - 1, TileID.Saplings);
            grew = W['bool AttemptToGrowTreeFromSapling(int x, int y, bool underground, int treeHeightAddon, bool ignoreWalls)'](tx, floor - 1, false, 0, false);
            leafy = grew && W['bool IsTileALeafyTreeTop(int i, int j)'](tx, treeTop(tx, floor));
        }
        return (grew && leafy && bl.tiles.typeAt(tx, floor - 1) === TileID.Trees) || `cresceu ${grew}, copa ${leafy}, ${tries} tentativa(s)`;
    });
    check('árvore: a folha é a gore do mod, com o voo de folha (SpecialAI 3)', () => {
        const top = treeTop(tx, floor);
        const t = Main.tile['Tile get_Item(int x, int y)'];
        const pass = new Ref(0);
        W['void GetTreeLeaf(int x, Tile topTile, Tile t, ref int treeHeight, out int treeFrame, out int passStyle)'](tx, t(tx, top), t(tx, floor), new Ref(0), new Ref(0), pass);
        // A gore do mod (a folha de 1 coluna, 12 px) e não uma do jogo.
        const leaf = pass.value;
        const width = leaf > 0 ? Terraria.GameContent.TextureAssets.Gore[leaf].Value.Width : -1;
        return (leaf > 0 && width === 12 && Terraria.ID.GoreID.Sets.SpecialAI[leaf] === 3) ||
            `passStyle ${leaf}, largura ${width}, SpecialAI ${leaf > 0 ? Terraria.ID.GoreID.Sets.SpecialAI[leaf] : '-'}`;
    });
}

function treeShakeAndChop() {
    const { floor, tree: tx } = area();
    check('árvore: sacudir dá Bloco de Exemplo (Shake)', () => {
        clearItems();
        kill(tx, floor - 3, true, false);
        kill(tx, floor - 3, true, false);
        return itemsNear(I('ExampleTileItem'), tx, 6) >= 1 || 'sem bloco';
    });
    check('árvore: cortar dá Exemplo de Item, não madeira (DropWood)', () => {
        clearItems();
        kill(tx, floor - 1, false, false);
        const ex = itemsNear(I('ExampleItem'), tx, 8), wood = itemsNear(ItemID.Wood, tx, 8);
        return (ex > 0 && wood === 0) || `Exemplo de Item ${ex}, madeira ${wood}`;
    });
    clearTree();
    clearItems();
    check('árvore: o RandomUpdate da muda a faz crescer com o tempo', () => {
        const sapling = ModContent.Find(ModTile, EX('ExampleSapling'));
        let calls = 0;
        for (let round = 0; round < 10 && bl.tiles.typeAt(tx, floor - 1) !== TileID.Trees; round++) {
            clearTree();
            if (placeAsPlayer(tx, floor - 1, TileID.Saplings) !== T('ExampleSapling')) return 'não plantou';
            for (let k = 0; k < 200 && bl.tiles.typeAt(tx, floor - 1) === T('ExampleSapling'); k++) { sapling.RandomUpdate(tx, floor - 1); calls++; }
        }
        return bl.tiles.typeAt(tx, floor - 1) === TileID.Trees || `não cresceu em ${calls} chamadas`;
    });
    // Sem árvore no caminho do gancho.
    clearTree();
    clearItems();
}

// ---- barra de chefe ----
let boss = null;
function bossSpawn() {
    const p = me();
    const index = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.Center.X) + 10 * 16, Math.floor(p.Center.Y) - 6 * 16,
        ModContent.NPCType(EX('ExampleBoss')), 0, 0, 0, 0, 0, Main.myPlayer);
    boss = Main.npc[index];
    // A vida máxima muda com a dificuldade do mundo: as contas são relativas.
    boss.life = Math.floor(boss.lifeMax * 0.75);   // escudo: o que passa da metade
    bl.log(`exmodnew diag: chefe no índice ${index}, ativo ${boss.active}, tipo ${boss.type}, vida ${boss.life}/${boss.lifeMax}`);
}

function bossHold() {
    if (!boss || !boss.active) return;
    const p = me();
    boss.position = Vector2.new(p.Center.X + 10 * 16 - boss.width / 2, p.Center.Y - 6 * 16 - boss.height / 2);
    boss.velocity = Vector2.new(0, 0);
    boss.timeLeft = 750;
    p.immune = true;
    p.immuneTime = 30;
}

// A barra do Example Mod (a mesma instância que o chefe pede no SetDefaults).
const barOf = () => ModContent.GetContent(ModBossBar).find((b) => b.constructor.name === 'ExampleBossBar');
let firstHead = -1;
function bossCheck75() {
    bl.log(`exmodnew diag: chefe ativo ${boss && boss.active}, tipo ${boss && boss.type}, em ${boss && Math.round(boss.position.X)},${boss && Math.round(boss.position.Y)}; jogador ${Math.round(me().position.X)},${Math.round(me().position.Y)}`);
    check('chefe: a barra é a ExampleBossBar, com a vida e o escudo até a metade', () => {
        const bar = barOf(boss);
        if (!bar || bar.constructor.name !== 'ExampleBossBar') return 'barra ' + (bar && bar.constructor.name);
        firstHead = bar.bossHeadIndex;
        const half = boss.lifeMax / 2;
        return (bar.Life === boss.life && bar.LifeMax === boss.lifeMax && Math.abs(bar.Shield - (boss.life - half)) < 1 &&
                Math.abs(bar.ShieldMax - half) < 1 && firstHead >= 0) ||
            `vida ${bar.Life}/${bar.LifeMax} (NPC ${boss.life}/${boss.lifeMax}), escudo ${bar.Shield}/${bar.ShieldMax}, cabeça ${firstHead}`;
    });
    boss.life = Math.floor(boss.lifeMax * 0.45);   // abaixo da metade: a IA passa para a 2a fase
}

function bossCheck2nd() {
    check('chefe: na 2a fase, sem escudo e com a outra cabeça', () => {
        const bar = barOf(boss);
        return (bar && bar.Shield === 0 && bar.Life === boss.life && boss.ai[0] === 1 && bar.bossHeadIndex >= 0 && bar.bossHeadIndex !== firstHead) ||
            `escudo ${bar && bar.Shield}, vida ${bar && bar.Life}, fase ${boss.ai[0]}, cabeça ${bar && bar.bossHeadIndex} (antes ${firstHead})`;
    });
}

function bossGone() {
    if (boss && boss.active) boss.active = false;
    boss = null;
}

// ---- coleta ----
const defense = () => ModContent.BuffType(EX('ExampleDefenseBuff'));
const hasDefense = () => me().FindBuffIndex(defense()) >= 0;
function dropBuff(type) {
    const k = me().FindBuffIndex(type);
    if (k >= 0) me()['void DelBuff(int b)'](k);
}

let near = -1, far = -1;
function spawnPickup(dx) {
    const c = me().Center;
    return NewItem(null, Math.floor(c.X + dx), Math.floor(c.Y - 8), 0, 0, I('ExamplePickup'), 1, false, 0, 0, null, null);
}
const alive = (k) => k >= 0 && Main.item[k].active && Main.item[k].type === I('ExamplePickup');

function pickupNear() {
    dropBuff(defense());
    near = spawnPickup(0);
}

function pickupNearCheck() {
    check('coleta: pega, some e dá a Defesa (OnPickup false)', () => {
        let inBag = 0;
        for (let k = 0; k < 59; k++) if (me().inventory[k].type === I('ExamplePickup')) inBag++;
        const k = me().FindBuffIndex(defense());
        return (!alive(near) && k >= 0 && me().buffTime[k] > 500 && inBag === 0) || `no chão ${alive(near)}, buff ${k}, no inventário ${inBag}`;
    });
    check('coleta: a dica da Defesa ganha o tempo restante (ModifyBuffText)', () => {
        const tip = Main['string GetBuffTooltip(Player player, int buffType)'](me(), defense());
        bl.log('exmodnew diag: dica "' + tip.replace(/\n/g, ' | ') + '"');
        const lines = tip.split('\n');
        return (lines.length >= 2 && /\d/.test(lines[lines.length - 1])) || 'dica "' + tip + '"';
    });
    dropBuff(defense());
    far = spawnPickup(200);   // a 200 px: longe do alcance do jogo (42)
}

function pickupFarControl() {
    check('coleta: a 200 px, sem a Poção de Alcance Cardíaco, fica no chão', () => (alive(far) && !hasDefense()) || `no chão ${alive(far)}, buff ${hasDefense()}`);
    me()['void AddBuff(int type, int time, bool fromNetPvP)'](BuffID.Heartreach, 600, false);
}

function pickupFarMagnet() {
    check('coleta: com a Poção de Alcance Cardíaco, vem de 200 px (GrabRange +250)', () => (!alive(far) && hasDefense()) || `no chão ${alive(far)}, buff ${hasDefense()}`);
    dropBuff(BuffID.Heartreach);
    dropBuff(defense());
    if (alive(far)) Main.item[far]['void TurnToAir()']();
}

// ---- espada curta ----
let stab = null, stabSamples = [], stabSawDeath = -1, stabFrames = 0;
function stabStart() {
    const c = me().Center;
    const k = newProjectile(null, c.X, c.Y, 2.1, 0, P('ExampleShortswordProjectile'), 8, 4, Main.myPlayer, 0, 0, 0, null);
    stab = Main.projectile[k];
}

function stabWatch() {
    if (!stab) return;
    stabFrames++;
    if (!stab.active || stab.type !== P('ExampleShortswordProjectile')) {
        if (stabSawDeath < 0) stabSawDeath = stabFrames;
        return;
    }
    const p = me();
    const hand = p['Vector2 RotatedRelativePoint(Vector2 pos, bool reverseRotation, bool addGfxOffY, int pushFromOriginHack)'](p.MountedCenter, false, true, 0);
    const timer = stab.ai[0];
    if (timer < 1) return;
    const ex = hand.X + stab.velocity.X * (timer - 1), ey = hand.Y + stab.velocity.Y * (timer - 1);
    stabSamples.push({ timer, off: Math.hypot(stab.Center.X - ex, stab.Center.Y - ey), opacity: stab.Opacity });
}

function stabCheck() {
    check('espada curta: a posição é a da IA, sem a velocidade somada (ShouldUpdatePosition)', () => {
        const worst = Math.max(...stabSamples.map((s) => s.off));
        return (stabSamples.length >= 3 && worst < 0.5) || `${stabSamples.length} amostras, maior desvio ${worst.toFixed(2)} ` + JSON.stringify(stabSamples.slice(0, 3));
    });
    check('espada curta: some em 16 atualizações (8 quadros com extraUpdates 1)', () => (stabSawDeath > 0 && stabSawDeath <= 10) || 'sumiu no quadro ' + stabSawDeath);
}

// ---- gancho ----
let hook = null, hookRetract = null;
function hookFire(vx, vy) {
    const c = me().Center;
    const k = newProjectile(null, c.X, c.Y - 8, vx, vy, P('ExampleHookProjectile'), 0, 0, Main.myPlayer, 0, 0, 0, null);
    return Main.projectile[k];
}

function hookWatch() {
    if (!hook || !hook.active || hookRetract || hook.ai[0] !== 1) return;
    const c = me().MountedCenter;
    hookRetract = { at: Math.hypot(c.X - hook.Center.X, c.Y - hook.Center.Y), speed: 0, p: hook };
}

function hookSpeed() {
    if (hookRetract && hookRetract.p.active) hookRetract.speed = Math.max(hookRetract.speed, Math.hypot(hookRetract.p.velocity.X, hookRetract.p.velocity.Y));
}

function killHooks() {
    for (let k = 0; k < 1000; k++) {
        const q = Main.projectile[k];
        if (q.active && q.type === P('ExampleHookProjectile')) q['void Kill()']();
    }
}

function hookRangeCheck() {
    check('gancho: no ar, volta perto de 500 px (GrappleRange)', () => (hookRetract && hookRetract.at >= 480 && hookRetract.at <= 540) || JSON.stringify(hookRetract && { at: hookRetract.at, speed: hookRetract.speed }));
    check('gancho: a volta corre a 18 (GrappleRetreatSpeed)', () => (hookRetract && Math.abs(hookRetract.speed - 18) < 0.6) || 'velocidade ' + (hookRetract && hookRetract.speed));
    killHooks();
}

function hookCeilingCheck() {
    const latched = bl.projectiles.latchedHooks(Main.myPlayer, -1).filter((h) => h.type === P('ExampleHookProjectile'));
    check('gancho: três no teto, só dois presos (NumGrappleHooks 2)', () => latched.length === 2 || latched.length + ' preso(s)');
    killHooks();
}

// ---- roteiro ----
// O que o teste pegou do chão (o balde, que cai perto) sai do inventário.
let bag = null;
const snapshotBag = () => { bag = []; for (let k = 0; k < 59; k++) bag.push({ type: me().inventory[k].type, stack: me().inventory[k].stack }); };
function restoreBag() {
    if (!bag) return;
    for (let k = 0; k < 59; k++) {
        const it = me().inventory[k], was = bag[k];
        if (it.type === was.type && it.stack === was.stack) continue;
        if (was.type === 0) it['void TurnToAir()']();
        else if (it.type === was.type) it.stack = was.stack;
    }
}

function finish() {
    bossGone();
    killHooks();
    clearArea();
    clearItems();
    restoreBag();
    dropBuff(defense());
    const p = me();
    p.position = Vector2.new(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
    p.velocity = Vector2.new(0, 0);
    p.fallStart = Main.spawnTileY - 3;
    done = true;
    bl.log('exmodnew FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

const S = 60;
let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    self.statLife = self.statLifeMax2;
    if (++frames < S) return;
    const f = frames - S;

    try {
        if (f === 0) {
            const { x0, floor, ceiling, stand: sx } = area();
            standX = sx;
            // Sobra de uma rodada antiga deste teste (o barril quebrado solta o
            // item e o jogador pegava): o item é novo, só pode ter vindo daqui.
            const modItems = [];
            for (let k = 0; k < 59; k++) if (me().inventory[k].type >= bl.items.vanillaCount) modItems.push(k + ':' + me().inventory[k].type);
            bl.log(`exmodnew diag: itens de mod no inventário ${modItems.join(' ') || 'nenhum'} (barril ${I('BasicTileEntityItem')}); casas 0-9: ${[...Array(10).keys()].map((k) => me().inventory[k].type).join(',')}`);
            for (let k = 0; k < 59; k++) if (me().inventory[k].type === I('BasicTileEntityItem')) me().inventory[k]['void TurnToAir()']();
            snapshotBag();
            clearArea();
            clearItems();
            for (let k = x0 - 20; k <= x0 + 40; k++) { placeTile(k, floor, TileID.Stone); placeTile(k, ceiling, TileID.Stone); }
            check('preparo', types);
            if (!pylon || !barrelTile) { finish(); return; }
            check('pilar', pylonTests);
            check('barril', barrelTests);
            check('árvore', treeTests);
        }
        stand();
        bossHold();
        stabWatch();
        hookWatch();
        hookSpeed();

        if (f === 240) bl.log('exmodnew pose objetos');
        if (f === 300) barrelRain();
        if (f === 340) barrelAfterRain();
        if (f === 350) treeShakeAndChop();
        if (f === 360) barrelGone();
        if (f === 370) bossSpawn();
        // A barra com o escudo (1a fase) fica uns segundos na tela, para a captura.
        if (f === 400) bl.log('exmodnew pose chefe');
        if (f === 800) bossCheck75();
        const B = 400;
        if (f === 520 + B) bossCheck2nd();
        if (f === 530 + B) bossGone();
        if (f === 540 + B) pickupNear();
        if (f === 570 + B) pickupNearCheck();
        if (f === 630 + B) pickupFarControl();
        if (f === 700 + B) pickupFarMagnet();
        if (f === 710 + B) stabStart();
        if (f === 730 + B) stabCheck();
        if (f === 740 + B) hook = hookFire(14, 0);
        if (f === 820 + B) hookRangeCheck();
        if (f === 830 + B) hookFire(-3, -14);
        if (f === 850 + B) hookFire(0, -14);
        if (f === 870 + B) hookFire(3, -14);
        if (f === 920 + B) hookCeilingCheck();
        if (f === 930 + B) finish();
    } catch (e) {
        check('roteiro no quadro ' + f, () => { throw e; });
        finish();
    }
});
bl.log('exmodnew: carregado');

export default class TestExModNew extends Mod {}
