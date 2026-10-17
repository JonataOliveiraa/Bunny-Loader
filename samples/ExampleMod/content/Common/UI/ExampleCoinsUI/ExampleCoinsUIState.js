import { ExampleDraggableUIPanel } from './ExampleDraggableUIPanel.js';
import { ExampleUIHoverImageButton } from './ExampleUIHoverImageButton.js';
import { UIMoneyDisplay } from './UIMoneyDisplay.js';
import { ExampleCoinsUISystem } from './ExampleCoinsUISystem.js';

const GetTextValue = Terraria.Localization.Language['string GetTextValue(string key)'];
const SoundID = Terraria.ID.SoundID;

// A tela das moedas por minuto: um painel arrastável com o contador, um botão
// para zerar e um para fechar. Abre com o comando /coins.
export class ExampleCoinsUIState extends UIState {
    CoinCounterPanel = null;
    MoneyDisplay = null;

    // Monta os elementos uma vez, no primeiro Activate.
    OnInitialize() {
        const panel = this.CoinCounterPanel = new ExampleDraggableUIPanel();
        panel.SetPadding(0);
        this.SetRectangle(panel, 400, 100, 170, 70);
        panel.BackgroundColor = Color.new(73, 94, 171);

        // O tModLoader usa Terraria/Images/UI/ButtonPlay e ButtonDelete, que
        // não existem no celular: os dois ícones ficam ao lado deste arquivo.
        const play = new ExampleUIHoverImageButton(ModContent.Request('Common/UI/ExampleCoinsUI/ButtonPlay'),
            ModLocalization.Translate('UI.ExampleCoinsUIState.Reset'));
        this.SetRectangle(play, 110, 10, 22, 22);
        play.OnLeftClick.Add((evt, element) => this.PlayButtonClicked(evt, element));
        panel.Append(play);

        const close = new ExampleUIHoverImageButton(ModContent.Request('Common/UI/ExampleCoinsUI/ButtonDelete'),
            GetTextValue('LegacyInterface.52'));   // "Fechar", na língua do jogo
        this.SetRectangle(close, 140, 10, 22, 22);
        close.OnLeftClick.Add((evt, element) => this.CloseButtonClicked(evt, element));
        panel.Append(close);

        const money = this.MoneyDisplay = new UIMoneyDisplay();
        this.SetRectangle(money, 15, 20, 100, 40);
        panel.Append(money);

        this.Append(panel);
    }

    SetRectangle(element, left, top, width, height) {
        element.Left.Set(left, 0);
        element.Top.Set(top, 0);
        element.Width.Set(width, 0);
        element.Height.Set(height, 0);
    }

    PlayButtonClicked(evt, listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuOpen);
        this.MoneyDisplay.ResetCoins();
    }

    CloseButtonClicked(evt, listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuClose);
        ModContent.GetInstance(ExampleCoinsUISystem).HideMyUI();
    }

    UpdateValue(pickedUp) {
        this.MoneyDisplay.AddCoinsPerMinute(pickedUp);
    }
}
