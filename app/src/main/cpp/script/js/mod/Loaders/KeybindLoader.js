// O KeybindLoader do tModLoader, para os mods portados do PC: no celular não
// há teclado, e a "tecla" vira um botão de toque (ModTouchButton, que também
// atende por ModKeybind). A tecla padrão é aceita e ignorada.
class KeybindLoader {
    static RegisterKeybind(mod, name, defaultBinding) {
        return TouchButtonLoader.RegisterButton(mod, name);
    }
}
