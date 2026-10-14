import { TouchButtonSystem } from '../Systems/TouchButtonSystem.js';

const NewText = Terraria.Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'];
const GetBuffName = Terraria.Lang['string GetBuffName(int id)'];

// Lê os botões de toque do mod (o ExampleKeybindPlayer do tModLoader). Os
// botões são registrados em Common/Systems/TouchButtonSystem.js.
export class ExampleTouchButtonPlayer extends ModPlayer {
    LearningExampleHeldTimer = 0;
    LearningExampleDoubleTapTimer = 0;

    ProcessTriggers(player, triggersSet) {
        // O mais comum: JustPressed roda uma vez a cada toque no botão.
        if (TouchButtonSystem.RandomBuffButton.JustPressed) {
            const buff = Terraria.Main.rand['int Next(int minValue, int maxValue)'](1, Terraria.ID.BuffID.Count);
            player.AddBuff(buff, 600, true);
            NewText(ModLocalization.Translate('CustomText.RandomBuff').replace('{0}', GetBuffName(buff)), 255, 255, 255, false);
        }

        // Outros jeitos de usar um botão: segurar e tocar duas vezes.

        // Current e um contador: roda depois de segurar o botão por um tempo.
        if (TouchButtonSystem.LearningExampleButton.Current) {
            this.LearningExampleHeldTimer++;
            if (this.LearningExampleHeldTimer === 30) {
                NewText(ModLocalization.Translate('CustomText.LearningExampleHeld'), 255, 255, 255, false);
            }
        } else {
            this.LearningExampleHeldTimer = 0;
        }

        // JustPressed e um contador: o toque duplo.
        this.LearningExampleDoubleTapTimer = Math.max(0, this.LearningExampleDoubleTapTimer - 1);
        if (TouchButtonSystem.LearningExampleButton.JustPressed) {
            if (this.LearningExampleDoubleTapTimer > 0) {
                NewText(ModLocalization.Translate('CustomText.LearningExampleDoubleTap'), 255, 255, 255, false);
            } else {
                // No 1º toque, 15 atualizações; um 2º toque antes de zerar é o toque duplo.
                this.LearningExampleDoubleTapTimer = 15;
            }
        }
    }
}
