import { ExampleResourceBar } from './ExampleResourceBar.js';

const Main = Terraria.Main;

// A barra do recurso de exemplo, desenhada logo antes das barras de vida e mana.
export class ExampleResourceUISystem extends ModSystem {
    ExampleResourceBarUserInterface = null;
    ExampleResourceBar = null;

    // O tModLoader monta no Load; aqui as texturas só existem com o jogo rodando,
    // então a tela nasce no PostSetupContent (como a das moedas).
    PostSetupContent() {
        this.ExampleResourceBar = new ExampleResourceBar();
        this.ExampleResourceBarUserInterface = new UserInterface();
        this.ExampleResourceBarUserInterface.SetState(this.ExampleResourceBar);
    }

    UpdateUI(gameTime) {
        this.ExampleResourceBarUserInterface?.Update(gameTime);
    }

    ModifyInterfaceLayers(layers) {
        const resourceBarIndex = layers.FindIndex((layer) => layer.Name === 'Vanilla: Resource Bars');
        if (resourceBarIndex === -1) return;
        layers.Insert(resourceBarIndex, new LegacyGameInterfaceLayer('ExampleMod: Example Resource Bar', () => {
            this.ExampleResourceBarUserInterface.Draw(Main.spriteBatch, null);
            return true;
        }, InterfaceScaleType.UI));
    }
}
