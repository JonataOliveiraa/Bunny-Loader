// Tema do Menu (Configurações > Geral, no menu principal). Este pacote põe um
// segundo tema, com nome comprido (o botão corta com "...") e a música do
// título clássico (MusicID 6); com o Example Mod ligado, a roda é Terraria ->
// Example ModMenu -> este. Os toques são dados de fora (adb); a conferência é
// pela tela e pelo log, a cada 2 s nos menus:
//   menutheme <tema> cur=<curMusic> fade50/51/6=<musicFade> nocross51=<marca>
const Main = Terraria.Main;

export class TestLongMenu extends ModMenu {
    get DisplayName() { return 'Tema de Teste com um Nome Bem Comprido Mesmo'; }

    SetStaticDefaults() {
        this.Music = 6;
    }

    PreDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor) {
        drawColor.value = Color.new(255, 120, 120, 255);
        return true;
    }

    OnSelected() { bl.log('menutheme OnSelected TestLongMenu'); }
    OnDeselected() { bl.log('menutheme OnDeselected TestLongMenu'); }
}

let frames = 0;
GUILogo['void Draw()'].hook((original, self) => {
    original(self);
    if (++frames % 120 !== 0) return;

    const fade = Main.musicFade;
    const f = (i) => Number(fade[i]).toFixed(2);
    bl.log('menutheme cur=' + Main.curMusic + ' new=' + Main.newMusic + ' fade0/50/51/6=' + f(0) + '/' + f(50) + '/' + f(51) + '/' + f(6) +
        ' nocross51=' + Main.musicNoCrossFade[51] + ' menuMode=' + Main.menuMode + ' vol=' + Main.musicVolume);
});

bl.log('menutheme FIM: carregado');

export default class TestMenuTheme extends Mod {}
