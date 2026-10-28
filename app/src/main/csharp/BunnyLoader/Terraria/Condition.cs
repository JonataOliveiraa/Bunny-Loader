using System;
using System.Text.RegularExpressions;
using Terraria.ID;
using Terraria.Localization;

namespace Terraria;

public sealed record Condition(LocalizedText Description, Func<bool> Predicate)
{
    public Condition(string LocalizationKey, Func<bool> Predicate) : this(Language.GetOrRegister(LocalizationKey, () => Readable(LocalizationKey)), Predicate) { }

    public bool IsMet() => Predicate();

    static string Readable(string key) => Regex.Replace(key[(key.LastIndexOf('.') + 1)..], "([a-z0-9])([A-Z])", "$1 $2");

    static Player Me => Main.LocalPlayer;

    public static readonly Condition NearWater = new("Conditions.NearWater", () => Me.adjWaterSource || Me.adjTile[TileID.Sinks]);
    public static readonly Condition NearLava = new("Conditions.NearLava", () => Me.adjLava);
    public static readonly Condition NearHoney = new("Conditions.NearHoney", () => Me.adjHoney);
    public static readonly Condition TimeDay = new("Conditions.TimeDay", () => Main.dayTime);
    public static readonly Condition TimeNight = new("Conditions.TimeNight", () => !Main.dayTime);
    public static readonly Condition InDungeon = new("Conditions.InDungeon", () => Me.ZoneDungeon);
    public static readonly Condition InCorrupt = new("Conditions.InCorrupt", () => Me.ZoneCorrupt);
    public static readonly Condition InHallow = new("Conditions.InHallow", () => Me.ZoneHallow);
    public static readonly Condition InMeteor = new("Conditions.InMeteor", () => Me.ZoneMeteor);
    public static readonly Condition InJungle = new("Conditions.InJungle", () => Me.ZoneJungle);
    public static readonly Condition InSnow = new("Conditions.InSnow", () => Me.ZoneSnow);
    public static readonly Condition InCrimson = new("Conditions.InCrimson", () => Me.ZoneCrimson);
    public static readonly Condition InWaterCandle = new("Conditions.InWaterCandle", () => Me.ZoneWaterCandle);
    public static readonly Condition InPeaceCandle = new("Conditions.InPeaceCandle", () => Me.ZonePeaceCandle);
    public static readonly Condition InTowerSolar = new("Conditions.InTowerSolar", () => Me.ZoneTowerSolar);
    public static readonly Condition InTowerVortex = new("Conditions.InTowerVortex", () => Me.ZoneTowerVortex);
    public static readonly Condition InTowerNebula = new("Conditions.InTowerNebula", () => Me.ZoneTowerNebula);
    public static readonly Condition InTowerStardust = new("Conditions.InTowerStardust", () => Me.ZoneTowerStardust);
    public static readonly Condition InDesert = new("Conditions.InDesert", () => Me.ZoneDesert);
    public static readonly Condition InGlowshroom = new("Conditions.InGlowshroom", () => Me.ZoneGlowshroom);
    public static readonly Condition InUndergroundDesert = new("Conditions.InUndergroundDesert", () => Me.ZoneUndergroundDesert);
    public static readonly Condition InSkyHeight = new("Conditions.InSkyHeight", () => Me.ZoneSkyHeight);
    public static readonly Condition InSpace = InSkyHeight;
    public static readonly Condition InOverworldHeight = new("Conditions.InOverworldHeight", () => Me.ZoneOverworldHeight);
    public static readonly Condition InDirtLayerHeight = new("Conditions.InDirtLayerHeight", () => Me.ZoneDirtLayerHeight);
    public static readonly Condition InRockLayerHeight = new("Conditions.InRockLayerHeight", () => Me.ZoneRockLayerHeight);
    public static readonly Condition InUnderworldHeight = new("Conditions.InUnderworldHeight", () => Me.ZoneUnderworldHeight);
    public static readonly Condition InUnderworld = InUnderworldHeight;
    public static readonly Condition InBeach = new("Conditions.InBeach", () => Me.ZoneBeach);
    public static readonly Condition InRain = new("Conditions.InRain", () => Me.ZoneRain);
    public static readonly Condition InSandstorm = new("Conditions.InSandstorm", () => Me.ZoneSandstorm);
    public static readonly Condition InOldOneArmy = new("Conditions.InOldOneArmy", () => Me.ZoneOldOneArmy);
    public static readonly Condition InGranite = new("Conditions.InGranite", () => Me.ZoneGranite);
    public static readonly Condition InMarble = new("Conditions.InMarble", () => Me.ZoneMarble);
    public static readonly Condition InHive = new("Conditions.InHive", () => Me.ZoneHive);
    public static readonly Condition InGemCave = new("Conditions.InGemCave", () => Me.ZoneGemCave);
    public static readonly Condition InLihzhardTemple = new("Conditions.InLihzardTemple", () => Me.ZoneLihzhardTemple);
    public static readonly Condition InGraveyard = new("Conditions.InGraveyard", () => Me.ZoneGraveyard);
    public static readonly Condition InAether = new("Conditions.InAether", () => Me.ZoneShimmer);
    public static readonly Condition InShoppingZoneForest = new("Conditions.InShoppingForest", () => Me.ShoppingZone_Forest);
    public static readonly Condition InEvilBiome = new("Conditions.InEvilBiome", () => Me.ZoneCrimson || Me.ZoneCorrupt);
    public static readonly Condition NotInEvilBiome = new("Conditions.NotInEvilBiome", () => !Me.ZoneCrimson && !Me.ZoneCorrupt);
    public static readonly Condition NotInHallow = new("Conditions.NotInHallow", () => !Me.ZoneHallow);
    public static readonly Condition NotInGraveyard = new("Conditions.NotInGraveyard", () => !Me.ZoneGraveyard);
    public static readonly Condition NotInUnderworld = new("Conditions.NotInUnderworld", () => !Me.ZoneUnderworldHeight);
    public static readonly Condition InClassicMode = new("Conditions.InClassicMode", () => !Main.expertMode);
    public static readonly Condition InExpertMode = new("Conditions.InExpertMode", () => Main.expertMode);
    public static readonly Condition InMasterMode = new("Conditions.InMasterMode", () => Main.masterMode);
    public static readonly Condition InJourneyMode = new("Conditions.InJourneyMode", () => Main.IsJourneyMode);
    public static readonly Condition Hardmode = new("Conditions.InHardmode", () => Main.hardMode);
    public static readonly Condition PreHardmode = new("Conditions.PreHardmode", () => !Main.hardMode);
    public static readonly Condition SmashedShadowOrb = new("Conditions.SmashedShadowOrb", () => WorldGen.shadowOrbSmashed);
    public static readonly Condition CrimsonWorld = new("Conditions.WorldCrimson", () => WorldGen.crimson);
    public static readonly Condition CorruptWorld = new("Conditions.WorldCorrupt", () => !WorldGen.crimson);
    public static readonly Condition DrunkWorld = new("Conditions.WorldDrunk", () => Main.drunkWorld);
    public static readonly Condition RemixWorld = new("Conditions.WorldRemix", () => Main.remixWorld);
    public static readonly Condition NotTheBeesWorld = new("Conditions.WorldNotTheBees", () => Main.notTheBeesWorld);
    public static readonly Condition ForTheWorthyWorld = new("Conditions.WorldForTheWorthy", () => Main.getGoodWorld);
    public static readonly Condition TenthAnniversaryWorld = new("Conditions.WorldAnniversary", () => Main.tenthAnniversaryWorld);
    public static readonly Condition DontStarveWorld = new("Conditions.WorldDontStarve", () => Main.dontStarveWorld);
    public static readonly Condition NoTrapsWorld = new("Conditions.WorldNoTraps", () => Main.noTrapsWorld);
    public static readonly Condition ZenithWorld = new("Conditions.WorldZenith", () => Main.zenithWorld);
}
