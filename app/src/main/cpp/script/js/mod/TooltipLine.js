// Uma linha do tooltip. `new TooltipLine(Mod, nome, texto)` também vale.
class TooltipLine {
    constructor(...args) {
        const [name, text] = args.length >= 3 ? [args[1], args[2]] : args;

        this.Name = String(name);
        this.Text = text === undefined ? '' : String(text);
        this.OverrideColor = undefined;
        this.IsModifier = false;
        this.IsModifierBad = false;
        this.OneDropLogo = false;
    }

    static colorTag(text, color) {
        return '[c/' + TooltipLoader.Hex(color) + ':' + text + ']';
    }
}
