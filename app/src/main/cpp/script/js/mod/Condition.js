// Uma condição com descrição, como o Condition do tModLoader: o recipe.AddCondition
// usa. A descrição é uma chave de texto (do mod ou do jogo) ou o próprio texto.
class Condition {
    constructor(description, predicate) {
        if (typeof predicate !== 'function') throw new TypeError('new Condition(description, predicate): predicate must be a function');

        const key = description && typeof description === 'object' && typeof description.Key === 'string'
            ? description.Key : String(description);
        this.DescriptionKey = Condition.#FullKey(key);
        this.Predicate = predicate;
    }

    // Uma chave do próprio mod ('Conditions.LowHealth') vira a completa (Mods.<id>....).
    static #FullKey(key) {
        const entry = LocalizationLoader.CallerEntry();
        if (!entry) return key;

        const rel = LocalizationLoader.Relative(entry, key);
        return entry.paths.has(rel) ? entry.prefix + rel : key;
    }

    get Description() {
        const key = this.DescriptionKey;
        if (ModLocalization.Exists(key)) return ModLocalization.GetTextValue(key);

        const last = key.slice(key.lastIndexOf('.') + 1);
        return /^[A-Za-z]\w*$/.test(last) && key.includes('.') ? last.replace(/([a-z0-9])([A-Z])/g, '$1 $2') : key;
    }

    IsMet() {
        return !!this.Predicate();
    }

    toString() {
        return this.Description;
    }

    static #Me() {
        const Main = Terraria.Main;
        return Main.player[Main.myPlayer];
    }

    static #Of(key, predicate) {
        return Object.freeze(new Condition('Conditions.' + key, predicate));
    }

    static #Defaults() {
        const me = Condition.#Me;
        const main = () => Terraria.Main;
        const zone = (name) => () => !!me()[name];

        return {
            NearWater: Condition.#Of('NearWater', () => me().adjWaterSource || me().adjTile[Terraria.ID.TileID.Sinks]),
            NearLava: Condition.#Of('NearLava', zone('adjLava')),
            NearHoney: Condition.#Of('NearHoney', zone('adjHoney')),
            TimeDay: Condition.#Of('TimeDay', () => main().dayTime),
            TimeNight: Condition.#Of('TimeNight', () => !main().dayTime),
            InDungeon: Condition.#Of('InDungeon', zone('ZoneDungeon')),
            InCorrupt: Condition.#Of('InCorrupt', zone('ZoneCorrupt')),
            InHallow: Condition.#Of('InHallow', zone('ZoneHallow')),
            InMeteor: Condition.#Of('InMeteor', zone('ZoneMeteor')),
            InJungle: Condition.#Of('InJungle', zone('ZoneJungle')),
            InSnow: Condition.#Of('InSnow', zone('ZoneSnow')),
            InCrimson: Condition.#Of('InCrimson', zone('ZoneCrimson')),
            InWaterCandle: Condition.#Of('InWaterCandle', zone('ZoneWaterCandle')),
            InPeaceCandle: Condition.#Of('InPeaceCandle', zone('ZonePeaceCandle')),
            InDesert: Condition.#Of('InDesert', zone('ZoneDesert')),
            InGlowshroom: Condition.#Of('InGlowshroom', zone('ZoneGlowshroom')),
            InUndergroundDesert: Condition.#Of('InUndergroundDesert', zone('ZoneUndergroundDesert')),
            InSkyHeight: Condition.#Of('InSkyHeight', zone('ZoneSkyHeight')),
            InOverworldHeight: Condition.#Of('InOverworldHeight', zone('ZoneOverworldHeight')),
            InDirtLayerHeight: Condition.#Of('InDirtLayerHeight', zone('ZoneDirtLayerHeight')),
            InRockLayerHeight: Condition.#Of('InRockLayerHeight', zone('ZoneRockLayerHeight')),
            InUnderworldHeight: Condition.#Of('InUnderworldHeight', zone('ZoneUnderworldHeight')),
            InBeach: Condition.#Of('InBeach', zone('ZoneBeach')),
            InRain: Condition.#Of('InRain', zone('ZoneRain')),
            InSandstorm: Condition.#Of('InSandstorm', zone('ZoneSandstorm')),
            InOldOneArmy: Condition.#Of('InOldOneArmy', zone('ZoneOldOneArmy')),
            InGranite: Condition.#Of('InGranite', zone('ZoneGranite')),
            InMarble: Condition.#Of('InMarble', zone('ZoneMarble')),
            InHive: Condition.#Of('InHive', zone('ZoneHive')),
            InGemCave: Condition.#Of('InGemCave', zone('ZoneGemCave')),
            InLihzhardTemple: Condition.#Of('InLihzardTemple', zone('ZoneLihzhardTemple')),
            InGraveyard: Condition.#Of('InGraveyard', zone('ZoneGraveyard')),
            InAether: Condition.#Of('InAether', zone('ZoneShimmer')),
            InEvilBiome: Condition.#Of('InEvilBiome', () => me().ZoneCrimson || me().ZoneCorrupt),
            NotInEvilBiome: Condition.#Of('NotInEvilBiome', () => !me().ZoneCrimson && !me().ZoneCorrupt),
            NotInHallow: Condition.#Of('NotInHallow', () => !me().ZoneHallow),
            NotInGraveyard: Condition.#Of('NotInGraveyard', () => !me().ZoneGraveyard),
            NotInUnderworld: Condition.#Of('NotInUnderworld', () => !me().ZoneUnderworldHeight),
            InClassicMode: Condition.#Of('InClassicMode', () => !main().expertMode),
            InExpertMode: Condition.#Of('InExpertMode', () => main().expertMode),
            InMasterMode: Condition.#Of('InMasterMode', () => main().masterMode),
            InJourneyMode: Condition.#Of('InJourneyMode', () => main().IsJourneyMode),
            Hardmode: Condition.#Of('InHardmode', () => main().hardMode),
            PreHardmode: Condition.#Of('PreHardmode', () => !main().hardMode),
            SmashedShadowOrb: Condition.#Of('SmashedShadowOrb', () => Terraria.WorldGen.shadowOrbSmashed),
            CrimsonWorld: Condition.#Of('WorldCrimson', () => Terraria.WorldGen.crimson),
            CorruptWorld: Condition.#Of('WorldCorrupt', () => !Terraria.WorldGen.crimson),
            DrunkWorld: Condition.#Of('WorldDrunk', () => main().drunkWorld),
            RemixWorld: Condition.#Of('WorldRemix', () => main().remixWorld),
            NotTheBeesWorld: Condition.#Of('WorldNotTheBees', () => main().notTheBeesWorld),
            ForTheWorthyWorld: Condition.#Of('WorldForTheWorthy', () => main().getGoodWorld),
            TenthAnniversaryWorld: Condition.#Of('WorldAnniversary', () => main().tenthAnniversaryWorld),
            DontStarveWorld: Condition.#Of('WorldDontStarve', () => main().dontStarveWorld),
            NoTrapsWorld: Condition.#Of('WorldNoTraps', () => main().noTrapsWorld),
            ZenithWorld: Condition.#Of('WorldZenith', () => main().zenithWorld),
        };
    }

    static {
        const all = Condition.#Defaults();
        all.InSpace = all.InSkyHeight;
        all.InUnderworld = all.InUnderworldHeight;
        Object.assign(Condition, all);
    }
}
