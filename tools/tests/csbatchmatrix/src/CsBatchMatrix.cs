using System;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CsBatchMatrix;

public sealed class CsBatchMatrixMod : Mod
{
    internal static ILog Log => ModContent.GetInstance<CsBatchMatrixMod>().Logger;
}

public sealed class MatrixDraw : ModSystem
{
    const int Size = 20;
    const int End = 150;

    static int frames, pass, fail;
    static bool logged;

    static readonly Color Red = new(255, 0, 0), Green = new(0, 255, 0), Blue = new(0, 0, 255);
    static readonly Color Yellow = new(255, 255, 0), Magenta = new(255, 0, 255), Cyan = new(0, 255, 255), Orange = new(255, 128, 0);

    public override void OnWorldLoad()
    {
        frames = pass = fail = 0;
        logged = false;
    }

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (Main.gameMenu) return;
        frames++;

        try
        {
            spriteBatch.End();
            DrawCases(spriteBatch);
            Report("draw", null);
        }
        catch (Exception e)
        {
            Report("draw", e.GetType().Name + ": " + e.Message);
        }
        finally
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        if (frames == 60 && !logged)
        {
            logged = true;
            LogCells();
        }

        if (frames == End) CsBatchMatrixMod.Log.Info($"csbatchmatrix END pass={pass} fail={fail}");
    }

    static void Report(string name, string? problem)
    {
        if (frames != 30) return;
        if (problem == null) pass++;
        else fail++;
        CsBatchMatrixMod.Log.Info($"csbatchmatrix {name}: {(problem == null ? "ok" : "FAIL " + problem)}");
    }

    static void Square(SpriteBatch batch, float x, float y, Color color) =>
        batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)x, (int)y, Size, Size), new Rectangle(0, 0, 1, 1), color);

    static void DrawCases(SpriteBatch batch)
    {
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.CreateScale(2f));
        Square(batch, 50, 50, Red);
        batch.End();

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.CreateTranslation(150f, 0f, 0f));
        Square(batch, 50, 200, Green);
        batch.End();

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null,
            Matrix.CreateRotationZ(MathHelper.PiOver2) * Matrix.CreateTranslation(700f, 0f, 0f));
        Square(batch, 100, 300, Blue);
        batch.End();

        batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.CreateScale(1.5f));
        Square(batch, 200, 150, Yellow);
        batch.End();

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        Square(batch, 500, 300, Magenta);
        batch.End();

        batch.Begin(SpriteSortMode.Texture, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.CreateScale(2f));
        Square(batch, 300, 30, Cyan);
        batch.End();

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.CreateTranslation(0f, 350f, 0f));
        Square(batch, 600, 30, Orange);
        batch.End_();
    }

    static void LogCells()
    {
        ILog log = CsBatchMatrixMod.Log;
        Vector2 from = XNAUnityRunner.AdjustRealScreenForUI(new Vector2(0, XNAUnityRunner.ScreenHeight));
        Vector2 to = XNAUnityRunner.AdjustRealScreenForUI(new Vector2(XNAUnityRunner.ScreenWidth, 0));
        log.Info($"csbatchmatrix screen {from.X:0.###},{from.Y:0.###} {to.X:0.###},{to.Y:0.###}");
        log.Info(Cell("cell", "scale", 120, 120, Red));
        log.Info(Cell("absent", "scale_unscaled", 55, 55, Red));
        log.Info(Cell("cell", "translation", 210, 210, Green));
        log.Info(Cell("absent", "translation_origin", 60, 210, Green));
        log.Info(Cell("cell", "rotation", 390, 110, Blue));
        log.Info(Cell("absent", "rotation_origin", 110, 310, Blue));
        log.Info(Cell("cell", "immediate", 315, 240, Yellow));
        log.Info(Cell("absent", "immediate_unscaled", 205, 155, Yellow));
        log.Info(Cell("cell", "view_matrix", 510, 310, Magenta));
        log.Info(Cell("cell", "texture_sort", 620, 80, Cyan));
        log.Info(Cell("absent", "texture_sort_unscaled", 305, 35, Cyan));
        log.Info(Cell("cell", "game_end", 610, 390, Orange));
        log.Info(Cell("absent", "game_end_origin", 610, 40, Orange));
    }

    static string Cell(string kind, string name, int x, int y, Color c) => $"csbatchmatrix {kind} {name} {x},{y} {c.R},{c.G},{c.B}";
}
