// A fonte própria de um mod: pede a do Example Mod pelo ModContent.Request,
// confere que é um SpriteFont novo (a MouseText do jogo continua igual) e
// desenha um texto com ela no meio da tela, para uma captura. Loga
// "modfont ..." e "modfont FIM".
const Main = Terraria.Main;
const { FontAssets } = Terraria.GameContent;
const DrawBorderStringFourWay = Terraria.Utils['void DrawBorderStringFourWay(SpriteBatch sb, SpriteFont font, string text, float x, float y, Color textColor, Color borderColor, Vector2 origin, float scale)'];
const MEASURE = 'Vector2 MeasureString(string text)';
const log = (s) => bl.log('modfont ' + s);
let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + (detail === undefined ? '' : ' (' + detail + ')'));
}
const modErrors = [];
const blError = bl.error;
bl.error = (text) => {
    modErrors.push(String(text).split('\n')[0]);
    return blError(text);
};

const SAMPLE = 'Fonte do mod: Olá, ação!';
let font = null, frame = 0, done = false;

export class TestModFontSystem extends ModSystem {
    PostSetupContent() {
        try {
            const mouse = FontAssets.MouseText.Value;
            const before = mouse[MEASURE](SAMPLE).X;
            const asset = ModContent.Request('examplemod/Assets/Fonts/ExampleFont');
            font = asset.Value;
            check('Request devolve fonte', !!font && asset.IsLoaded === true);
            check('mesma fonte no segundo pedido', ModContent.Request('examplemod/Assets/Fonts/ExampleFont').Value === font);
            check('fonte nova, nao a MouseText', font !== mouse);
            check('HasAsset acha a fonte', ModContent.HasAsset('examplemod/Assets/Fonts/ExampleFont'));
            const own = font[MEASURE](SAMPLE).X;
            check('medida diferente da MouseText', Math.abs(own - before) > 1, before.toFixed(1) + ' x ' + own.toFixed(1));
            check('MouseText intacta', Math.abs(mouse[MEASURE](SAMPLE).X - before) < 0.01);
            check('LineSpacing do arquivo', font.LineSpacing === 24, font.LineSpacing);
            check('caractere fora da fonte usa o do jogo', font['bool HasCharacter(char c)']('ж'.charCodeAt(0)) === mouse['bool HasCharacter(char c)']('ж'.charCodeAt(0)));
            let missing = null;
            try { ModContent.Request('examplemod/Assets/Fonts/NaoExiste'); } catch (e) { missing = String(e); }
            check('fonte inexistente lanca', !!missing, missing);
        } catch (e) {
            fails++;
            log('erro: ' + e + ' ' + e.stack);
        }
    }

    ModifyInterfaceLayers(layers) {
        const index = layers.FindIndex((layer) => layer.Name === 'Vanilla: Mouse Text');
        if (index === -1 || !font) return;
        layers.Insert(index, new LegacyGameInterfaceLayer('TestModFont: Sample', () => {
            const x = Main.screenWidth / Main.UIScale / 2 - 200;
            const y = Main.screenHeight / Main.UIScale / 2 - 120;
            DrawBorderStringFourWay(Main.spriteBatch, font, SAMPLE, x, y, Color.Gold, Color.Black, Vector2.new(0, 0), 1.5);
            DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, SAMPLE, x, y + 50, Color.White, Color.Black, Vector2.new(0, 0), 1.5);
            return true;
        }, InterfaceScaleType.UI));
    }

    PostUpdateEverything() {
        if (done || Main.gameMenu) return;
        frame++;
        if (frame === 120) log('SHOT texto');
        if (frame === 200) {
            check('nenhum erro de mod', modErrors.length === 0, modErrors.join(' | '));
            log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
            done = true;
        }
    }
}
log('carregado');

export default class TestModFont extends Mod {}
