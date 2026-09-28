// O fundo de subsolo do ExampleUndergroundBiome, como no tModLoader.
const BG = 'Assets/Textures/Backgrounds/';

export class ExampleUndergroundBackgroundStyle extends ModUndergroundBackgroundStyle {
    FillTextureArray(textureSlots) {
        for (let i = 0; i < 4; i++) {
            textureSlots[i] = BackgroundTextureLoader.GetBackgroundSlot(this.Mod, BG + 'ExampleBiomeUnderground' + i);
        }
    }
}
