using System;
using System.Collections.Generic;
using System.Diagnostics;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CsPrim;

public sealed class DrawSystem : ModSystem
{
    static BasicEffect? basic;

    static Effect trail;

    static Terraria.Graphics.Shaders.MiscShaderData projected;

    static Texture2D quadrants;

    static VertexBuffer? vertexBuffer;

    static IndexBuffer? indexBuffer;

    static bool checkedOnce, cellsLogged, ended;

    static int pass, fail, drawnFrames;

    static ILog Log => ModContent.GetInstance<CsPrimMod>().Logger;

    static GraphicsDevice Device => Main.instance.GraphicsDevice;

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (!CsPrimMod.Ran) return;

        spriteBatch.End();

        try
        {
            Prepare();
            if (!checkedOnce) Checks();
            Cells();
            drawnFrames++;
        }
        catch (Exception e)
        {
            if (!ended) Report("draw", e.GetType().Name + ": " + e.Message + " " + e.StackTrace);
            ended = true;
        }
        finally
        {
            EffectPass.BoundEffect = default;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        if (!ended && drawnFrames > 120)
        {
            ended = true;
            Log.Info($"cstprim END pass={pass} fail={fail}");
        }
    }

    static bool worldLogged;

    public override void PostDrawTiles()
    {
        if (!CsPrimMod.Ran || basic == null) return;

        Player player = Main.LocalPlayer;
        Vector2 center = player.Center + new Vector2(70, 0);
        basic.World = Matrix.CreateTranslation(-Main.screenPosition.X, -Main.screenPosition.Y, 0);
        basic.View = Main.GameViewMatrix.TransformationMatrix;
        basic.Projection = Projection;
        basic.TextureEnabled = false;
        basic.VertexColorEnabled = true;
        basic.CurrentTechnique.Passes[0].Apply();
        Device.DrawUserPrimitives(PrimitiveType.TriangleList, Quad(center.X - 20, center.Y - 20, 40, 40, Color.Magenta, out _), 0, 2);

        Vector2 left = player.Center + new Vector2(-70, 0) - Main.screenPosition;
        Main.instance.GraphicsDevice.Textures[0] = TextureAssets.MagicPixel.Value;
        projected.Apply();
        Device.DrawUserPrimitives(PrimitiveType.TriangleList, Textured(left.X - 20, left.Y - 20, 40, 40), 0, 2);
        EffectPass.BoundEffect = default;

        if (!worldLogged)
        {
            worldLogged = true;
            Vector2 screen = center - Main.screenPosition;
            Log.Info($"cstprim worldcell world_magenta {screen.X / Main.screenWidth:0.0000},{screen.Y / Main.screenHeight:0.0000} 255,0,255 world screen {Main.screenWidth}x{Main.screenHeight}");
            Log.Info($"cstprim worldcell misc_projection {left.X / Main.screenWidth:0.0000},{left.Y / Main.screenHeight:0.0000} 128,128,0");
        }
    }

    static void Prepare()
    {
        if (basic != null) return;

        basic = new BasicEffect(Device);
        trail = ModContent.Request<Effect>("CsPrim/Effects/Trail", AssetRequestMode.ImmediateLoad).Value;
        projected = new Terraria.Graphics.Shaders.MiscShaderData(ModContent.Request<Effect>("CsPrim/Effects/Projected", AssetRequestMode.ImmediateLoad), "Projected")
            .UseProjectionMatrix(true).UseOpacity(0.25f);
        Terraria.Graphics.Shaders.GameShaders.Misc["CsPrimProjected"] = projected;
        quadrants = new Texture2D(Device, 16, 16);
        var pixels = new Color[256];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                pixels[y * 16 + x] = y < 8 ? (x < 8 ? Color.Red : Color.Lime) : (x < 8 ? Color.Blue : Color.White);
        quadrants.SetData(pixels);

        vertexBuffer = new VertexBuffer(Device, typeof(VertexPositionColor), 4, BufferUsage.WriteOnly);
        indexBuffer = new IndexBuffer(Device, IndexElementSize.ThirtyTwoBits, 6, BufferUsage.WriteOnly);
        indexBuffer.SetData(new[] { 0, 1, 2, 2, 1, 3 });
    }

    static Matrix Projection => Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

    static void Cells()
    {
        var cells = new List<string>();

        basic!.World = Matrix.Identity;
        basic.View = Matrix.Identity;
        basic.Projection = Projection;
        basic.TextureEnabled = false;
        basic.VertexColorEnabled = true;
        basic.CurrentTechnique.Passes[0].Apply();
        Device.DrawUserPrimitives(PrimitiveType.TriangleList, Quad(100, 100, 80, 60, Color.Red, out _), 0, 2);
        cells.Add(Cell("list_red", 140, 130, Color.Red));

        basic.TextureEnabled = true;
        basic.Texture = quadrants;
        basic.CurrentTechnique.Passes[0].Apply();
        VertexPositionColorTexture[] textured = Textured(200, 100, 80, 60);
        Device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, textured, 0, 4, new short[] { 0, 1, 2, 2, 1, 3 }, 0, 2);
        cells.Add(Cell("texture_top_left", 210, 108, Color.Red));
        cells.Add(Cell("texture_top_right", 270, 108, Color.Lime));
        cells.Add(Cell("texture_bottom_left", 210, 152, Color.Blue));
        cells.Add(Cell("texture_bottom_right", 270, 152, Color.White));

        basic.TextureEnabled = false;
        basic.CurrentTechnique.Passes[0].Apply();
        Quad(300, 100, 80, 60, Color.Lime, out VertexPositionColor[] strip);
        Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, strip, 0, 2);
        cells.Add(Cell("strip_green", 340, 130, Color.Lime));

        basic.DiffuseColor = new Vector3(1, 1, 0);
        basic.CurrentTechnique.Passes[0].Apply();
        var line = new[] { new VertexPositionColor(new Vector3(400, 130, 0), Color.White), new VertexPositionColor(new Vector3(480, 130, 0), Color.White) };
        for (int k = -2; k <= 2; k++)
        {
            line[0].Position.Y = line[1].Position.Y = 130 + k * 0.5f;
            Device.DrawUserPrimitives(PrimitiveType.LineList, line, 0, 1);
        }
        basic.DiffuseColor = Vector3.One;
        cells.Add(Cell("line_yellow", 440, 130, new Color(255, 255, 0)));

        Quad(500, 100, 80, 60, Color.Blue, out VertexPositionColor[] corners);
        vertexBuffer!.SetData(corners);
        Device.SetVertexBuffer(vertexBuffer);
        Main.instance.GraphicsDevice.Indices = indexBuffer!;
        Main.instance.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        basic.CurrentTechnique.Passes[0].Apply();
        Device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 4, 0, 2);
        Device.SetVertexBuffer(null!);
        cells.Add(Cell("buffers_blue", 540, 130, Color.Blue));

        basic.TextureEnabled = false;
        basic.CurrentTechnique.Passes[0].Apply();
        Device.DrawUserPrimitives(PrimitiveType.TriangleList, Quad(600, 200, 60, 60, Color.Red, out _), 0, 2);
        Device.DrawUserPrimitives(PrimitiveType.TriangleList, Quad(680, 200, 60, 60, Color.Lime, out _), 0, 2);
        GraphicsDevice blended = Main.instance.GraphicsDevice;
        blended.BlendState = BlendState.Additive;
        basic.CurrentTechnique.Passes[0].Apply();
        Device.DrawUserPrimitives(PrimitiveType.TriangleList, Quad(600, 200, 60, 60, new Color(128, 128, 128), out _), 0, 2);
        blended.BlendState = BlendState.NonPremultiplied;
        basic.CurrentTechnique.Passes[0].Apply();
        Device.DrawUserPrimitives(PrimitiveType.TriangleList, Quad(680, 200, 60, 60, new Color(0, 0, 255, 128), out _), 0, 2);
        blended.BlendState = BlendState.AlphaBlend;
        cells.Add(Cell("blend_additive", 630, 230, new Color(255, 128, 128)));
        cells.Add(Cell("blend_nonpremultiplied", 710, 230, new Color(0, 128, 128)));

        Device.Textures[0] = TextureAssets.MagicPixel.Value;
        trail.Parameters["uWorldViewProjection"].SetValue(Projection);
        trail.CurrentTechnique.Passes["Trail"].Apply();
        Device.DrawUserPrimitives(PrimitiveType.TriangleList, Textured(100, 200, 200, 100), 0, 2);
        cells.Add(Cell("fxc_top_left", 130, 215, new Color(38, 38, 0)));
        cells.Add(Cell("fxc_top_right", 270, 215, new Color(217, 38, 0)));
        cells.Add(Cell("fxc_bottom_left", 130, 285, new Color(38, 217, 0)));

        if (!cellsLogged)
        {
            cellsLogged = true;
            Log.Info($"cstprim screen {Main.screenWidth}x{Main.screenHeight}");
            foreach (string c in cells) Log.Info(c);
        }
    }

    static string Cell(string name, int x, int y, Color c) => $"cstprim cell {name} {x},{y} {c.R},{c.G},{c.B}";

    static VertexPositionColor[] Quad(float x, float y, float w, float h, Color color, out VertexPositionColor[] corners)
    {
        corners = new[]
        {
            new VertexPositionColor(new Vector3(x, y, 0), color),
            new VertexPositionColor(new Vector3(x + w, y, 0), color),
            new VertexPositionColor(new Vector3(x, y + h, 0), color),
            new VertexPositionColor(new Vector3(x + w, y + h, 0), color),
        };

        return new[] { corners[0], corners[1], corners[2], corners[2], corners[1], corners[3] };
    }

    static VertexPositionColorTexture[] Textured(float x, float y, float w, float h) => new[]
    {
        new VertexPositionColorTexture(new Vector3(x, y, 0), Color.White, new Vector2(0, 0)),
        new VertexPositionColorTexture(new Vector3(x + w, y, 0), Color.White, new Vector2(1, 0)),
        new VertexPositionColorTexture(new Vector3(x, y + h, 0), Color.White, new Vector2(0, 1)),
        new VertexPositionColorTexture(new Vector3(x + w, y + h, 0), Color.White, new Vector2(1, 1)),
        new VertexPositionColorTexture(new Vector3(x, y + h, 0), Color.White, new Vector2(0, 1)),
        new VertexPositionColorTexture(new Vector3(x + w, y, 0), Color.White, new Vector2(1, 0)),
    };

    static void Report(string name, string? problem)
    {
        if (problem == null) pass++;
        else fail++;

        Log.Info($"cstprim {name}: {(problem == null ? "ok" : "FAIL " + problem)}");
    }

    static void Check(string name, Func<string?> test)
    {
        string? problem;
        try { problem = test(); }
        catch (Exception e) { problem = e.GetType().Name + ": " + e.Message; }
        Report(name, problem);
    }

    static void Throws<T>(string name, Action action) where T : Exception => Check(name, () =>
    {
        try { action(); }
        catch (T e) { return e.Message.Length > 0 ? null : "empty message"; }
        return $"no {typeof(T).Name}";
    });

    static bool Near(Matrix a, Matrix b)
    {
        float[] x = { a.M11, a.M12, a.M13, a.M14, a.M21, a.M22, a.M23, a.M24, a.M31, a.M32, a.M33, a.M34, a.M41, a.M42, a.M43, a.M44 };
        float[] y = { b.M11, b.M12, b.M13, b.M14, b.M21, b.M22, b.M23, b.M24, b.M31, b.M32, b.M33, b.M34, b.M41, b.M42, b.M43, b.M44 };
        for (int i = 0; i < 16; i++)
            if (MathF.Abs(x[i] - y[i]) > 1e-3f) return false;
        return true;
    }

    static void Checks()
    {
        checkedOnce = true;

        Check("matrix_multiply", () =>
        {
            Matrix a = Matrix.CreateRotationZ(0.7f) * Matrix.CreateTranslation(3, 4, 5);
            Matrix b = Matrix.CreateScale(2, 3, 4);
            return Near(a * b, Matrix.Multiply(a, b)) ? null : $"{a * b} vs {Matrix.Multiply(a, b)}";
        });

        Check("matrix_equality", () => Matrix.Identity == Matrix.CreateScale(1) && Matrix.Identity != Matrix.CreateScale(2) ? null : "== or != wrong");

        Check("parameter_matrix", () =>
        {
            EffectParameter p = trail.Parameters["uWorldViewProjection"];
            Matrix m = Matrix.CreateTranslation(1, 2, 3);
            p.SetValue(m);
            return p.GetValueMatrix() == m ? null : p.GetValueMatrix().ToString();
        });

        Check("parameter_vector", () =>
        {
            EffectParameter p = trail.Parameters["uWorldViewProjection"];
            p.SetValue(new Vector4(1, 2, 3, 4));
            return p.GetValueVector4().Equals(new Vector4(1, 2, 3, 4)) ? null : p.GetValueVector4().ToString();
        });

        Check("declaration_stride", () => VertexPositionColorTexture.VertexDeclaration.VertexStride == 24 && VertexPositionColor.VertexDeclaration.VertexStride == 16 ? null : "stride");

        Check("vertex_buffer_round_trip", () =>
        {
            var buffer = new VertexBuffer(Device, VertexPositionColor.VertexDeclaration, 2, BufferUsage.None);
            var data = new[] { new VertexPositionColor(new Vector3(1, 2, 3), Color.Red), new VertexPositionColor(new Vector3(4, 5, 6), Color.Blue) };
            buffer.SetData(data);
            var back = new VertexPositionColor[2];
            buffer.GetData(back);
            return back[0] == data[0] && back[1] == data[1] ? null : $"{back[0]} {back[1]}";
        });

        Throws<InvalidOperationException>("error_no_pass", () =>
        {
            EffectPass.BoundEffect = default;
            Device.DrawUserPrimitives(PrimitiveType.TriangleList, Quad(0, 0, 1, 1, Color.Red, out _), 0, 2);
        });

        Throws<NotSupportedException>("error_points", () =>
        {
            basic!.CurrentTechnique.Passes[0].Apply();
            Device.DrawUserPrimitives(PrimitiveType.PointListEXT, Quad(0, 0, 1, 1, Color.Red, out _), 0, 2);
        });

        Throws<ArgumentOutOfRangeException>("error_index", () =>
        {
            basic!.CurrentTechnique.Passes[0].Apply();
            Device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, Quad(0, 0, 1, 1, Color.Red, out _), 0, 3, new short[] { 0, 1, 7 }, 0, 1);
        });

        Check("lighting_ignored", () =>
        {
            basic!.LightingEnabled = true;
            bool kept = basic.LightingEnabled;
            basic.LightingEnabled = false;
            return kept ? null : "LightingEnabled did not keep its value";
        });

        Throws<NotSupportedException>("error_get_write_only", () => indexBuffer!.GetData(new int[6]));

        Check("misc_apply_without_arguments", () =>
        {
            projected.Apply();
            float opacity = projected.Shader.Parameters["uOpacity"].GetValueSingle();
            return MathF.Abs(opacity - 0.25f) < 1e-4f ? null : $"uOpacity {opacity}";
        });

        Check("misc_projection_matrix", () =>
        {
            projected.Apply();
            Matrix m = projected.Shader.Parameters["MatrixTransform"].GetValueMatrix();
            return Near(m, Main.GameViewMatrix.NormalizedTransformationMatrix) ? null : m.ToString();
        });

        Check("normalized_matches_pc_formula", () =>
        {
            float w = Main.screenWidth, h = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            float tx = MathF.Round(w * 0.5f - w * 0.5f / zoom.X), ty = MathF.Round(h * 0.5f - h * 0.5f / zoom.Y);
            Matrix expected = Matrix.CreateTranslation(-tx, -ty, 0) * Matrix.CreateScale(zoom.X, zoom.Y, 1) * Matrix.CreateTranslation(1f / 256, 1f / 256, 0)
                * Matrix.CreateTranslation(-0.5f, -0.5f, 0) * Matrix.CreateOrthographicOffCenter(0, w, h, 0, 0, 1);
            Matrix got = Main.GameViewMatrix.NormalizedTransformationMatrix;
            return Near(got, expected) ? null : $"{got} vs {expected}";
        });

        Check("matrix_to_string", () => Matrix.Identity.ToString().StartsWith("{M11:1 M12:0") && !Matrix.Identity.ToString().StartsWith("{ ") ? null : Matrix.Identity.ToString());

        Check("perf", () =>
        {
            basic!.World = Matrix.Identity;
            basic.View = Matrix.Identity;
            basic.Projection = Projection;
            basic.TextureEnabled = false;
            var stripVertices = new VertexPositionColor[2000];
            for (int i = 0; i < stripVertices.Length; i++)
                stripVertices[i] = new VertexPositionColor(new Vector3(-50 + i % 2, -50 + i / 2 % 10, 0), Color.Transparent);

            basic.CurrentTechnique.Passes[0].Apply();
            Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, stripVertices, 0, 1998);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++) Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, stripVertices, 0, 1998);
            double strip = watch.Elapsed.TotalMilliseconds * 1000 / 20;

            VertexPositionColor[] quad = Quad(-50, -50, 1, 1, Color.Transparent, out _);
            watch.Restart();
            for (int i = 0; i < 200; i++) Device.DrawUserPrimitives(PrimitiveType.TriangleList, quad, 0, 2);
            double small = watch.Elapsed.TotalMilliseconds * 1000 / 200;

            var raw = BunnyLoader.Interop.GameArray<long>.New(new BunnyLoader.Interop.GameClass("Microsoft.Xna.Framework.Graphics", "SpriteBatcher", "VertexData"), 4);
            BunnyLoader.Interop.GameArray<short> rawIndices = new short[] { 0, 1, 2, 2, 1, 3 };
            GraphicsDevice device = GraphicsDevice.Instance;
            watch.Restart();
            for (int i = 0; i < 200; i++) device.DrawGeometry(new BunnyLoader.Interop.GameObject(raw.Pointer), rawIndices, 4, 6, Vector3.Zero, Vector3.One);
            double game = watch.Elapsed.TotalMilliseconds * 1000 / 200;

            basic.CurrentTechnique.Passes[0].Apply();
            watch.Restart();
            for (int i = 0; i < 200; i++) Device.DrawUserPrimitives(PrimitiveType.TriangleList, quad, 0, 2);
            double warm = watch.Elapsed.TotalMilliseconds * 1000 / 200;

            watch.Restart();
            for (int i = 0; i < 200; i++) basic.CurrentTechnique.Passes[0].Apply();
            double apply = watch.Elapsed.TotalMilliseconds * 1000 / 200;

            Log.Info($"cstprim perf strip 2000 vertices {strip:0} us, quad {small:0.0} us (game pool growing), quad {warm:0.0} us (warm), game DrawGeometry alone {game:0.0} us, pass Apply {apply:0.0} us");
            return null;
        });
    }
}
