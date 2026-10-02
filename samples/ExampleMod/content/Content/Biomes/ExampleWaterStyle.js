import { ExampleWaterfallStyle } from './ExampleWaterfallStyle.js';

// A água do bioma de superfície, como o ExampleWaterStyle do tModLoader: as
// texturas ao lado deste arquivo (a superfície, _Block e _Slope), a
// cachoeira de exemplo, a gota de exemplo, a luz passando sem perda, a chuva
// própria (Content/Biomes/ExampleRain) e o cabelo branco na tintura de bioma. O
// respingo é um pó do jogo (o do tModLoader é um ModDust, que ainda não temos).
export class ExampleWaterStyle extends ModWaterStyle {
    ChooseWaterfallStyle() { return ModContent.GetInstance(ExampleWaterfallStyle).Slot; }

    GetSplashDust() { return Terraria.ID.DustID.BlueCrystalShard; }

    GetDropletGore() { return ModGore.getTypeByName('ExampleDroplet'); }

    LightColorMultiplier(r, g, b) {
        r.value = 1;
        g.value = 1;
        b.value = 1;
    }

    GetRainTexture() { return 'Content/Biomes/ExampleRain'; }

    BiomeHairColor() { return Color.White; }
}
