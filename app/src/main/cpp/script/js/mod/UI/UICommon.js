// Ajudas da UI do tModLoader.
class UICommon {
    static #MOUSE_TEXT = 'void MouseText(string cursorText, int rare, byte diff, int hackedMouseX, int hackedMouseY, int hackedScreenWidth, int hackedScreenHeight, int pushWidthX)';

    // O texto de dica junto do dedo (no DrawSelf de um elemento tocado).
    static TooltipMouseText(text) {
        Terraria.Main.instance[UICommon.#MOUSE_TEXT](String(text), 0, 0, -1, -1, -1, -1, 0);
    }
}
