import { ExampleSurfaceBiome } from '../Biomes/ExampleSurfaceBiome.js';

const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;

// A nuvem rara do tModLoader (AdvancedExampleCloud): bem mais comum com o
// jogador no bioma de exemplo. A ExampleCloud (a comum) vem só da textura, em
// ExampleMod.Load (CloudLoader.AddCloudFromTexture).
export class ExampleAdvancedCloud extends ModCloud {
    get RareCloud() { return true; }

    // O peso é contra as outras raras (as do jogo pesam 1 cada).
    SpawnChance() {
        if (!Terraria.Main.gameMenu && Terraria.Main.LocalPlayer.InModBiome(ExampleSurfaceBiome)) return 10;
        return 1;
    }

    // A textura tem texto: a nuvem não pode nascer espelhada.
    OnSpawn(cloud) {
        cloud.spriteDir = SpriteEffects.None;
    }
}
