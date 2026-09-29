import { ExampleWaterfallStyle } from './ExampleWaterfallStyle.js';

// A água do bioma de superfície, como o ExampleWaterStyle do tModLoader: as
// texturas em Assets/Textures/Biomes (a superfície, _Block e _Slope), a
// cachoeira de exemplo, a gota de exemplo e a luz passando sem perda. O
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

    GetRainVariant() { return Rand.Next(3); }
}
