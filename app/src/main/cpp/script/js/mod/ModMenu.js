// Um tema da tela de título, como o ModMenu do tModLoader: o logo, o sol, a
// lua, a música e o fundo enquanto o jogo está nos menus. Os temas se trocam
// tocando no "Tema do menu" no rodapé do título, e o escolhido fica salvo.
// Os padrões ficam no protótipo: a classe do mod pode escrever
// `get Music() { ... }` (como no tModLoader) ou `this.Music = ...`.
class ModMenu {
    // Logo, SunTexture e MoonTexture: o caminho de uma textura do mod (como
    // 'Assets/Textures/Menu/ExampleSun') ou um Asset (ModContent.Request);
    // null = a do jogo. A lua é um quadro só (o MoonTexture do tModLoader).
    // Music: MusicLoader.GetMusicSlot(...) ou um MusicID do jogo; -1 = a do
    // jogo. MenuBackgroundStyle: a instância de um ModSurfaceBackgroundStyle
    // (ModContent.GetInstance(Classe)), ou null. DisplayName: o nome no
    // rodapé (o nome do mod, se não mudar). IsAvailable: false tira o tema da
    // troca (pode ser `get IsAvailable()`). Mod: o mod dono (posto no registro).

    get IsSelected() { return MenuLoader.CurrentMenu === this; }

    // Na escolha do tema (também na abertura do jogo, se ele estava salvo).
    OnSelected() {}

    OnDeselected() {}

    // Todo quadro nos menus; isOnTitleScreen: na tela de título (menuMode 0).
    Update(isOnTitleScreen) {}

    // Antes do logo. logoDrawCenter, logoRotation, logoScale e drawColor são
    // Ref (.value), como os ref do tModLoader. false: não desenha o logo.
    PreDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor) { return true; }

    // Depois do logo, com os valores com que ele foi desenhado.
    PostDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor) {}

    SetStaticDefaults() {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModMenu)) {
            throw new TypeError('ModMenu.register(Classe): passe a classe, que estende ModMenu');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        MenuLoader.Add(inst);
        Safe.Run(cls.name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        return inst;
    }
}
ModMenu.prototype.Logo = null;
ModMenu.prototype.SunTexture = null;
ModMenu.prototype.MoonTexture = null;
ModMenu.prototype.Music = -1;
ModMenu.prototype.MenuBackgroundStyle = null;
ModMenu.prototype.IsAvailable = true;
ModMenu.prototype.DisplayName = null;
