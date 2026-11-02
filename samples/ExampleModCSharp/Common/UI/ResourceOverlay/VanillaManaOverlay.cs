using ExampleMod.Common.Players;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace ExampleMod.Common.UI.ResourceOverlay;

public class VanillaManaOverlay : ModResourceOverlay
{
    private Dictionary<string, Asset<Texture2D>> vanillaAssetCache = new();

    private Asset<Texture2D> starTexture, fancyPanelTexture, barsFillingTexture, barsPanelTexture;

    public override void PostDrawResource(ResourceOverlayDrawContext context)
    {
        Asset<Texture2D> asset = context.texture;

        string fancyFolder = "Images/UI/PlayerResourceSets/FancyClassic/";
        string barsFolder = "Images/UI/PlayerResourceSets/HorizontalBars/";

        if (Main.LocalPlayer.GetModPlayer<ExampleStatIncreasePlayer>().exampleManaCrystals <= 0)
            return;

        if (asset == TextureAssets.Mana)
        {
            DrawClassicFancyOverlay(context);
        }
        else if (CompareAssets(asset, fancyFolder + "Star_Fill"))
        {
            DrawClassicFancyOverlay(context);
        }
        else if (CompareAssets(asset, barsFolder + "MP_Fill"))
        {
            DrawBarsOverlay(context);
        }
        else if (CompareAssets(asset, fancyFolder + "Star_A") || CompareAssets(asset, fancyFolder + "Star_B") || CompareAssets(asset, fancyFolder + "Star_C") || CompareAssets(asset, fancyFolder + "Star_Single"))
        {
            DrawFancyPanelOverlay(context);
        }
        else if (CompareAssets(asset, barsFolder + "MP_Panel_Middle"))
        {
            DrawBarsPanelOverlay(context);
        }
    }

    private bool CompareAssets(Asset<Texture2D> existingAsset, string compareAssetPath)
    {
        if (!vanillaAssetCache.TryGetValue(compareAssetPath, out var asset))
            asset = vanillaAssetCache[compareAssetPath] = Main.Assets.Request<Texture2D>(compareAssetPath);

        return existingAsset == asset;
    }

    private void DrawClassicFancyOverlay(ResourceOverlayDrawContext context)
    {
        context.texture = starTexture ??= ModContent.Request<Texture2D>("ExampleMod/Common/UI/ResourceOverlay/ClassicManaOverlay");
        context.Draw();
    }

    private void DrawFancyPanelOverlay(ResourceOverlayDrawContext context)
    {
        string fancyFolder = "Images/UI/PlayerResourceSets/FancyClassic/";

        Vector2 positionOffset;

        if (context.resourceNumber == context.snapshot.AmountOfManaStars - 1)
        {
            if (CompareAssets(context.texture, fancyFolder + "Star_Single"))
            {
                positionOffset = new Vector2(4, 4);
            }
            else
            {
                positionOffset = new Vector2(4, 0);
            }
        }
        else if (CompareAssets(context.texture, fancyFolder + "Star_A"))
        {
            positionOffset = new Vector2(4, 4);
        }
        else
        {
            positionOffset = new Vector2(4, 0);
        }

        context.texture = fancyPanelTexture ??= ModContent.Request<Texture2D>("ExampleMod/Common/UI/ResourceOverlay/FancyManaOverlay_Panel");
        context.source = context.texture.Frame();
        context.position += positionOffset;
        context.Draw();
    }

    private void DrawBarsOverlay(ResourceOverlayDrawContext context)
    {
        context.texture = barsFillingTexture ??= ModContent.Request<Texture2D>("ExampleMod/Common/UI/ResourceOverlay/BarsManaOverlay_Fill");
        context.Draw();
    }

    private void DrawBarsPanelOverlay(ResourceOverlayDrawContext context)
    {
        context.texture = barsPanelTexture ??= ModContent.Request<Texture2D>("ExampleMod/Common/UI/ResourceOverlay/BarsManaOverlay_Panel");
        context.source = context.texture.Frame();
        context.position.Y += 6;
        context.Draw();
    }
}
