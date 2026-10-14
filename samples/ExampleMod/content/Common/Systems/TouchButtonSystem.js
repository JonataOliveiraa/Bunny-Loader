// Guarda os botões de toque do mod (o KeybindSystem do tModLoader). O uso
// está em Common/Players/ExampleTouchButtonPlayer.js.
//
// No celular não há teclado: cada botão vira uma ação nova no editor de
// controles do jogo (Configurações > Controles > Editar), no fim da lista de
// ações de um espaço, com ícone e nome. O jogador o põe onde quiser, como o
// Pular ou o Curar; o layout guarda.
//
// O nome vem do Localization (TouchButtons.<Nome>.DisplayName) e o ícone de
// Assets/TouchButtons/<Nome>.png (48x48 cabe melhor). Um mod portado do PC
// pode manter o KeybindLoader.RegisterKeybind(this.Mod, 'RandomBuff', 'P'):
// é o mesmo botão, e a tecla é ignorada.
export class TouchButtonSystem extends ModSystem {
    static RandomBuffButton = null;
    static LearningExampleButton = null;

    Load() {
        TouchButtonSystem.RandomBuffButton = TouchButtonLoader.RegisterButton(this.Mod, 'RandomBuff');
        TouchButtonSystem.LearningExampleButton = TouchButtonLoader.RegisterButton(this.Mod, 'LearningExample');
    }
}
