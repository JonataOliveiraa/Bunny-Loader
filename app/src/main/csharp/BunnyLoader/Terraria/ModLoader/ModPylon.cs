using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader.Core;
using Terraria.ObjectData;
using Terraria.UI;

namespace Terraria.ModLoader;

public abstract class ModPylon : ModTile
{
    public TeleportPylonType PylonType { get; internal set; }

    public virtual bool CanPlacePylon() => !Main.PylonSystem.HasPylonOfType(PylonType);

    public virtual NPCShop.Entry? GetNPCShopEntry()
    {
        int drop = TileLoader.DropType(Type, 0);
        if (drop == 0)
            return null;
        return new NPCShop.Entry(drop, Condition.AnotherTownNPCNearby, Condition.NotInEvilBiome);
    }

    public virtual bool ValidTeleportCheck_NPCCount(TeleportPylonInfo pylonInfo, int defaultNecessaryNPCCount) =>
        PylonHooks.EnoughNPCs(defaultNecessaryNPCCount, pylonInfo.PositionInTiles);

    public virtual bool ValidTeleportCheck_BiomeRequirements(TeleportPylonInfo pylonInfo, SceneMetrics sceneData) => true;

    public virtual void DrawMapIcon(ref MapOverlayDrawContext context, ref string mouseOverText, TeleportPylonInfo pylonInfo, bool isNearPylon, Color drawColor, float deselectedScale, float selectedScale)
    {
    }

    public override bool RightClick(int i, int j)
    {
        PylonHooks.OpenMap(i, j);
        return true;
    }

    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

    public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
    {
        TileObjectData data = TileObjectData.GetTileData(Type, 0, 0);
        if (!data.IsNull && drawData.tileFrameX % data.CoordinateFullWidth == 0 && drawData.tileFrameY == 0)
            Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
    }

    public void DefaultDrawPylonCrystal(SpriteBatch spriteBatch, int i, int j, Asset<Texture2D> crystalTexture, Asset<Texture2D> crystalHighlightTexture, Vector2 crystalOffset, Color pylonShadowColor, Color dustColor, int dustChanceDenominator, int crystalVerticalFrameCount)
    {
        Vector2 offscreenVector = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Tile tile = Main.tile[i, j];
        if (!tile.HasTile)
            return;
        TileObjectData tileData = TileObjectData.GetTileData(tile);
        if (tileData.IsNull)
            return;
        int frameY = Main.tileFrameCounter[TileID.TeleportationPylon] / crystalVerticalFrameCount;
        Rectangle crystalFrame = crystalTexture.Frame(1, crystalVerticalFrameCount, 0, frameY);
        crystalFrame.Height -= 1;
        Vector2 origin = new Vector2(crystalFrame.Width / 2f, crystalFrame.Height / 2f);
        Vector2 crystalPosition = new Point16(i, j).ToWorldCoordinates(tileData.CoordinateFullWidth / 2f - 2f, tileData.CoordinateFullHeight / 2f) + crystalOffset;
        float sinusoidalOffset = (float)Math.Sin(Main.GlobalTimeWrappedHourly * (Math.PI * 2) / 5);
        Vector2 drawingPosition = crystalPosition + offscreenVector + new Vector2(0f, sinusoidalOffset * 4f);
        if (!Main.gamePaused && dustChanceDenominator > 0 && Main.rand.Next(4 * dustChanceDenominator) == 0)
        {
            Vector2 corner = crystalPosition - origin;
            int numForDust = Dust.NewDust(corner, crystalFrame.Width, crystalFrame.Height, DustID.TintableDustLighted, 0f, 0f, 254, dustColor, 0.5f);
            Dust dust = Main.dust[numForDust];
            dust.velocity = new Vector2(dust.velocity.X * 0.1f, dust.velocity.Y * 0.1f - 0.2f);
        }

        Color light = Lighting.GetColor(new Point(i, j));
        Color color = new Color((int)((light.R + (255 - light.R) * 0.8f) * 0.7f), (int)((light.G + (255 - light.G) * 0.8f) * 0.7f),
            (int)((light.B + (255 - light.B) * 0.8f) * 0.7f), (int)((light.A + (255 - light.A) * 0.8f) * 0.7f));
        Texture2D texture = crystalTexture.Value;
        spriteBatch.Draw(texture, drawingPosition - Main.screenPosition, crystalFrame, color, 0f, origin, 1f, SpriteEffects.None, 0f);
        float scale = (float)Math.Sin(Main.GlobalTimeWrappedHourly * ((float)Math.PI * 2f)) * 0.2f + 0.8f;
        Color shadowColor = pylonShadowColor * scale;
        for (float shadowPos = 0f; shadowPos < 1f; shadowPos += 1f / 6f)
            spriteBatch.Draw(texture, drawingPosition - Main.screenPosition + ((float)Math.PI * 2f * shadowPos).ToRotationVector2() * (6f + sinusoidalOffset * 2f), crystalFrame, shadowColor, 0f, origin, 1f, SpriteEffects.None, 0f);
    }

    public bool DefaultDrawMapIcon(ref MapOverlayDrawContext context, Asset<Texture2D> mapIcon, Vector2 drawCenter, Color drawColor, float deselectedScale, float selectedScale)
    {
        return context.Draw(mapIcon.Value, drawCenter, drawColor, new SpriteFrame(1, 1, 0, 0), deselectedScale, selectedScale, Alignment.Center).IsMouseOver;
    }

    public void DefaultMapClickHandle(bool mouseIsHovering, TeleportPylonInfo pylonInfo, string hoveringTextKey, ref string mouseOverText)
    {
        if (!mouseIsHovering)
            return;
        Main.cancelWormHole = true;
        mouseOverText = Language.GetTextValue(hoveringTextKey);
        if (Main.mouseLeft && Main.mouseLeftRelease)
        {
            Main.mouseLeftRelease = false;
            Main.mapFullscreen = false;
            Main.PylonSystem.RequestTeleportation(pylonInfo, Main.LocalPlayer);
        }
    }
}
