// Uma linha do tooltip. `new TooltipLine(Mod, nome, texto)` também vale, como
// no tModLoader. As linhas do jogo têm Mod 'Terraria' e os nomes do tModLoader
// ('ItemName', 'Damage', 'Tooltip0'...). Hide() tira a linha do tooltip.
class TooltipLine {
    constructor(...args) {
        const [mod, name, text] = args.length >= 3 ? args : [undefined, args[0], args[1]];

        this.Mod = mod === undefined ? '' : typeof mod === 'string' ? mod : String(mod.id ?? mod.name ?? mod);
        this.Name = String(name);
        this.Text = text === undefined ? '' : String(text);
        this.OverrideColor = undefined;
        this.IsModifier = false;
        this.IsModifierBad = false;
        this.OneDropLogo = false;
        this.Visible = true;
    }

    Hide() { this.Visible = false; }

    static colorTag(text, color) {
        return '[c/' + TooltipLoader.Hex(color) + ':' + text + ']';
    }
}
