// As condições do spawn natural do jogo em objetos, como o SpawnCondition do
// tModLoader (NPCSpawnHelper): a sequência de if/else do jogo virou uma árvore,
// e cada condição diz a fatia do sorteio do jogo que cairia nela (Chance, 0 a 1)
// no ponto do spawn atual. Uso, no SpawnChance de um ModNPC:
//     return SpawnCondition.OverworldNightMonster.Chance * 0.1;
// A árvore é avaliada uma vez por sorteio, só se algum mod ler Chance ou Active.
// Uma condição que lança (um membro do jogo que o celular não tem) vale false,
// com um aviso no log.
class SpawnCondition {
    static #roots = [];
    static #info = null;
    static #generation = 0;
    static #checked = -1;
    static #warned = new Set();

    #name = '';
    #test;
    #children = [];
    #blockWeight;
    #chance = 0;
    #active = false;
    WeightFunc = null;

    constructor(parent, test, blockWeight = 1) {
        if (typeof parent === 'function') {   // (test, blockWeight): raiz
            blockWeight = test === undefined ? 1 : test;
            test = parent;
            parent = null;
        }
        this.#test = test;
        this.#blockWeight = blockWeight;
        if (parent) parent.#children.push(this);
        else SpawnCondition.#roots.push(this);
    }

    get Chance() { SpawnCondition.#Ensure(); return this.#chance; }
    get Active() { SpawnCondition.#Ensure(); return this.#active; }
    get BlockWeight() { return this.#blockWeight; }

    // O spawnInfo do sorteio que começa (o SpawnLoader chama antes dos SpawnChance).
    static Begin(info) {
        SpawnCondition.#info = info;
        SpawnCondition.#generation++;
    }

    // Todas as condições no spawnInfo dado, na hora (para testes).
    static CheckAll(info) {
        SpawnCondition.Begin(info);
        SpawnCondition.#Ensure();
    }

    static #Ensure() {
        if (SpawnCondition.#checked === SpawnCondition.#generation || !SpawnCondition.#info) return;
        SpawnCondition.#checked = SpawnCondition.#generation;

        const all = [];
        const walk = (c) => { all.push(c); c.#children.forEach(walk); };
        SpawnCondition.#roots.forEach(walk);
        for (const c of all) { c.#chance = 0; c.#active = false; }

        const remaining = { value: 1 };
        for (const root of SpawnCondition.#roots) {
            root.#Check(SpawnCondition.#info, remaining);
            if (Math.abs(remaining.value) < 5e-6) break;
        }
    }

    #Check(info, remaining) {
        if (this.WeightFunc) this.#blockWeight = this.WeightFunc();
        this.#active = true;
        let passed = false;
        try {
            passed = !!this.#test(info);
        } catch (e) {
            if (!SpawnCondition.#warned.has(this)) {
                SpawnCondition.#warned.add(this);
                bl.log(`SpawnCondition.${this.#name}: ${e} (vale false)`);
            }
        }
        if (!passed) return;

        this.#chance = remaining.value * this.#blockWeight;
        const child = { value: this.#chance };
        for (const c of this.#children) {
            c.#Check(info, child);
            if (Math.abs(child.value) < 5e-6) break;
        }
        remaining.value -= this.#chance;
    }

    // Os nomes, para o aviso (depois de criar todas).
    static Name(all) {
        for (const [name, c] of Object.entries(all)) c.#name = name;
    }
}

// ---- as condições, na ordem e com os pesos do tModLoader ----
(() => {
    const Main = () => Terraria.Main;
    const N = () => Terraria.NPC;
    const ID = () => Terraria.ID;
    const T = (name) => Terraria.ID.TileID[name];
    const tile = (info) => Terraria.Main.tile['Tile get_Item(int x, int y)'](info.SpawnTileX, info.SpawnTileY);
    const tileType = (info) => bl.tiles.typeAt(info.SpawnTileX, info.SpawnTileY);
    const wall = (info) => tile(info).wall;
    const anyNPCs = (type) => Terraria.NPC['bool AnyNPCs(int Type)'](type);
    const solid = (x, y) => Terraria.WorldGen['bool SolidTile(int i, int j, bool noDoors)'](x, y, false);
    const waterSurface = (info) => {
        if (info.SafeRangeX) return false;
        for (let k = info.SpawnTileY - 1; k > info.SpawnTileY - 50; k--) {
            const t = Terraria.Main.tile['Tile get_Item(int x, int y)'](info.SpawnTileX, k);
            if (t.liquid === 0 && !solid(info.SpawnTileX, k) && !solid(info.SpawnTileX, k + 1) && !solid(info.SpawnTileX, k + 2)) return true;
        }
        return false;
    };
    const innerThird = (info) => Math.abs(info.SpawnTileX - Main().spawnTileX) < Math.trunc(Main().maxTilesX / 3);
    const outerThird = (info) => Math.abs(info.SpawnTileX - Main().spawnTileX) > Math.trunc(Main().maxTilesX / 3);
    const martianProbeHelper = (info) =>
        Math.abs(info.SpawnTileX - Math.trunc(Main().maxTilesX / 2)) / Math.trunc(Main().maxTilesX / 2) > 0.33 &&
        !Terraria.NPC['bool AnyDanger(bool quickBossNPCCheck, bool ignorePillarsAndMoonlordCountdown, bool ignoreInvasions)'](false, false, false);
    const grassy = (info) => { const t = tileType(info); return t === T('Grass') || t === T('HallowedGrass'); };
    const S = (a, b, c) => new SpawnCondition(a, b, c);

    const c = {};
    c.NebulaTower = S((info) => info.Player.ZoneTowerNebula);
    c.VortexTower = S((info) => info.Player.ZoneTowerVortex);
    c.StardustTower = S((info) => info.Player.ZoneTowerStardust);
    c.SolarTower = S((info) => info.Player.ZoneTowerSolar);
    c.Sky = S((info) => info.skyMob);
    c.Invasion = S((info) => info.invaders);
    c.GoblinArmy = S(c.Invasion, () => Main().invasionType === 1);
    c.FrostLegion = S(c.Invasion, () => Main().invasionType === 2);
    c.Pirates = S(c.Invasion, () => Main().invasionType === 3);
    c.MartianMadness = S(c.Invasion, () => Main().invasionType === 4);
    c.Bartender = S((info) => !N().savedBartender && Terraria.GameContent.Events.DD2Event.ReadyToFindBartender &&
        !anyNPCs(ID().NPCID.BartenderUnconscious) && !info.waterTile, 1 / 80);
    c.SpiderCave = S((info) => wall(info) === ID().WallID.SpiderUnsafe || info.spawnSpider);
    c.DesertCave = S((info) => {
        const conv = ID().WallID.Sets.Conversion;
        const w = wall(info);
        return (conv.HardenedSand[w] || conv.Sandstone[w] || info.spawnUndergroundDesert) &&
            Terraria.WorldGen['bool checkUnderground(int x, int y)'](info.SpawnTileX, info.SpawnTileY);
    });
    c.HardmodeJungleWater = S((info) => Main().hardMode && info.waterTile && info.Player.ZoneJungle, 2 / 3);
    c.HardmodeCrimsonWater = S((info) => Main().hardMode && info.waterTile && info.Player.ZoneCrimson, 8 / 9);
    c.Ocean = S((info) => info.waterTile && (info.SpawnTileX < 250 || info.SpawnTileX > Main().maxTilesX - 250) &&
        Main().tileSand[info.SpawnTileType] && info.SpawnTileY < Main().rockLayer);
    c.OceanAngler = S(c.Ocean, (info) => !N().savedAngler && !anyNPCs(ID().NPCID.SleepingAngler) && waterSurface(info));
    c.OceanMonster = S(c.Ocean, () => true);
    c.BeachAngler = S((info) => !info.waterTile && !N().savedAngler && !anyNPCs(ID().NPCID.SleepingAngler) &&
        (info.SpawnTileX < 340 || info.SpawnTileX > Main().maxTilesX - 340) && Main().tileSand[info.SpawnTileType] &&
        info.SpawnTileY < Main().worldSurface);
    c.JungleWater = S((info) => info.waterTile && info.SpawnTileType === T('JungleGrass'));
    c.CavePiranha = S((info) => info.waterTile && info.SpawnTileY > Main().rockLayer, 0.5);
    c.CaveJellyfish = S((info) => info.waterTile && info.SpawnTileY > Main().worldSurface, 1 / 3);
    c.WaterCritter = S((info) => info.waterTile, 0.25);
    c.CorruptWaterCritter = S(c.WaterCritter, (info) => info.Player.ZoneCorrupt);
    c.OverworldWaterCritter = S(c.WaterCritter, (info) => info.SpawnTileY < Main().worldSurface && info.SpawnTileY > 50 && Main().dayTime, 2 / 3);
    c.OverworldWaterSurfaceCritter = S(c.OverworldWaterCritter, waterSurface);
    c.OverworldUnderwaterCritter = S(c.OverworldWaterCritter, () => true);
    c.DefaultWaterCritter = S(c.WaterCritter, () => true);
    c.BoundCaveNPC = S((info) => !info.waterTile && info.SpawnTileY >= Main().rockLayer && info.SpawnTileY < Main().maxTilesY - 210, 1 / 20);
    c.TownCritter = S((info) => info.spawnFriendly);
    c.TownWaterCritter = S(c.TownCritter, (info) => info.waterTile);
    c.TownOverworldWaterCritter = S(c.TownWaterCritter, (info) => info.SpawnTileY < Main().worldSurface && info.SpawnTileY > 50 && Main().dayTime, 2 / 3);
    c.TownOverworldWaterSurfaceCritter = S(c.TownOverworldWaterCritter, waterSurface);
    c.TownOverworldUnderwaterCritter = S(c.TownOverworldWaterCritter, () => true);
    c.TownDefaultWaterCritter = S(c.TownWaterCritter, () => true);
    c.TownSnowCritter = S(c.TownCritter, (info) => info.SpawnTileType === T('SnowBlock') || info.SpawnTileType === T('IceBlock'));
    c.TownJungleCritter = S(c.TownCritter, (info) => info.SpawnTileType === T('JungleGrass'));
    c.TownGeneralCritter = S(c.TownCritter, (info) => info.SpawnTileType === T('Grass') || info.SpawnTileType === T('HallowedGrass') ||
        info.SpawnTileY > Main().worldSurface);
    c.Dungeon = S((info) => info.Player.ZoneDungeon);
    c.DungeonGuardian = S(c.Dungeon, () => !N().downedBoss3);
    c.DungeonNormal = S(c.Dungeon, () => true);
    c.Meteor = S((info) => info.Player.ZoneMeteor);
    c.OldOnesArmy = S((info) => Terraria.GameContent.Events.DD2Event.Ongoing && info.Player.ZoneOldOneArmy);
    c.FrostMoon = S((info) => info.SpawnTileY <= Main().worldSurface && !Main().dayTime && Main().snowMoon);
    c.PumpkinMoon = S((info) => info.SpawnTileY <= Main().worldSurface && !Main().dayTime && Main().pumpkinMoon);
    c.SolarEclipse = S((info) => info.SpawnTileY <= Main().worldSurface && Main().dayTime && Main().eclipse);
    c.HardmodeMushroomWater = S((info) => Main().hardMode && info.SpawnTileType === T('MushroomGrass') && info.waterTile);
    c.OverworldMushroom = S((info) => info.SpawnTileType === T('MushroomGrass') && info.SpawnTileY <= Main().worldSurface, 2 / 3);
    c.UndergroundMushroom = S((info) => info.SpawnTileType === T('MushroomGrass') && Main().hardMode && info.SpawnTileY >= Main().worldSurface, 2 / 3);
    c.CorruptWorm = S((info) => info.Player.ZoneCorrupt && !info.noWorms, 1 / 65);
    c.UndergroundMimic = S((info) => Main().hardMode && info.SpawnTileY > Main().worldSurface, 1 / 70);
    c.OverworldMimic = S((info) => Main().hardMode && wall(info) === ID().WallID.DirtUnsafe, 0.05);
    c.Wraith = S((info) => Main().hardMode && info.SpawnTileY <= Main().worldSurface && !Main().dayTime, 0.05);
    c.Wraith.WeightFunc = () => 1 - (Main().moonPhase === 4 ? 0.95 * 0.8 : 0.95);
    c.HoppinJack = S((info) => Main().hardMode && Main().halloween && info.SpawnTileY <= Main().worldSurface && !Main().dayTime, 0.1);
    c.DoctorBones = S((info) => info.SpawnTileType === T('JungleGrass') && !Main().dayTime, 0.002);
    c.LacBeetle = S((info) => info.SpawnTileType === T('JungleGrass') && info.SpawnTileY > Main().worldSurface, 1 / 60);
    const plain = (info) => !info.Player.ZoneSnow && !info.Player.ZoneCrimson && !info.Player.ZoneCorrupt && !info.Player.ZoneHallow;
    c.WormCritter = S((info) => info.SpawnTileY > Main().worldSurface && info.SpawnTileY < Main().maxTilesY - 210 &&
        plain(info) && !info.Player.ZoneJungle, 1 / 8);
    c.MouseCritter = S((info) => info.SpawnTileY > Main().worldSurface && info.SpawnTileY < Main().maxTilesY - 210 &&
        plain(info) && !info.Player.ZoneJungle, 1 / 13);
    c.SnailCritter = S((info) => info.SpawnTileY > Main().worldSurface && info.SpawnTileY < (Main().rockLayer + Main().maxTilesY) / 2 &&
        plain(info), 1 / 13);
    c.FrogCritter = S((info) => info.SpawnTileY < Main().worldSurface && info.Player.ZoneJungle, 1 / 9);
    c.HardmodeJungle = S((info) => info.SpawnTileType === T('JungleGrass') && Main().hardMode, 2 / 3);
    c.JungleTemple = S((info) => info.SpawnTileType === T('LihzahrdBrick') && info.ZoneLihzhardTemple);
    c.UndergroundJungle = S((info) => info.SpawnTileType === T('JungleGrass') && info.SpawnTileY > (Main().worldSurface + Main().rockLayer) / 2);
    c.SurfaceJungle = S((info) => info.SpawnTileType === T('JungleGrass'), 11 / 32);
    c.SandstormEvent = S((info) => Terraria.GameContent.Events.Sandstorm.Happening && info.Player.ZoneSandstorm &&
        ID().TileID.Sets.Conversion.Sand[info.SpawnTileType] &&
        Terraria.NPC.Spawner['bool Spawning_SandstoneCheck(int x, int y)'](info.SpawnTileX, info.SpawnTileY));
    c.Mummy = S((info) => Main().hardMode && info.SpawnTileType === T('Sand'), 1 / 3);
    c.DarkMummy = S((info) => Main().hardMode && (info.SpawnTileType === T('Ebonsand') || info.SpawnTileType === T('Crimsand')), 0.5);
    c.LightMummy = S((info) => Main().hardMode && info.SpawnTileType === T('Pearlsand'), 0.5);
    const hallowed = (info) => [T('Pearlsand'), T('Pearlstone'), T('HallowedGrass'), T('HallowedIce')].includes(info.SpawnTileType);
    c.OverworldHallow = S((info) => Main().hardMode && !info.waterTile && info.SpawnTileY < Main().rockLayer && hallowed(info));
    c.EnchantedSword = S((info) => !info.noWorms && Main().hardMode && !info.waterTile && info.SpawnTileY >= Main().rockLayer && hallowed(info), 0.02);
    c.Crimson = S((info) => (info.SpawnTileType === T('Crimtane') && info.Player.ZoneCrimson) ||
        [T('CrimsonGrass'), T('FleshIce'), T('Crimstone'), T('Crimsand')].includes(info.SpawnTileType));
    c.Corruption = S((info) => (info.SpawnTileType === T('Demonite') && info.Player.ZoneCorrupt) ||
        [T('CorruptGrass'), T('Ebonstone'), T('Ebonsand'), T('CorruptIce')].includes(info.SpawnTileType));
    c.Overworld = S((info) => info.SpawnTileY <= Main().worldSurface);
    c.IceGolem = S(c.Overworld, (info) => info.Player.ZoneSnow && Main().hardMode && Main().cloudAlpha > 0 && !anyNPCs(ID().NPCID.IceGolem), 0.05);
    c.RainbowSlime = S(c.Overworld, (info) => info.Player.ZoneHallow && Main().hardMode && Main().cloudAlpha > 0 && !anyNPCs(ID().NPCID.RainbowSlime), 0.05);
    c.AngryNimbus = S(c.Overworld, (info) => !info.Player.ZoneSnow && Main().hardMode && Main().cloudAlpha > 0 &&
        Terraria.NPC['int CountNPCS(int Type)'](ID().NPCID.AngryNimbus) < 2, 0.1);
    c.MartianProbe = S(c.Overworld, (info) => martianProbeHelper(info) && Main().hardMode && N().downedGolemBoss &&
        !anyNPCs(ID().NPCID.MartianProbe), 1 / 400);
    c.MartianProbe.WeightFunc = () => 1 - (N().downedMartians ? 399 / 400 : 399 / 400 * 0.99);
    c.OverworldDay = S(c.Overworld, () => Main().dayTime);
    c.OverworldDaySnowCritter = S(c.OverworldDay, (info) => innerThird(info) && (tileType(info) === T('SnowBlock') || tileType(info) === T('IceBlock')), 1 / 15);
    c.OverworldDayGrassCritter = S(c.OverworldDay, (info) => innerThird(info) && grassy(info), 1 / 15);
    c.OverworldDaySandCritter = S(c.OverworldDay, (info) => innerThird(info) && tileType(info) === T('Sand'), 1 / 15);
    c.OverworldMorningBirdCritter = S(c.OverworldDay, (info) => innerThird(info) && Main().time < 18000 && grassy(info), 0.25);
    c.OverworldDayBirdCritter = S(c.OverworldDay, (info) => innerThird(info) && (grassy(info) || tileType(info) === T('SnowBlock')), 1 / 15);
    c.KingSlime = S(c.OverworldDay, (info) => outerThird(info) && tileType(info) === T('Grass') && !anyNPCs(ID().NPCID.KingSlime), 1 / 300);
    c.OverworldDayDesert = S(c.OverworldDay, (info) => tileType(info) === T('Sand') && !info.waterTile, 0.2);
    c.GoblinScout = S(c.OverworldDay, (info) => outerThird(info), 1 / 15);
    // Como no tModLoader (que devolve 14/15 × 6/7 nesse caso, e não 1 - isso).
    c.GoblinScout.WeightFunc = () => (!N().downedGoblins && Terraria.WorldGen.shadowOrbSmashed ? 14 / 15 * (6 / 7) : 1 - 14 / 15);
    c.OverworldDayRain = S(c.OverworldDay, () => Main().raining, 2 / 3);
    c.OverworldDaySlime = S(c.OverworldDay, () => true);
    c.OverworldNight = S(c.Overworld, () => true);
    c.OverworldFirefly = S(c.OverworldNight, (info) => grassy(info), 0.1);
    c.OverworldFirefly.WeightFunc = () => 1 / N().fireFlyChance;
    c.OverworldNightMonster = S(c.OverworldNight, () => true);
    c.Underground = S((info) => info.SpawnTileY <= Main().rockLayer);
    c.Underworld = S((info) => info.SpawnTileY > Main().maxTilesY - 190);
    c.Cavern = S(() => true);

    SpawnCondition.Name(c);
    for (const [name, cond] of Object.entries(c)) {
        Object.defineProperty(SpawnCondition, name, { value: cond, enumerable: true });
    }
})();
