import { ExampleCoinsUISystem } from '../UI/ExampleCoinsUI/ExampleCoinsUISystem.js';

// "/coins": abre a tela das moedas por minuto.
export class ExampleCoinsCommand extends ModCommand {
    get Command() { return 'coins'; }
    get Type() { return CommandType.Chat; }
    get Description() { return ModLocalization.Translate('Commands.ExampleCoinsCommand.Description'); }

    Action(caller, input, args) {
        ModContent.GetInstance(ExampleCoinsUISystem).ShowMyUI();
    }
}
