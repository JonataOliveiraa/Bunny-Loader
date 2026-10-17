import { ExampleResourcePlayer } from '../../Players/ExampleResourcePlayer.js';
import { ExampleCustomResourceWeapon } from '../../../Content/Items/Weapons/ExampleCustomResourceWeapon.js';

const Main = Terraria.Main;
const FillRect = Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Rectangle destinationRectangle, Color color)'];

// A barra do recurso de exemplo: a moldura (ExampleResourceFrame.png), o
// enchimento em degradê e o texto. Só aparece com a arma do recurso na mão.
export class ExampleResourceBar extends UIState {
    text = null;
    area = null;
    barFrame = null;
    gradientA = Color.new(123, 25, 138);   // roxo escuro
    gradientB = Color.new(187, 91, 201);   // roxo claro

    OnInitialize() {
        // Uma área de 182x60, à esquerda dos corações (perto do topo da tela).
        const area = this.area = new UIElement();
        area.Left.Set(-area.Width.Pixels - 600, 1);
        area.Top.Set(30, 0);
        area.Width.Set(182, 0);
        area.Height.Set(60, 0);

        const frame = this.barFrame = new UIImage(ModContent.Request('Common/UI/ExampleResourceUI/ExampleResourceFrame'));
        frame.Left.Set(22, 0);
        frame.Top.Set(0, 0);
        frame.Width.Set(138, 0);
        frame.Height.Set(34, 0);

        const text = this.text = new UIText('0/0', 0.8);
        text.Width.Set(138, 0);
        text.Height.Set(34, 0);
        text.Top.Set(40, 0);
        text.Left.Set(0, 0);

        area.Append(text);
        area.Append(frame);
        this.Append(area);
    }

    static #Holding() {
        const held = Main.player[Main.myPlayer].HeldItem;
        return ModItem.getModItem(held.type) instanceof ExampleCustomResourceWeapon;
    }

    Draw(spriteBatch) {
        if (!ExampleResourceBar.#Holding()) return;
        super.Draw(spriteBatch);
    }

    // O enchimento, coluna a coluna, do roxo escuro ao claro.
    DrawSelf(spriteBatch) {
        super.DrawSelf(spriteBatch);
        const modPlayer = Main.player[Main.myPlayer].GetModPlayer(ExampleResourcePlayer);
        const quotient = Math.min(Math.max(modPlayer.exampleResourceCurrent / modPlayer.exampleResourceMax2, 0), 1);

        const box = this.barFrame.GetInnerDimensions();
        const left = Math.trunc(box.X) + 12;
        const right = Math.trunc(box.X) + Math.trunc(box.Width) - 12;
        const y = Math.trunc(box.Y) + 8;
        const height = Math.trunc(box.Height) - 16;
        const pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
        const steps = Math.trunc((right - left) * quotient);
        for (let i = 0; i < steps; i++) {
            FillRect(spriteBatch, pixel, Rectangle.new(left + i, y, 1, height), Color.Lerp(this.gradientA, this.gradientB, i / (right - left)));
        }
    }

    Update(gameTime) {
        if (!ExampleResourceBar.#Holding()) return;
        const modPlayer = Main.player[Main.myPlayer].GetModPlayer(ExampleResourcePlayer);
        this.text.SetText(ModLocalization.Translate('UI.ExampleResource')
            .replace('{0}', modPlayer.exampleResourceCurrent).replace('{1}', modPlayer.exampleResourceMax2));
        super.Update(gameTime);
    }
}
