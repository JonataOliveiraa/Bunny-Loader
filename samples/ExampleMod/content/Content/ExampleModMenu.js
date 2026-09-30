// O tema da tela de título do Example Mod, como o ExampleModMenu do
// tModLoader: o sol e a lua dele, a música e o fundo do bioma de exemplo, um
// trovão quando é escolhido e o logo do jogo nas cores do arco-íris. Os temas
// se trocam tocando no "Tema do menu", no rodapé do título.
import { ExampleSurfaceBackgroundStyle } from './Biomes/ExampleSurfaceBackgroundStyle.js';

const MENU = 'Assets/Textures/Menu/';

export class ExampleModMenu extends ModMenu {
    get SunTexture() { return MENU + 'ExampleSun'; }
    get MoonTexture() { return MENU + 'ExampliumMoon'; }
    get MenuBackgroundStyle() { return ModContent.GetInstance(ExampleSurfaceBackgroundStyle); }
    get DisplayName() { return 'Example ModMenu'; }

    SetStaticDefaults() {
        this.Music = MusicLoader.GetMusicSlot('Music/Ropocalypse2');
    }

    OnSelected() {
        SoundEngine.PlaySound(Terraria.ID.SoundID.Thunder);
    }

    // drawColor é um Ref: a cor do logo neste quadro.
    PreDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor) {
        const Main = Terraria.Main;
        drawColor.value = Color.new(Main.DiscoR, Main.DiscoG, Main.DiscoB, 255);
        return true;
    }
}
