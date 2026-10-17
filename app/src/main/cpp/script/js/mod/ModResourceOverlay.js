// Desenha por cima (ou no lugar) dos corações e das estrelas do jogo, como o
// ModResourceOverlay do tModLoader. No celular a vida e a mana são o
// GUIHearts e o GUIMana: cada coração e estrela passa pelo PreDrawResource e
// pelo PostDrawResource, um desenho de cada vez (ResourceOverlayDrawContext).
class ModResourceOverlay {
    // false: o jogo não faz este desenho.
    PreDrawResource(context) { return true; }
    PostDrawResource(context) {}
    // A barra inteira (vida ou mana). false: o jogo não a desenha. drawText
    // (Ref) false tira o texto da vida.
    PreDrawResourceDisplay(snapshot, displaySet, drawingLife, textColor, drawText) { return true; }
    PostDrawResourceDisplay(snapshot, displaySet, drawingLife, textColor, drawText) {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModResourceOverlay)) {
            throw new TypeError('ModResourceOverlay.register(Class): pass the class, which extends ModResourceOverlay');
        }
        const inst = new cls();
        Templates.Adopt(cls, inst);
        ResourceOverlayLoader.Add(inst, cls);
        return inst;
    }
}
