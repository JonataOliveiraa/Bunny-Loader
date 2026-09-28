// O fundo de superfície do ExampleSurfaceBiome, como no tModLoader: longe, o
// meio animado em 4 quadros e a frente, com as texturas de
// Assets/Textures/Backgrounds.
const BG = 'Assets/Textures/Backgrounds/';

export class ExampleSurfaceBackgroundStyle extends ModSurfaceBackgroundStyle {
    static #frameCounter = 0;
    static #frame = 0;

    // Mantém as montanhas de longe do jogo sumindo e as deste estilo aparecendo.
    ModifyFarFades(fades, transitionSpeed) {
        for (let i = 0; i < fades.length; i++) {
            if (i === this.Slot) {
                fades[i] = Math.min(1, fades[i] + transitionSpeed);
            } else {
                fades[i] = Math.max(0, fades[i] - transitionSpeed);
            }
        }
    }

    ChooseFarTexture() {
        return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, BG + 'ExampleBiomeSurfaceFar');
    }

    ChooseMiddleTexture() {
        if (++ExampleSurfaceBackgroundStyle.#frameCounter > 12) {
            ExampleSurfaceBackgroundStyle.#frame = (ExampleSurfaceBackgroundStyle.#frame + 1) % 4;
            ExampleSurfaceBackgroundStyle.#frameCounter = 0;
        }
        const frame = ExampleSurfaceBackgroundStyle.#frame;
        // O caminho completo, com o nome do mod na frente, também vale.
        if (frame === 3) return BackgroundTextureLoader.GetBackgroundSlot('ExampleMod/' + BG + 'ExampleBiomeSurfaceMid3');
        return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, BG + 'ExampleBiomeSurfaceMid' + frame);
    }

    ChooseCloseTexture(scale, parallax, a, b) {
        return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, BG + 'ExampleBiomeSurfaceClose');
    }
}
