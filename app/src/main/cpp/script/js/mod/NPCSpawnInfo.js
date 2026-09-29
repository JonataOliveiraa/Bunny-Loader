// O que o SpawnChance, o EditSpawnPool e o EditSpawnInfo recebem, como o
// NPC.Spawner do tModLoader: o ponto do spawn (SpawnTileX/Y, GroundTileY e o
// bloco e a parede dele), o jogador-alvo (Player, o certo no multijogador) e
// os campos do NPC.Spawner do jogo (waterTile, nearGranite, ZoneCorrupt...),
// que se leem e se escrevem direto: o EditSpawnInfo muda o que o jogo usa.
// Mais os atalhos de altura, hora e bioma abaixo.
class NPCSpawnInfo {
    // Os campos de instância do NPC.Spawner do celular (1.4.5).
    static FIELDS = [
        'numberOfActivePlayers', 'reachedInvasionBossCap', 'pX', 'pY', 'luck', 'dayTime', 'raining',
        'townNPCs', 'skyMob', 'noWorms', 'noGroundWorms', 'invaders', 'spawnFriendly', 'ignoreSafeWalls',
        'waterTile', 'nearGranite', 'nearMarble', 'spawnSpider', 'surfaceSpawn', 'spawnUndergroundDesert',
        'hardDungeon', 'deeperThanRockLayer', 'underGround', 'isOcean', 'isBeach', 'isSpawningInWindDirection',
        'skyBehindPlayer', 'livingTree', 'dualDungeonsSpawnRules', 'inDualDungeon', 'tresspassingDualDungeon',
        'inRemixStartingArea', 'offensiveToTim', 'playerHasStartingHealth', 'ZoneCorrupt', 'ZoneCrimson',
        'ZoneHallow', 'ZoneJungle', 'ZoneSnow', 'ZoneGlowshroom', 'ZoneMeteor', 'ZoneGraveyard', 'ZoneDungeon',
        'ZoneLihzhardTemple', 'ZoneGranite', 'ZoneMarble', 'ZoneSandstorm', 'ZoneTowerSolar', 'ZoneTowerVortex',
        'ZoneTowerNebula', 'ZoneTowerStardust', 'ZoneOldOneArmy', 'ZoneWaterCandle', 'ZonePeaceCandle',
        'ZoneShadowCandle', 'defaultTarget',
    ];

    // spawner: o NPC.Spawner do jogo; tileX/tileY: o ponto do spawn (-1 no
    // EditSpawnFlags, antes de o jogo escolher); target: o índice do jogador.
    // xRange: longe o bastante na horizontal (SafeRangeX).
    constructor(spawner, tileX, tileY, target, xRange = false) {
        this.Spawner = spawner;
        this.SpawnTileX = tileX;
        this.SpawnTileY = tileY;
        this.SafeRangeX = !!xRange;
        this.Player = Terraria.Main.player[target >= 0 && target < 255 ? target : Terraria.Main.myPlayer];
        this.GroundTileY = -1;
        this.SpawnTileType = -1;
        this.SpawnWallType = -1;
        if (tileX < 0 || tileY < 0) return;

        const ground = new Ref(tileY);
        Terraria.NPC.Spawner['void FindGroundTile(int x, int y, out int groundTileY)'](tileX, tileY, ground);
        this.GroundTileY = ground.value;
        this.SpawnTileType = bl.tiles.typeAt(tileX, this.GroundTileY);
        this.SpawnWallType = Terraria.NPC.Spawner['int GetSpawnWallType(int spawnTileX, int spawnTileY)'](tileX, tileY);
    }

    get Sky() { return this.Player.ZoneSkyHeight; }
    get Surface() { return this.Player.ZoneOverworldHeight; }
    get Underground() { return this.Player.ZoneDirtLayerHeight; }
    get Cavern() { return this.Player.ZoneRockLayerHeight; }
    get Underworld() { return this.Player.ZoneUnderworldHeight; }
    get AboveSurface() { return this.Surface || this.Sky; }
    get BelowSurface() { return !this.AboveSurface && !this.Underworld; }

    get Day() { return Terraria.Main.dayTime; }
    get Night() { return !Terraria.Main.dayTime; }
    get Rain() { return Terraria.Main.raining; }
    get SlimeRain() { return Terraria.Main.slimeRain; }
    get BloodMoon() { return Terraria.Main.bloodMoon; }
    get SolarEclipse() { return Terraria.Main.eclipse; }
    get PumpkinMoon() { return Terraria.Main.pumpkinMoon; }
    get FrostMoon() { return Terraria.Main.snowMoon; }
    get AnyEvent() { return this.SlimeRain || this.SolarEclipse || this.PumpkinMoon || this.FrostMoon; }

    get HardMode() { return Terraria.Main.hardMode; }
    get Expert() { return Terraria.Main.expertMode; }
    get Master() { return Terraria.Main.masterMode; }

    get Corruption() { return this.Player.ZoneCorrupt && this.AboveSurface; }
    get UndergroundCorruption() { return this.Player.ZoneCorrupt && (this.Underground || this.Cavern); }
    get Crimson() { return this.Player.ZoneCrimson && this.AboveSurface; }
    get UndergroundCrimson() { return this.Player.ZoneCrimson && (this.Underground || this.Cavern); }
    get Hallow() { return this.Player.ZoneHallow && this.AboveSurface; }
    get UndergroundHallow() { return this.Player.ZoneHallow && (this.Underground || this.Cavern); }
    get Snow() { return this.Player.ZoneSnow && this.AboveSurface; }
    get Ice() { return this.Player.ZoneSnow && (this.Underground || this.Cavern); }
    get Jungle() { return this.Player.ZoneJungle && this.AboveSurface; }
    get UndergroundJungle() { return this.Player.ZoneJungle && (this.Underground || this.Cavern); }
    get SurfaceMushroom() { return this.Player.ZoneGlowshroom && this.Surface; }
    get Mushroom() { return this.Player.ZoneGlowshroom && this.Cavern; }
    get Ocean() { return this.Player.ZoneBeach; }
    get Meteor() { return this.Player.ZoneMeteor; }
    get Desert() { return this.Player.ZoneDesert; }
    get DesertCave() { return this.Player.ZoneUndergroundDesert; }
    get Marble() { return this.Player.ZoneMarble; }
    get Granite() { return this.Player.ZoneGranite; }
    get Graveyard() { return this.Player.ZoneGraveyard; }
    get Dungeon() { return this.Player.ZoneDungeon; }
    get Lihzahrd() { return this.Player.ZoneLihzhardTemple; }

    get Invasion() { return Terraria.Main.invasionType > 0; }
    get CommonEnemy() { return !this.Invasion && !this.AnyEvent && !this.AnyTower; }
    get AnyTower() {
        const p = this.Player;
        return p.ZoneTowerSolar || p.ZoneTowerVortex || p.ZoneTowerNebula || p.ZoneTowerStardust;
    }
}

for (const name of NPCSpawnInfo.FIELDS) {
    Object.defineProperty(NPCSpawnInfo.prototype, name, {
        get() { return this.Spawner[name]; },
        set(v) { this.Spawner[name] = v; },
    });
}
