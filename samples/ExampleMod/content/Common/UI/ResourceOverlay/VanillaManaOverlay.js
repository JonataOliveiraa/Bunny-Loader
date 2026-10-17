import { ExampleStatIncreasePlayer } from '../../Players/ExampleStatIncreasePlayer.js';

const Main = Terraria.Main;
const FANCY = 'Images/UI/PlayerResourceSets/FancyClassic/';
const BARS = 'Images/UI/PlayerResourceSets/HorizontalBars/';

// Com algum cristal de mana de exemplo, as estrelas ganham um desenho por cima.
export class VanillaManaOverlay extends ModResourceOverlay {
    vanillaAssetCache = new Map();
    starTexture = null;
    fancyPanelTexture = null;
    barsFillingTexture = null;
    barsPanelTexture = null;

    PostDrawResource(context) {
        if (context.drawingLife) return;
        if (Main.player[Main.myPlayer].GetModPlayer(ExampleStatIncreasePlayer).exampleManaCrystals <= 0) return;

        const asset = context.texture;
        const is = (path) => this.CompareAssets(asset, path);
        if (is('Images/Mana') || is(FANCY + 'Star_Fill')) {
            this.DrawClassicFancyOverlay(context);
        } else if (is(BARS + 'MP_Fill')) {
            this.DrawBarsOverlay(context);
        } else if (['Star_A', 'Star_B', 'Star_C', 'Star_Single'].some((n) => is(FANCY + n))) {
            this.DrawFancyPanelOverlay(context);
        } else if (is(BARS + 'MP_Panel_Middle')) {
            this.DrawBarsPanelOverlay(context);
        }
    }

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
        context.texture = this.starTexture ??= ModContent.Request('Common/UI/ResourceOverlay/ClassicManaOverlay');
        context.Draw();
    }

    DrawFancyPanelOverlay(context) {
        let offset;
        if (context.resourceNumber === context.snapshot.AmountOfManaStars - 1) {
            offset = this.CompareAssets(context.texture, FANCY + 'Star_Single') ? { X: 4, Y: 4 } : { X: 4, Y: 0 };
        } else if (this.CompareAssets(context.texture, FANCY + 'Star_A')) {
            offset = { X: 4, Y: 4 };
        } else {
            offset = { X: 4, Y: 0 };
        }
        context.texture = this.fancyPanelTexture ??= ModContent.Request('Common/UI/ResourceOverlay/FancyManaOverlay_Panel');
        context.source = VanillaManaOverlay.Frame(context.texture);
        context.position.X += offset.X;
        context.position.Y += offset.Y;
        context.Draw();
    }

    DrawBarsOverlay(context) {
        context.texture = this.barsFillingTexture ??= ModContent.Request('Common/UI/ResourceOverlay/BarsManaOverlay_Fill');
        context.Draw();
    }

    DrawBarsPanelOverlay(context) {
        context.texture = this.barsPanelTexture ??= ModContent.Request('Common/UI/ResourceOverlay/BarsManaOverlay_Panel');
        context.source = VanillaManaOverlay.Frame(context.texture);
        context.position.Y += 6;
        context.Draw();
    }
}
