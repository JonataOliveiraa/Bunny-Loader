import { ExampleSurfaceBiome } from '../Biomes/ExampleSurfaceBiome.js';

// O penteado de exemplo do tModLoader (baseado no Player_Hair_88 e no 98).
// Fora da criação de personagem: só no Cabeleireiro, com o jogador no bioma
// de exemplo.
export class ExampleHair extends ModHair {
    get AvailableDuringCharacterCreation() { return false; }

    GetUnlockConditions() {
        return [() => Terraria.Main.LocalPlayer.InModBiome(ExampleSurfaceBiome)];
    }
}
