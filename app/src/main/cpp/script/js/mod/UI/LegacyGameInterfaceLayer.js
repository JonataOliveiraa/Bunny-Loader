// Uma camada que desenha por uma função (o delegate do tModLoader):
//   new LegacyGameInterfaceLayer('MeuMod: Painel', () => { ui.Draw(Main.spriteBatch, null); return true; }, InterfaceScaleType.UI)
class LegacyGameInterfaceLayer extends GameInterfaceLayer {
    #draw = null;

    constructor(name, drawMethod, scaleType = InterfaceScaleType.Game) {
        super(name, scaleType);
        if (typeof drawMethod !== 'function') throw new TypeError('LegacyGameInterfaceLayer(name, drawMethod, scaleType): drawMethod must be a function');
        this.#draw = drawMethod;
    }

    DrawSelf() {
        return this.#draw() !== false;
    }
}
