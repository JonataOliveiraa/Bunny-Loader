import { ExampleBiomeTileCount } from '../../Common/Systems/ExampleBiomeTileCount.js';
import { ExampleUndergroundBackgroundStyle } from './ExampleUndergroundBackgroundStyle.js';

// O bioma subterrâneo do tModLoader: as mesmas condições do de superfície, na
// camada de terra ou de pedra, com o fundo de subsolo do exemplo.
export class ExampleUndergroundBiome extends ModBiome {
    get UndergroundBackgroundStyle() { return ModContent.GetInstance(ExampleUndergroundBackgroundStyle); }

    SetStaticDefaults() {
        this.Music = MusicLoader.GetMusicSlot('Music/Ropocalypse2');
    }

    IsBiomeActive(player) {
        return (player.ZoneRockLayerHeight || player.ZoneDirtLayerHeight) &&
            ModContent.GetInstance(ExampleBiomeTileCount).exampleBlockCount >= 40 &&
            Math.abs(Math.floor(player.position.X / 16) - Terraria.Main.maxTilesX / 2) < Terraria.Main.maxTilesX / 6;
    }
}
