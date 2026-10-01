// A linha no desenho do tooltip, como a DrawableTooltipLine do tModLoader: o
// que o PreDrawTooltipLine pode mexer é a posição (X, Y), a fonte, a rotação,
// a origem, a escala (BaseScale) e a distância da sombra (Spread); o texto e
// as linhas se mudam no ModifyTooltips. No desenho, BaseScale já vem com a
// escala do tooltip do celular (Settings.Tooltips.Scale). Autor: GST378.
class DrawableTooltipLine extends TooltipLine {
    constructor(parent, index, x, y, color) {
        super(parent.Mod, parent.Name, parent.Text);

        this.OneDropLogo = parent.OneDropLogo;
        this.IsModifier = parent.IsModifier;
        this.IsModifierBad = parent.IsModifierBad;
        this.OverrideColor = parent.OverrideColor;

        this.Index = index;
        this.OriginalX = this.X = x;
        this.OriginalY = this.Y = y;
        this.Color = color;
        this.Font = Terraria.GameContent.FontAssets.MouseText.Value;
        this.Rotation = 0;
        this.Origin = Vector2.Zero;
        this.BaseScale = Vector2.One;
        this.MaxWidth = -1;
        this.Spread = 2;
    }
}
