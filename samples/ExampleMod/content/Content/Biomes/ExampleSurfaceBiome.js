import { ExampleBiomeTileCount } from '../../Common/Systems/ExampleBiomeTileCount.js';
import { ExampleSurfaceBackgroundStyle } from './ExampleSurfaceBackgroundStyle.js';
import { ExampleWaterStyle } from './ExampleWaterStyle.js';

// O bioma de superfície do tModLoader: 40 ExampleTile por perto, no terço do
// meio do mapa, no céu ou na superfície: a música, o fundo e a água dele. No
// tModLoader ele também troca as cores do mapa; aqui, ainda não.
export class ExampleSurfaceBiome extends ModBiome {
    get SurfaceBackgroundStyle() { return ModContent.GetInstance(ExampleSurfaceBackgroundStyle); }
    get WaterStyle() { return ModContent.GetInstance(ExampleWaterStyle); }

    SetStaticDefaults() {
        // A única música do mod de exemplo (o tModLoader usa MysteriousMystery).
        this.Music = MusicLoader.GetMusicSlot('Music/Ropocalypse2');
        this.Priority = SceneEffectPriority.BiomeLow;
    }

    IsBiomeActive(player) {
        const enough = ModContent.GetInstance(ExampleBiomeTileCount).exampleBlockCount >= 40;
        const middle = Math.abs(Math.floor(player.position.X / 16) - Terraria.Main.maxTilesX / 2) < Terraria.Main.maxTilesX / 6;
        const surface = player.ZoneSkyHeight || player.ZoneOverworldHeight;
        return enough && middle && surface;
    }
}
