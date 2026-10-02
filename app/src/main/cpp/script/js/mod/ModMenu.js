// Um tema da tela de título, como o ModMenu do tModLoader: o logo, o sol, a
// lua e o fundo enquanto o jogo está nos menus. Não há troca: vale o tema do
// mod carregado por último. A música dos menus é sempre a do jogo (o Music
// fica, para o código do tModLoader rodar, mas não toca).
// Os padrões ficam no protótipo: a classe do mod pode escrever
// `get Logo() { ... }` (como no tModLoader) ou `this.Logo = ...`.
class ModMenu {
    // Logo, SunTexture e MoonTexture: o caminho de uma textura do mod (como
    // 'Content/Menu/ExampleSun' ou 'Assets/Textures/Menu/ExampleSun') ou um Asset (ModContent.Request);
    // null = a do jogo. A lua é um quadro só (o MoonTexture do tModLoader).
    // MenuBackgroundStyle: a instância de um ModSurfaceBackgroundStyle
    // (ModContent.GetInstance(Classe)), ou null. IsAvailable: false tira o
    // tema (vale o anterior; pode ser `get IsAvailable()`). Music e
    // DisplayName: aceitos e sem efeito. Mod: o mod dono (posto no registro).

    get IsSelected() { return MenuLoader.CurrentMenu === this; }

    // Quando o tema passa a valer (na abertura do jogo, já nos menus).
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
