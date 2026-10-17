// Toque (ou mouse): o elemento e a posição, em unidades de UI.
class UIMouseEvent extends UIEvent {
    MousePosition = null;

    constructor(target, mousePosition) {
        super(target);
        this.MousePosition = mousePosition;
    }
}
