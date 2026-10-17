import { ExampleCoinsUIState } from './ExampleCoinsUIState.js';

const Main = Terraria.Main;

// Dona da tela das moedas: o UserInterface, o Update e a camada de desenho.
export class ExampleCoinsUISystem extends ModSystem {
    exampleCoinUserInterface = null;
    exampleCoinsUI = null;

    ShowMyUI() {
        this.exampleCoinUserInterface?.SetState(this.exampleCoinsUI);
    }

    HideMyUI() {
        this.exampleCoinUserInterface?.SetState(null);
    }

    PostSetupContent() {
        this.exampleCoinUserInterface = new UserInterface();
        this.exampleCoinsUI = new ExampleCoinsUIState();
        this.exampleCoinsUI.Activate();   // chama o OnInitialize
    }

    UpdateUI(gameTime) {
        if (this.exampleCoinUserInterface?.CurrentState) this.exampleCoinUserInterface.Update(gameTime);
    }

    // Desenha logo antes do texto do dedo, na escala da interface.
    ModifyInterfaceLayers(layers) {
        const mouseTextIndex = layers.FindIndex((layer) => layer.Name === 'Vanilla: Mouse Text');
        if (mouseTextIndex === -1) return;
        layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer('ExampleMod: Coins Per Minute', () => {
            if (this.exampleCoinUserInterface?.CurrentState) this.exampleCoinUserInterface.Draw(Main.spriteBatch, null);
            return true;
        }, InterfaceScaleType.UI));
    }
}
