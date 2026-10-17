import { ExampleStatIncreasePlayer } from '../../Players/ExampleStatIncreasePlayer.js';

const Main = Terraria.Main;
const FANCY = 'Images/UI/PlayerResourceSets/FancyClassic/';
const BARS = 'Images/UI/PlayerResourceSets/HorizontalBars/';

// Os corações das frutas de exemplo ganham um desenho por cima (um por
// fruta: duas por coração). Funciona nos três estilos de barra do jogo.
export class VanillaLifeOverlay extends ModResourceOverlay {
    vanillaAssetCache = new Map();
    heartTexture = null;
    fancyPanelTexture = null;
    barsFillingTexture = null;
    barsPanelTexture = null;

    PostDrawResource(context) {
        if (!context.drawingLife) return;
        const fruits = Main.player[Main.myPlayer].GetModPlayer(ExampleStatIncreasePlayer).exampleLifeFruits;
        if (context.resourceNumber >= 2 * fruits) return;

        const asset = context.texture;
        const is = (path) => this.CompareAssets(asset, path);
        if (is('Images/Heart') || is('Images/Heart2') || is(FANCY + 'Heart_Fill') || is(FANCY + 'Heart_Fill_B')) {
            this.DrawClassicFancyOverlay(context);
        } else if (is(BARS + 'HP_Fill') || is(BARS + 'HP_Fill_Honey')) {
            this.DrawBarsOverlay(context);
        } else if (['Heart_Left', 'Heart_Middle', 'Heart_Right', 'Heart_Right_Fancy', 'Heart_Single_Fancy'].some((n) => is(FANCY + n))) {
            this.DrawFancyPanelOverlay(context);
        } else if (is(BARS + 'HP_Panel_Middle')) {
            this.DrawBarsPanelOverlay(context);
        }
    }

    // A imagem do jogo com esse nome é a que está sendo desenhada?
    CompareAssets(existingAsset, path) {
        if (!this.vanillaAssetCache.has(path)) {
            let asset = null;
            try {
                asset = ModContent.Request('Terraria/' + path);
            } catch (e) {
                // não existe no celular
            }
            this.vanillaAssetCache.set(path, asset);
        }
        const asset = this.vanillaAssetCache.get(path);
        return !!asset && bl.addressOf(asset) === bl.addressOf(existingAsset);
    }

    static Frame(texture) {
        const size = bl.textureSize(texture);
        return Rectangle.new(0, 0, size.Width, size.Height);
    }

    DrawClassicFancyOverlay(context) {
        context.texture = this.heartTexture ??= ModContent.Request('Common/UI/ResourceOverlay/ClassicLifeOverlay');
        context.Draw();
    }

    DrawFancyPanelOverlay(context) {
        let offset;
        if (context.resourceNumber === context.snapshot.AmountOfLifeHearts - 1) offset = { X: 8, Y: 8 };
        else if (this.CompareAssets(context.texture, FANCY + 'Heart_Left')) offset = { X: 4, Y: 4 };
        else offset = { X: 0, Y: 4 };
        context.texture = this.fancyPanelTexture ??= ModContent.Request('Common/UI/ResourceOverlay/FancyLifeOverlay_Panel');
        context.source = VanillaLifeOverlay.Frame(context.texture);
        context.position.X += offset.X;
        context.position.Y += offset.Y;
        context.Draw();
    }

    DrawBarsOverlay(context) {
        context.texture = this.barsFillingTexture ??= ModContent.Request('Common/UI/ResourceOverlay/BarsLifeOverlay_Fill');
        context.Draw();
    }

    DrawBarsPanelOverlay(context) {
        context.texture = this.barsPanelTexture ??= ModContent.Request('Common/UI/ResourceOverlay/BarsLifeOverlay_Panel');
        context.source = VanillaLifeOverlay.Frame(context.texture);
        context.position.Y += 6;
        context.Draw();
    }
}
