// A rolagem: no celular vem do arrasto do UIList, não de uma roda.
class UIScrollWheelEvent extends UIMouseEvent {
    ScrollWheelValue = 0;

    constructor(target, mousePosition, scrollWheelValue) {
        super(target, mousePosition);
        this.ScrollWheelValue = scrollWheelValue;
    }
}
