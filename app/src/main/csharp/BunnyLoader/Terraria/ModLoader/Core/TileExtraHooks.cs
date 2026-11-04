using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.Drawing;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe partial class TileHooks
{
    static readonly HashSet<int>[] modTorches = new HashSet<int>[256];

    internal static HashSet<int> ModTorches(int player) => modTorches[player] ??= new HashSet<int>();

    internal static void InstallExtras(ModTile tile)
    {
        if (TileLoader.Overrides(tile, nameof(ModTile.GetTorchLuck)))
        {
            Once("TorchRow", () => On_Player.TryRecalculatingTorchLuck += TorchRow);
            Once("TorchLuck", () => On_Player.UpdateTorchLuck_ConsumeCountersAndCalculate += TorchLuck);
        }

        if (TileLoader.Overrides(tile, nameof(ModTile.GetTileFlameData)))
            Once("TileFlame", () => On_TileDrawing.GetTileFlameData += TileFlame);
        if (TileLoader.Overrides(tile, nameof(ModTile.AdjustMultiTileVineParameters)))
            Once("VineWind", () => On_TileDrawing.DrawMultiTileVinesInWind += VineWind);
    }

    static void EmitParticles(TileDrawing self, ModTile? mod, Tile cell, int i, int j, int type)
    {
        bool own = mod != null && TileLoader.EmitsParticles(type);
        if ((!own && TileLoader.Hooks.EmitParticles.Length == 0) || !self._isActiveAndNotPaused || Main.rand.Next(4) != 0)
            return;
        short frameX = cell.frameX, frameY = cell.frameY;
        Color light = Lighting.GetColor(i, j);
        bool visible = TileDrawing.IsVisible(cell);
        if (own)
            mod!.EmitParticles(i, j, cell, frameX, frameY, light, visible);
        foreach (GlobalTile global in TileLoader.Hooks.EmitParticles)
            global.EmitParticles(i, j, cell, (ushort)type, frameX, frameY, light, visible);
    }

    static void TorchRow(On_Player.orig_TryRecalculatingTorchLuck orig, Player self)
    {
        if (!self.happyFunTorchTime)
        {
            Vector2 center = self._nextTorchLuckCheckCenter;
            int row = (int)center.Y / 16 - 40 + self.luckyTorchCounter;
            if (row >= 10 && row <= Main.maxTilesY - 10)
            {
                int first = Utils.Clamp((int)center.X / 16 - 40, 10, Main.maxTilesX - 10);
                int last = Utils.Clamp((int)center.X / 16 + 40, 10, Main.maxTilesX - 10);
                for (int x = first; x <= last; x++)
                {
                    Tile tile = Main.tile[x, row];
                    if (tile.active() && TileID.Sets.Torches[tile.type] && TileLoader.GetTile(tile.type) != null && tile.frameX >= 0 && tile.frameX < 66)
                        ModTorches(self.whoAmI).Add(tile.type);
                }
            }
        }

        orig(self);
    }

    static void TorchLuck(On_Player.orig_UpdateTorchLuck_ConsumeCountersAndCalculate orig, Player self)
    {
        HashSet<int> torches = ModTorches(self.whoAmI);
        int held = self.inventory[self.selectedItem].createTile;
        if (held >= 0 && TileLoader.GetTile(held) != null && TileID.Sets.Torches[held])
            torches.Add(held);
        orig(self);
        if (torches.Count == 0)
            return;
        float positive = 0f, negative = 0f;
        foreach (int type in torches)
        {
            float luck = TileLoader.GetTile(type)!.GetTorchLuck(self);
            if (luck > 0f)
                positive += luck;
            else
                negative -= luck;
        }

        torches.Clear();
        float result = self.torchLuck;
        if (positive >= 1f)
            result += 1f;
        else if (positive > 0f)
            result += 0.5f;
        if (negative >= 2f)
            result -= 1.5f;
        else if (negative >= 1f)
            result -= 1f;
        else if (negative > 0f)
            result -= 0.5f;
        self.torchLuck = MathHelper.Clamp(result, 0f, 1f);
    }

    static TileDrawing.TileFlameData TileFlame(On_TileDrawing.orig_GetTileFlameData orig, TileDrawing self, int tileX, int tileY, int type, int tileFrameY)
    {
        TileDrawing.TileFlameData data = orig(self, tileX, tileY, type, tileFrameY);
        TileLoader.GetTile(type)?.GetTileFlameData(tileX, tileY, ref data);
        return data;
    }

    static void VineWind(On_TileDrawing.orig_DrawMultiTileVinesInWind orig, TileDrawing self, Vector2 screenPosition, Vector2 offSet, int topLeftX, int topLeftY, int sizeX, int sizeY)
    {
        Tile top = Main.tile[topLeftX, topLeftY];
        int type = top.type;
        if (TileLoader.GetTile(type) is not ModTile mod || !TileLoader.Overrides(mod, nameof(ModTile.AdjustMultiTileVineParameters)))
        {
            orig(self, screenPosition, offSet, topLeftX, topLeftY, sizeX, sizeY);
            return;
        }

        float cycle = self.GetWindCycle(topLeftX, topLeftY, self._sunflowerWindCounter);
        float wind = cycle + self.GetHighestWindGridPushComplex(topLeftX, topLeftY, sizeX, sizeY, 60, 1.26f, 3, true);
        Vector2 anchor = new Vector2(topLeftX * 16 - (int)screenPosition.X + sizeX * 16f * 0.5f, topLeftY * 16 - (int)screenPosition.Y) + offSet;
        Vector2 shift = new Vector2(0f, -2f);
        bool underPlatform = true;
        for (int k = 0; k < sizeX && underPlatform; k++)
            underPlatform = WorldGen.IsBelowANonHammeredPlatform(topLeftX + k, topLeftY);
        if (underPlatform)
            shift.Y -= 8f;
        anchor += shift;

        Texture2D glow = default;
        Color glowColor = Color.Transparent;
        float? overrideCycle = null;
        float pushX = 1f, pushY = -4f, multiplier = 0.15f;
        bool steadyTop = false;
        mod.AdjustMultiTileVineParameters(topLeftX, topLeftY, ref overrideCycle, ref pushX, ref pushY, ref steadyTop, ref multiplier, ref glow, ref glowColor);
        if (steadyTop)
            anchor += new Vector2(0f, 16f);
        float spin = multiplier * -1f;
        if (!self.InAPlaceWithWind(topLeftX, topLeftY, sizeX, sizeY))
            wind -= cycle;

        SpriteBatch batch = Main.spriteBatch;
        ulong seed = 0;
        for (int x = topLeftX; x < topLeftX + sizeX; x++)
        {
            for (int y = topLeftY; y < topLeftY + sizeY; y++)
            {
                Tile cell = Main.tile[x, y];
                if (cell.type != type || !TileDrawing.IsVisible(cell))
                    continue;
                float height = (y - topLeftY + 1) / (float)sizeY;
                if (overrideCycle.HasValue)
                    height = overrideCycle.Value;
                if (steadyTop && y == topLeftY)
                    height = 0f;

                TileLoader.DrawData d = new() { FrameX = cell.frameX, FrameY = cell.frameY, Width = 16, Height = 16 };
                int half = 0;
                BunnyLoader.Host.Native()->ReadTileDrawData(self.Pointer, x, y, cell._tileOffset, type, &d, &half);
                Texture2D texture = self.GetTileDrawTexture(cell, x, y, out _, false);
                if (texture.IsNull)
                    continue;
                Vector2 place = new Vector2(x * 16 - (int)screenPosition.X, y * 16 - (int)screenPosition.Y + d.Top) + offSet + shift;
                Vector2 origin = anchor - place;
                Vector2 position = anchor + new Vector2(0f, Math.Abs(wind) * pushY * height);
                position.X += wind * pushX;
                Rectangle source = new Rectangle(d.FrameX + d.AddX, d.FrameY + d.AddY, d.Width, d.Height - half);
                float rotation = wind * spin * height;
                batch.Draw(texture, position, source, Lighting.GetColor(x, y), rotation, origin, 1f, d.Effects, 0f);
                if (!glow.IsNull)
                    batch.Draw(glow, position, source, glowColor, rotation, origin, 1f, d.Effects, 0f);

                TileDrawing.TileFlameData flame = self.GetTileFlameData(x, y, type, d.FrameY);
                if (seed == 0)
                    seed = flame.flameSeed;
                flame.flameSeed = seed;
                for (int k = 0; k < flame.flameCount; k++)
                {
                    float jitterX = Utils.RandomInt(ref flame.flameSeed, flame.flameRangeXMin, flame.flameRangeXMax) * flame.flameRangeMultX;
                    float jitterY = Utils.RandomInt(ref flame.flameSeed, flame.flameRangeYMin, flame.flameRangeYMax) * flame.flameRangeMultY;
                    batch.Draw(flame.flameTexture, position + new Vector2(jitterX, jitterY), source, flame.flameColor, rotation, origin, 1f, d.Effects, 0f);
                }
            }
        }
    }
}
