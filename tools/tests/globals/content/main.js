// Etapa 6: GlobalItem, GlobalNPC e GlobalProjectile nas entidades do jogo
// (filtro, instancia por entidade, Clone), drops por tipo e globais (com o
// Bestiario), e o ModSystem: ciclo do mundo, atualizacao e dados salvos.
// Rodar duas vezes: a segunda carrega o que a primeira salvou no mundo.
const Main = Terraria.Main;
const { ItemID, NPCID, ProjectileID } = Terraria.ID;
const { ItemDropRule } = Terraria.GameContent.ItemDropRules;
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, ' +
                            'float ai0, float ai1, float ai2, float ai3, int Target)'];
const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, ' +
    'float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, ' +
    'NewProjectileModifier modifer)'];
const strike = 'double StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool noEffect, bool fromNet, int owner)';
const setDefaults = 'void SetDefaults(int Type, ItemVariant variant)';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('globals ' + label + ': ok');
        else { fails++; bl.log('globals ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('globals ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const spy = {
    swordDefaults: 0, inventory: 0,
    slimeDefaults: 0, slimeSpawn: 0, slimeAI: 0, slimeHit: 0, slimeKill: 0, lootTypes: [], globalLoot: 0,
    bunnyPreAI: 0, bunnyAI: 0, bunnyPostAI: 0,
    arrowDefaults: 0, arrowSpawn: 0, arrowAI: 0, arrowKill: 0,
    modLoad: 0, setup: 0, clear: 0, worldLoad: 0, loadCalls: 0, postLoad: 0, loaded: -1, hadKey: false, saves: 0,
    preWorld: 0, postWorld: 0, preTime: 0, postTime: 0, everything: 0,
};

// ------------------------------ itens ------------------------------

class ShortswordGlobal extends GlobalItem {
    AppliesToEntity(item, lateInstantiation) { return item.type === ItemID.CopperShortsword; }
    SetDefaults(item) {
        item.damage = 50;
        spy.swordDefaults++;
    }
}
GlobalItem.register(ShortswordGlobal);

class ChargeGlobal extends GlobalItem {
    InstancePerEntity = true;
    charge = 0;
    AppliesToEntity(item, lateInstantiation) { return item.type === ItemID.Gel; }
}
GlobalItem.register(ChargeGlobal);

class InventorySpy extends GlobalItem {
    UpdateInventory(item, player) { spy.inventory++; }
}
GlobalItem.register(InventorySpy);

// ------------------------------ NPCs ------------------------------

class SlimeGlobal extends GlobalNPC {
    get InstancePerEntity() { return true; }
    ticks = 0;
    AppliesToEntity(npc, lateInstantiation) { return npc.type === NPCID.BlueSlime; }
    SetDefaults(npc) {
        npc.lifeMax = 777;
        npc.life = 777;
        spy.slimeDefaults++;
    }
    OnSpawn(npc, source) { spy.slimeSpawn++; }
    AI(npc) {
        this.ticks++;
        spy.slimeAI++;
    }
    HitEffect(npc, hitDirection, damage) { spy.slimeHit++; }
    OnKill(npc) { spy.slimeKill++; }
    ModifyNPCLoot(npc, npcLoot) {
        npcLoot.Add(ItemDropRule.Common(ItemID.Dynamite, 1, 1, 1));
        spy.lootTypes.push(npc.netID);
    }
    ModifyGlobalLoot(globalLoot) {
        globalLoot.Add(ItemDropRule.Common(ItemID.Sapphire, 1000000, 1, 1));
        spy.globalLoot++;
    }
}
GlobalNPC.register(SlimeGlobal);

// PreAI false num Global corta o AI de todos (o do jogo e o dos outros).
class BunnyBlocker extends GlobalNPC {
    AppliesToEntity(npc, lateInstantiation) { return npc.type === NPCID.Bunny; }
    PreAI(npc) {
        spy.bunnyPreAI++;
        return false;
    }
}
GlobalNPC.register(BunnyBlocker);

class BunnySpy extends GlobalNPC {
    AppliesToEntity(npc, lateInstantiation) { return npc.type === NPCID.Bunny; }
    AI(npc) { spy.bunnyAI++; }
    PostAI(npc) { spy.bunnyPostAI++; }
}
GlobalNPC.register(BunnySpy);

// ---------------------------- projeteis ----------------------------

class ArrowGlobal extends GlobalProjectile {
    InstancePerEntity = true;
    born = false;
    AppliesToEntity(p, lateInstantiation) { return p.type === ProjectileID.WoodenArrowFriendly; }
    SetDefaults(p) {
        p.scale = 2;
        spy.arrowDefaults++;
    }
    OnSpawn(p, source) {
        this.born = true;
        spy.arrowSpawn++;
    }
    AI(p) { spy.arrowAI++; }
    OnKill(p, timeLeft) { spy.arrowKill++; }
}
GlobalProjectile.register(ArrowGlobal);

// ----------------------------- ModSystem -----------------------------

class WorldSpy extends ModSystem {
    OnModLoad() { spy.modLoad++; }
    PostSetupContent() { spy.setup++; }
    ClearWorld() { spy.clear++; }
    OnWorldLoad() { spy.worldLoad++; }
    LoadWorldData(tag) {
        spy.loadCalls++;
        spy.hadKey = tag.ContainsKey('runs');
        spy.loaded = tag.GetInt('runs');
    }
    PostWorldLoad() { spy.postLoad++; }
    SaveWorldData(tag) {
        tag.runs = spy.loaded + 1;
        tag.nested = { a: 1 };
        spy.saves++;
    }
    PreUpdateWorld() { spy.preWorld++; }
    PostUpdateWorld() { spy.postWorld++; }
    PreUpdateTime() { spy.preTime++; }
    PostUpdateTime() { spy.postTime++; }
    PostUpdateEverything() { spy.everything++; }
}
ModSystem.register(WorldSpy);
const modLoadAtRegister = spy.modLoad;

// ------------------------------ checagens ------------------------------

function newItem(type) {
    const it = Terraria.Item.new();
    it['void .ctor()']();
    it[setDefaults](type, null);
    return it;
}

function debugSource() {
    const source = Terraria.DataStructures.EntitySource_DebugCommand.new();
    source['void .ctor()']();
    return source;
}

function hasDrop(rules, itemId) {
    return rules.some((r) => 'itemId' in r && r.itemId === itemId);
}

function bestiaryShows(npcId, itemId) {
    const entry = Main.BestiaryDB['BestiaryEntry FindEntryByNPCID(int npcNetId)'](npcId);
    if (!entry) return 'sem entrada';
    const info = entry.Info;
    for (let i = 0; i < info.Count; i++) {
        const el = info.get_Item(i);
        if (el.GetType().Name === 'ItemDropBestiaryInfoElement' && el._droprateInfo.itemId === itemId) return true;
    }
    return false;
}

let slime = null, bunny = null, arrow = null, arrowIndex = -1;
let frames = 0, done = false;

function stepItems() {
    check('ModSystem.OnModLoad no register', () => modLoadAtRegister === 1 || 'OnModLoad ' + modLoadAtRegister);
    check('ModSystem.PostSetupContent', () => spy.setup === 1 || 'setup ' + spy.setup);

    check('GlobalItem.SetDefaults so no filtrado (espada de cobre)', () => {
        const copper = newItem(ItemID.CopperShortsword);
        const iron = newItem(ItemID.IronShortsword);
        return (copper.damage === 50 && iron.damage !== 50 && spy.swordDefaults > 0) ||
            `cobre ${copper.damage}, ferro ${iron.damage}, chamadas ${spy.swordDefaults}`;
    });
    check('InstancePerEntity: cada item com a sua copia, e o Clone leva o estado', () => {
        const a = newItem(ItemID.Gel);
        const b = newItem(ItemID.Gel);
        a.GetGlobalItem(ChargeGlobal).charge = 5;
        const c = a.Clone();
        const ga = a.GetGlobalItem(ChargeGlobal), gc = c.GetGlobalItem(ChargeGlobal);
        const ok = ga.charge === 5 && b.GetGlobalItem('ChargeGlobal').charge === 0 && gc.charge === 5 && gc !== ga;
        a[setDefaults](ItemID.Gel, null);
        const reset = a.GetGlobalItem(ChargeGlobal).charge === 0;
        return (ok && reset) || `a ${ga.charge}, b ${b.GetGlobalItem(ChargeGlobal).charge}, clone ${gc.charge} (mesma? ${gc === ga}), apos SetDefaults ${a.GetGlobalItem(ChargeGlobal).charge}`;
    });
    check('GetGlobalItem de Global que nao se aplica lanca; TryGetGlobalItem da false', () => {
        const gel = newItem(ItemID.Gel);
        let threw = false;
        try { gel.GetGlobalItem(ShortswordGlobal); } catch (e) { threw = String(e).includes('nao se aplica'); }
        const found = gel.TryGetGlobalItem(ShortswordGlobal, new Ref(null));
        return (threw && found === false) || `lancou ${threw}, Try ${found}`;
    });
    check('TagCompound', () => {
        const t = TagCompound.from({ n: 3, s: 'x' });
        return (t.GetInt('n') === 3 && t.GetString('s') === 'x' && t.ContainsKey('n') && !t.ContainsKey('z') &&
                t.GetBool('z') === false && t.Count === 2 && JSON.stringify(t) === '{"n":3,"s":"x"}') || JSON.stringify(t);
    });
}

function stepLoot() {
    const db = Main.ItemDropsDB;
    const rulesOf = (id) => {
        const list = db['GetRulesForNPCID(int npcNetId, bool includeGlobalDrops)'](id, false);
        const out = [];
        for (let i = 0; i < list.Count; i++) out.push(list.get_Item(i));
        return out;
    };
    check('ModifyNPCLoot poe a regra no slime azul', () =>
        (hasDrop(rulesOf(NPCID.BlueSlime), ItemID.Dynamite) && spy.lootTypes.includes(NPCID.BlueSlime)) ||
        'tipos vistos ' + spy.lootTypes.length);
    check('ModifyNPCLoot respeita o AppliesToEntity (zumbi sem dinamite)', () =>
        !hasDrop(rulesOf(NPCID.Zombie), ItemID.Dynamite) || 'zumbi com dinamite');
    check('ModifyGlobalLoot poe a regra global', () => {
        const all = [];
        const g = db._globalEntries;
        for (let i = 0; i < g.Count; i++) all.push(g.get_Item(i));
        return (spy.globalLoot === 1 && hasDrop(all, ItemID.Sapphire)) || 'globalLoot ' + spy.globalLoot;
    });
    check('Bestiario mostra o drop novo do slime azul', () => bestiaryShows(NPCID.BlueSlime, ItemID.Dynamite));
    check('Example Mod (ExampleNPCLoot): o Demolidor solta dinamite', () =>
        hasDrop(rulesOf(NPCID.Demolitionist), ItemID.Dynamite) || 'sem dinamite');
    check('Example Mod (ExampleNPCLoot): o Diabinho de Fogo sem a Rosa de Obsidiana', () =>
        !hasDrop(rulesOf(NPCID.FireImp), ItemID.ObsidianRose) || 'ainda solta');
}

function stepSpawn() {
    const p = Main.player[Main.myPlayer];
    const x = Math.floor(p.position.X), y = Math.floor(p.position.Y);
    const si = newNpc(debugSource(), x + 160, y - 60, NPCID.BlueSlime, 0, 0, 0, 0, 0, 255);
    slime = Main.npc[si];
    const bi = newNpc(debugSource(), x - 160, y - 60, NPCID.Bunny, 0, 0, 0, 0, 0, 255);
    bunny = Main.npc[bi];
    arrowIndex = newProj(debugSource(), p.position.X, p.position.Y - 200, 0, -1, ProjectileID.WoodenArrowFriendly,
                         5, 0, Main.myPlayer, 0, 0, 0, null);
    arrow = Main.projectile[arrowIndex];
    check('GlobalNPC.SetDefaults e OnSpawn no slime', () =>
        (slime.lifeMax === 777 && spy.slimeSpawn === 1 && spy.slimeDefaults > 0) ||
        `vida ${slime.lifeMax}, spawn ${spy.slimeSpawn}, defaults ${spy.slimeDefaults}`);
    check('GlobalProjectile.SetDefaults e OnSpawn (instancia por entidade)', () =>
        (arrow.scale === 2 && spy.arrowSpawn === 1 && arrow.GetGlobalProjectile(ArrowGlobal).born === true) ||
        `escala ${arrow.scale}, spawn ${spy.arrowSpawn}`);
}

function stepRun() {
    check('GlobalNPC.AI roda, com o estado por NPC', () =>
        (spy.slimeAI > 0 && slime.GetGlobalNPC(SlimeGlobal).ticks > 0) || 'AI ' + spy.slimeAI);
    check('PreAI false corta o AI dos outros Globais, e o PostAI roda', () =>
        (spy.bunnyPreAI > 0 && spy.bunnyAI === 0 && spy.bunnyPostAI > 0) ||
        `PreAI ${spy.bunnyPreAI}, AI ${spy.bunnyAI}, PostAI ${spy.bunnyPostAI}`);
    check('GlobalProjectile.AI roda', () => spy.arrowAI > 0 || 'AI ' + spy.arrowAI);
    check('GlobalItem.UpdateInventory roda', () => spy.inventory > 0 || 'inventario ' + spy.inventory);

    slime.playerInteraction[Main.myPlayer] = true;
    slime[strike](99999, 0, 1, false, false, false, Main.myPlayer);
    check('GlobalNPC.HitEffect e OnKill', () =>
        (spy.slimeHit > 0 && spy.slimeKill === 1) || `hit ${spy.slimeHit}, kill ${spy.slimeKill}`);
    if (arrow.active) arrow['void Kill()']();
    check('GlobalProjectile.OnKill', () => spy.arrowKill === 1 || 'kill ' + spy.arrowKill);
    if (bunny.active) bunny.active = false;

    check('ModSystem: mundo carregado (ClearWorld, OnWorldLoad, LoadWorldData, PostWorldLoad)', () =>
        (spy.clear > 0 && spy.worldLoad > 0 && spy.loadCalls === 1 && spy.postLoad > 0) ||
        `clear ${spy.clear}, load ${spy.worldLoad}, data ${spy.loadCalls}, post ${spy.postLoad}`);
    check('ModSystem: atualizacao (mundo, tempo, tudo)', () =>
        (spy.preWorld > 0 && spy.postWorld > 0 && spy.preTime > 0 && spy.postTime > 0 && spy.everything > 0) ||
        JSON.stringify({ w: [spy.preWorld, spy.postWorld], t: [spy.preTime, spy.postTime], e: spy.everything }));
    bl.log('globals dados do mundo carregados: runs=' + spy.loaded + ' (tinha a chave: ' + spy.hadKey + ')');

    check('SaveWorldData grava ao lado do mundo', () => {
        Terraria.IO.WorldFile['void SaveWorld(bool useCloudSaving, bool resetTime, WorldFile.WorldSaveContext saveContext)'](false, false, 0);
        const file = Main.worldPathName + '.bl.json';
        const all = JSON.parse(bl.file.read(file) || '{}');
        const key = Object.keys(all).find((k) => k.endsWith('/WorldSpy'));
        const mine = key ? all[key] : undefined;
        return (spy.saves > 0 && mine && mine.runs === spy.loaded + 1 && mine.nested.a === 1) ||
            `saves ${spy.saves}, arquivo ${JSON.stringify(all)}`;
    });
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 60) {
        check('itens', stepItems);
        check('drops', stepLoot);
        check('nascer', stepSpawn);
    }
    if (frames === 100) {
        done = true;
        check('rodar', stepRun);
        bl.log('globals FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('globals: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestGlobals extends Mod {}
