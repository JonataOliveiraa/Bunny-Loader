import { buildTemple } from './temple-runtime.js';

export default class TemploPiramide extends Mod {
    Load() {
        Terraria.WorldGen['void makeTemple(int x, int y, GenerationProgress progress)'].hook((_original, x, y) => {
            buildTemple(x, y);
        });
        bl.log('Templo Pirâmide: ativo. Crie um mundo novo para gerar o templo.');
    }
}
