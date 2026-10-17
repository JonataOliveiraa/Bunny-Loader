// O retângulo calculado de um elemento (em unidades de UI, as do Main.mouseX).
class CalculatedStyle {
    X = 0;
    Y = 0;
    Width = 0;
    Height = 0;

    constructor(x = 0, y = 0, width = 0, height = 0) {
        this.X = x;
        this.Y = y;
        this.Width = width;
        this.Height = height;
    }

    ToRectangle() {
        return Rectangle.new(Math.trunc(this.X), Math.trunc(this.Y), Math.trunc(this.Width), Math.trunc(this.Height));
    }

    Position() { return Vector2.new(this.X, this.Y); }
    Center() { return Vector2.new(this.X + this.Width * 0.5, this.Y + this.Height * 0.5); }

    // O Rectangle.Contains do jogo, com o ponto arredondado para baixo.
    Contains(x, y) {
        const px = Math.trunc(x), py = Math.trunc(y);
        const left = Math.trunc(this.X), top = Math.trunc(this.Y);
        return px >= left && px < left + Math.trunc(this.Width) && py >= top && py < top + Math.trunc(this.Height);
    }

    Clone() {
        return new CalculatedStyle(this.X, this.Y, this.Width, this.Height);
    }
}
