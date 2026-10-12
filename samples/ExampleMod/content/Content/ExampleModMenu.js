import { ExampleSurfaceBackgroundStyle } from './Biomes/ExampleSurfaceBackgroundStyle.js';

const MENU = 'Assets/Textures/Menu/';

export class ExampleModMenu extends ModMenu {
    get Logo() { return MENU + 'ExampleModTitle'; }
    get SunTexture() { return MENU + 'ExampleSun'; }
    get MoonTexture() { return MENU + 'ExampliumMoon'; }
    get MenuBackgroundStyle() { return ModContent.GetInstance(ExampleSurfaceBackgroundStyle); }
    get DisplayName() { return 'Example ModMenu'; }

    SetStaticDefaults() {
        this.Music = MusicLoader.GetMusicSlot('Music/MysteriousMystery');
    }

    OnSelected() {
        SoundEngine.PlaySound(Terraria.ID.SoundID.Thunder);
    }

    PreDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor) {
        drawColor.value = Color.White;
        return true;
    }
}
