// Uma medida do layout, como o StyleDimension do jogo: pixels + uma fração do
// tamanho do pai (Precent, com o erro de grafia do jogo; Percent também vale).
// No C# é struct: o UIElement copia ao atribuir (Width = StyleDimension.Fill).
class StyleDimension {
    Pixels = 0;
    Precent = 0;

    constructor(pixels = 0, precent = 0) {
        this.Pixels = pixels;
        this.Precent = precent;
    }

    get Percent() { return this.Precent; }
    set Percent(value) { this.Precent = value; }

    Set(pixels, precent) {
        this.Pixels = pixels;
        this.Precent = precent;
    }

    GetValue(containerSize) {
        return this.Pixels + this.Precent * containerSize;
    }

    Clone() {
        return new StyleDimension(this.Pixels, this.Precent);
    }

    static get Fill() { return new StyleDimension(0, 1); }
    static get Empty() { return new StyleDimension(0, 0); }
    static FromPixels(pixels) { return new StyleDimension(pixels, 0); }
    static FromPercent(percent) { return new StyleDimension(0, percent); }
    static FromPixelsAndPercent(pixels, percent) { return new StyleDimension(pixels, percent); }

    // Número = pixels; StyleDimension (ou { Pixels, Precent }) = cópia.
    static From(value) {
        if (typeof value === 'number') return new StyleDimension(value, 0);
        if (!value) return new StyleDimension();
        return new StyleDimension(Number(value.Pixels) || 0, Number(value.Precent ?? value.Percent) || 0);
    }
}
