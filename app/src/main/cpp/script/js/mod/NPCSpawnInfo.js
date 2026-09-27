class NPCSpawnInfo {
    constructor(x, y, player) {
        this.SpawnTileX = Math.floor(x / 16);
        this.SpawnTileY = Math.floor(y / 16);
        this.Player = player;
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
