// A parte de dentro da UIList: aceita o toque inteiro e só desenha o que aparece.
class UIListInner extends UIElement {
    ContainsPoint(point) { return true; }

    DrawChildren(spriteBatch) {
        const p = this.Parent.GetDimensions();
        for (const e of this.Elements) {
            const d = e.GetDimensions();
            if (d.X < p.X + p.Width && d.X + d.Width > p.X && d.Y < p.Y + p.Height && d.Y + d.Height > p.Y) e.Draw(spriteBatch);
        }
    }

    GetViewCullingArea() { return this.Parent.GetDimensions().ToRectangle(); }
}
