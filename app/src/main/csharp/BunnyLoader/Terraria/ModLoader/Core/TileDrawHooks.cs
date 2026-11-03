using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ObjectData;

namespace Terraria.ModLoader.Core;

internal static unsafe partial class TileHooks
{
    static int[] drawCells = new int[12288], nearbyCells = new int[12288], lightCells = new int[12288];

    static Vector3[] lightValues = new Vector3[4096];

    static int lightCount;

    static readonly List<Point> special = new();

    static readonly List<Point> vines = new();

    static SceneMetrics scanning;

    internal static SceneMetrics ScanningMetrics => scanning.IsNull ? Main.PlayerSceneMetrics : scanning;

    static GameHandle? drawHandle;

    static bool drawing;

    internal static int Scan(ref int[] cells, int x, int y, int width, int height, uint mask)
    {
        while (true)
        {
            int count;
            fixed (int* buffer = cells)
                count = BunnyLoader.Host.Native()->ScanTiles(x, y, width, height, mask, buffer, cells.Length / 3);
            if (count >= 0)
                return count;
            Array.Resize(ref cells, checked(cells.Length * 2));
        }
    }

    static void InstallDrawing(uint flags)
    {
        if ((flags & ((1u << 13) | (1u << 23))) != 0)
        {
            Once("Draw", () => On_TileDrawing.PostDrawTiles += Draw);
            Once("Special", () => On_TileDrawing.AddSpecialLegacyPoint_Point += AddSpecial);
            Once("SpecialXY", () => On_TileDrawing.AddSpecialLegacyPoint_int_int += AddSpecialXY);
        }

        if ((flags & (1u << 10)) != 0)
        {
            Once("Light", () => On_Lighting.LightTiles += Light);
            Once("ApplyLight", () => On_Main.DoUpdateInWorld += ApplyLight);
        }

        if ((flags & (1u << 9)) != 0)
            Once("Nearby", () => On_SceneMetrics.Scan += Nearby);
    }

    static void Light(On_Lighting.orig_LightTiles orig, int firstX, int lastX, int firstY, int lastY)
    {
        orig(firstX, lastX, firstY, lastY);
        lightCount = Scan(ref lightCells, firstX - 28, firstY - 28, lastX - firstX + 57, lastY - firstY + 57, 1u << 10);
        if (lightValues.Length < lightCount)
            Array.Resize(ref lightValues, lightCells.Length / 3);
        for (int k = 0; k < lightCount; k++)
        {
            int i = lightCells[k * 3], j = lightCells[k * 3 + 1], type = lightCells[k * 3 + 2];
            float r = 0, g = 0, b = 0;
            if (Main.tileLighted[type])
                TileLoader.GetTile(type)?.ModifyLight(i, j, ref r, ref g, ref b);
            foreach (GlobalTile global in TileLoader.Hooks.ModifyLight)
                global.ModifyLight(i, j, type, ref r, ref g, ref b);
            lightValues[k] = new Vector3(r, g, b);
        }
    }

    static void ApplyLight(On_Main.orig_DoUpdateInWorld orig, Main self)
    {
        orig(self);
        if (Main.gameMenu)
        {
            lightCount = 0;
            return;
        }

        for (int k = 0; k < lightCount; k++)
        {
            Vector3 color = lightValues[k];
            if (color.X == 0 && color.Y == 0 && color.Z == 0)
                continue;
            int i = lightCells[k * 3], j = lightCells[k * 3 + 1];
            Tile tile = Main.tile[i, j];
            if (!tile.active() || tile.type != lightCells[k * 3 + 2])
                continue;
            Lighting.AddLight(i, j, color.X, color.Y, color.Z);
        }
    }

    static void Nearby(On_SceneMetrics.orig_Scan orig, SceneMetrics self, SceneMetricsScanSettings settings)
    {
        orig(self, settings);
        int x = (int)settings.BiomeScanCenterPositionInWorld.X / 16, y = (int)settings.BiomeScanCenterPositionInWorld.Y / 16;
        int width = Main.buffScanAreaWidth, height = Main.buffScanAreaHeight;
        SceneMetrics outer = scanning;
        scanning = self;
        try
        {
            NearArea(x - width / 2, y - height / 2, width, height, false);
            if (settings.VisualScanArea.HasValue)
            {
                Rectangle area = settings.VisualScanArea.Value;
                NearArea(area.X, area.Y, area.Width, area.Height, true);
            }
        }
        finally
        {
            scanning = outer;
        }
    }

    static void NearArea(int x, int y, int width, int height, bool closer)
    {
        int count = Scan(ref nearbyCells, x, y, width, height, 1u << 9);
        for (int k = 0; k < count; k++)
        {
            int i = nearbyCells[k * 3], j = nearbyCells[k * 3 + 1], type = nearbyCells[k * 3 + 2];
            TileLoader.GetTile(type)?.NearbyEffects(i, j, closer);
            foreach (GlobalTile global in TileLoader.Hooks.NearbyEffects)
                global.NearbyEffects(i, j, type, closer);
        }
    }

    static void AddSpecial(On_TileDrawing.orig_AddSpecialLegacyPoint_Point orig, TileDrawing self, Point point)
    {
        if (drawing)
            special.Add(point);
        else
            orig(self, point);
    }

    static void AddSpecialXY(On_TileDrawing.orig_AddSpecialLegacyPoint_int_int orig, TileDrawing self, int x, int y)
    {
        if (drawing)
            special.Add(new Point(x, y));
        else
            orig(self, x, y);
    }

    internal static void AddVine(int i, int j)
    {
        if (drawing)
            vines.Add(new Point(i, j));
    }

    static void Draw(On_TileDrawing.orig_PostDrawTiles orig, TileDrawing self, bool solid, bool targets, bool intoTargets)
    {
        orig(self, solid, targets, intoTargets);
        if (solid || intoTargets || Main.gameMenu)
            return;
        int x = (int)Main.screenPosition.X / 16 - 2, y = (int)Main.screenPosition.Y / 16 - 2;
        int count = Scan(ref drawCells, x, y, Main.screenWidth / 16 + 5, Main.screenHeight / 16 + 5, (1u << 13) | (1u << 23));
        if (count == 0)
            return;
        SpriteBatch batch = Main.spriteBatch;
        bool begun = batch._beginCalled;
        if (!begun)
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, default, Main.Transform, true);
        bool toScreen = Main.drawToScreen;
        Main.drawToScreen = true;
        drawing = true;
        special.Clear();
        vines.Clear();
        if (drawHandle == null)
            drawHandle = new GameHandle(TileDrawInfo.New().Pointer);
        try
        {
            for (int k = 0; k < count; k++)
            {
                int i = drawCells[k * 3], j = drawCells[k * 3 + 1], type = drawCells[k * 3 + 2];
                try
                {
                    Tile cell = Main.tile[i, j];
                    if (!cell.active() || cell.type != type)
                        continue;
                    ModTile? mod = TileLoader.GetTile(type);
                    bool draw = true, hidden = TileLoader.Hooks.PreDraw.Length > 0 || TileLoader.Hooks.DrawEffects.Length > 0 || (mod != null && TileLoader.Has(mod, 13));
                    if (!hidden)
                    {
                        mod?.PostDraw(i, j, batch);
                        foreach (GlobalTile global in TileLoader.Hooks.PostDraw)
                            global.PostDraw(i, j, type, batch);
                        continue;
                    }

                    foreach (GlobalTile global in TileLoader.Hooks.PreDraw)
                        draw &= global.PreDraw(i, j, type, batch);
                    if (draw && mod != null)
                        draw = mod.PreDraw(i, j, batch);
                    var info = new TileDrawInfo(drawHandle.Target);
                    short fx = cell.frameX, fy = cell.frameY;
                    TileLoader.DrawData d = new()
                    {
                        FrameX = fx,
                        FrameY = fy,
                        Width = 16,
                        Height = 16
                    };
                    int half = 0;
                    BunnyLoader.Host.Native()->ReadTileDrawData(self.Pointer, i, j, cell._tileOffset, type, &d, &half);
                    fx = (short)d.FrameX;
                    fy = (short)d.FrameY;
                    int width = d.Width, height = d.Height, top = d.Top, ax = d.AddX, ay = d.AddY;
                    SpriteEffects effects = d.Effects;
                    info.spriteBatch = batch;
                    info.typeCache = (ushort)type;
                    info.tileIndex = cell._tileOffset;
                    info.tileFrameX = fx;
                    info.tileFrameY = fy;
                    info.tileWidth = width;
                    info.tileHeight = height;
                    info.tileTop = top;
                    info.halfBrickHeight = half;
                    info.addFrX = ax;
                    info.addFrY = ay;
                    info.tileSpriteEffect = effects;
                    info.tileLight = Lighting.GetColor(i, j);
                    info.finalColor = info.tileLight;
                    info.drawTexture = TextureAssets.Tile[type].Value;
                    info.glowTexture = default;
                    info.glowSourceRect = default;
                    info.glowColor = default;
                    foreach (GlobalTile global in TileLoader.Hooks.DrawEffects)
                        global.DrawEffects(i, j, type, batch, ref info);
                    mod?.DrawEffects(i, j, batch, ref info);
                    if (draw && hidden && info.tileWidth > 0 && info.tileHeight > info.halfBrickHeight)
                    {
                        var source = new Rectangle(info.tileFrameX + info.addFrX, info.tileFrameY + info.addFrY, info.tileWidth, info.tileHeight - info.halfBrickHeight);
                        float center = Terraria.ID.TileID.Sets.DoNotAdjustDrawPositionBasedOnTileWidth[type] ? 0 : (info.tileWidth - 16) * 0.5f;
                        Vector2 position = new Vector2(i * 16 - center - (int)Main.screenPosition.X, j * 16 + info.tileTop + info.halfBrickHeight - (int)Main.screenPosition.Y);
                        batch.Draw(info.drawTexture, position, source, info.tileLight, 0, Vector2.Zero, 1, info.tileSpriteEffect, 0);
                    }

                    mod?.PostDraw(i, j, batch);
                    foreach (GlobalTile global in TileLoader.Hooks.PostDraw)
                        global.PostDraw(i, j, type, batch);
                }
                catch (Exception e)
                {
                    ItemHooks.Report("TileLoader.Draw", e);
                }
            }

            for (int k = 0; k < special.Count; k++)
            {
                Point point = special[k];
                int type = Main.tile[point.X, point.Y].type;
                TileLoader.GetTile(type)?.SpecialDraw(point.X, point.Y, batch);
                foreach (GlobalTile global in TileLoader.Hooks.SpecialDraw)
                    global.SpecialDraw(point.X, point.Y, type, batch);
            }

            for (int k = 0; k < vines.Count; k++)
            {
                Point point = vines[k];
                var data = TileObjectData.GetTileData(Main.tile[point.X, point.Y]);
                if (!data.IsNull)
                    self.DrawMultiTileVinesInWind(Main.screenPosition, Vector2.Zero, point.X, point.Y, data.Width, data.Height);
            }
        }
        finally
        {
            drawing = false;
            Main.drawToScreen = toScreen;
            if (!begun)
                batch.End();
        }
    }
}
