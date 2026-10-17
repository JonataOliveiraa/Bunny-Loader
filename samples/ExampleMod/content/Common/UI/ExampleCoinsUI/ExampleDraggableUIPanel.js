// Um painel que se arrasta com o dedo (o ExampleDraggableUIPanel do tModLoader).
export class ExampleDraggableUIPanel extends UIPanel {
    offset = { X: 0, Y: 0 };
    dragging = false;

    LeftMouseDown(evt) {
        super.LeftMouseDown(evt);
        // Só quando o toque é no próprio painel, não nos botões dentro dele.
        if (evt.Target === this) this.DragStart(evt);
    }

    LeftMouseUp(evt) {
        super.LeftMouseUp(evt);
        if (evt.Target === this) this.DragEnd(evt);
    }

    DragStart(evt) {
        this.offset = { X: evt.MousePosition.X - this.Left.Pixels, Y: evt.MousePosition.Y - this.Top.Pixels };
        this.dragging = true;
    }

    DragEnd(evt) {
        const end = evt.MousePosition;
        this.dragging = false;
        this.Left.Set(end.X - this.offset.X, 0);
        this.Top.Set(end.Y - this.offset.Y, 0);
        this.Recalculate();
    }

    Update(gameTime) {
        super.Update(gameTime);
        const Main = Terraria.Main;
        // O dedo em cima do painel não usa o item na mão.
        if (this.ContainsPoint(Main.MouseScreen)) Main.player[Main.myPlayer].mouseInterface = true;

        if (this.dragging) {
            this.Left.Set(Main.mouseX - this.offset.X, 0);
            this.Top.Set(Main.mouseY - this.offset.Y, 0);
            this.Recalculate();
        }

        // Fora da tela, volta para dentro.
        const parent = this.Parent.GetDimensions();
        const d = this.GetDimensions();
        const inside = d.X < parent.X + parent.Width && d.X + d.Width > parent.X && d.Y < parent.Y + parent.Height && d.Y + d.Height > parent.Y;
        if (!inside) {
            this.Left.Pixels = Math.min(Math.max(this.Left.Pixels, 0), parent.Width - this.Width.Pixels);
            this.Top.Pixels = Math.min(Math.max(this.Top.Pixels, 0), parent.Height - this.Height.Pixels);
            this.Recalculate();
        }
    }
}
