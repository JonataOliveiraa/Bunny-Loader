// Um botão de imagem com um texto de dica enquanto o dedo está nele.
export class ExampleUIHoverImageButton extends UIImageButton {
    hoverText = '';

    constructor(texture, hoverText) {
        super(texture);
        this.hoverText = hoverText;
    }

    DrawSelf(spriteBatch) {
        super.DrawSelf(spriteBatch);
        if (this.IsMouseHovering) UICommon.TooltipMouseText(this.hoverText);
    }
}
