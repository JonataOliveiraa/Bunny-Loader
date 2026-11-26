using System;
using System.Diagnostics;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CsPrimPerf;

public sealed class CsPrimPerfMod : Mod
{
}

public sealed class PerfSystem : ModSystem
{
    const int PhaseFrames = 240;

    const int Skip = 30;

    const int TrailVertices = 60;

    static readonly string[] Phases = { "idle", "trails20", "trails100", "indexed20", "raw20", "bigarray20" };

    static BasicEffect? basic;

    static MiscShaderData projected;

    static VertexPositionColorTexture[] strip = new VertexPositionColorTexture[TrailVertices];

    static VertexPositionColorTexture[] quads = new VertexPositionColorTexture[TrailVertices];

    static short[] quadIndices = new short[(TrailVertices / 2 - 1) * 6];

    static VertexPositionColorTexture[] big = new VertexPositionColorTexture[4000];

    static GameArray<long> rawVertices;

    static GameArray<short> rawIndices;

    static readonly System.Collections.Generic.List<GameHandle> kept = new();

    static int frame = -120, phase;

    static long lastStamp;

    static double frameTotal, drawTotal;

    static int measured, calls;

    static bool micro, ended;

    static ILog Log => ModContent.GetInstance<CsPrimPerfMod>().Logger;

    public override void PostDrawTiles()
    {
        if (Main.gameMenu || ended) return;

        long now = Stopwatch.GetTimestamp();
        double frameMs = lastStamp == 0 ? 0 : (now - lastStamp) * 1000.0 / Stopwatch.Frequency;
        lastStamp = now;

        if (++frame < 0) return;

        if (!micro)
        {
            micro = true;
            Prepare();
            Micro();
            return;
        }

        int inPhase = frame % PhaseFrames;
        long start = Stopwatch.GetTimestamp();
        int drawn = Draw(Phases[phase]);
        double drawMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

        if (inPhase >= Skip)
        {
            frameTotal += frameMs;
            drawTotal += drawMs;
            calls += drawn;
            measured++;
        }

        if (inPhase != PhaseFrames - 1) return;

        double frameAvg = frameTotal / measured, drawAvg = drawTotal / measured;
        double perCall = calls == 0 ? 0 : drawTotal * 1000 / calls;
        Log.Info($"cstprimperf phase {Phases[phase]}: frame {frameAvg:0.00} ms ({1000 / frameAvg:0.0} fps), draws {drawAvg:0.000} ms/frame, {perCall:0.0} us per call, {calls / measured} calls/frame");
        frameTotal = drawTotal = 0;
        measured = calls = 0;

        if (++phase < Phases.Length) return;
        ended = true;
        Log.Info("cstprimperf END");
    }

    static void Prepare()
    {
        basic = new BasicEffect(Main.instance.GraphicsDevice) { VertexColorEnabled = true, TextureEnabled = false };
        projected = new MiscShaderData(ModContent.Request<Effect>("CsPrimPerf/Effects/Projected", AssetRequestMode.ImmediateLoad), "Projected").UseProjectionMatrix(true);
        GameShaders.Misc["CsPrimPerfProjected"] = projected;

        for (int q = 0; q < TrailVertices / 2 - 1; q++)
        {
            int b = q * 2;
            short[] six = { (short)b, (short)(b + 1), (short)(b + 2), (short)(b + 2), (short)(b + 1), (short)(b + 3) };
            six.CopyTo(quadIndices, q * 6);
        }

        for (int i = 0; i < big.Length; i++)
            big[i] = new VertexPositionColorTexture(new Vector3(i % 40, i / 40, 0), new Color(255, 255, 255, 60), Vector2.Zero);

        rawVertices = GameArray<long>.New(new GameClass("Microsoft.Xna.Framework.Graphics", "SpriteBatcher", "VertexData"), TrailVertices * 3 / 2 + 8);
        rawIndices = new short[(TrailVertices - 2) * 3];
        kept.Add(new GameHandle(rawVertices.Pointer));
        kept.Add(new GameHandle(rawIndices.Pointer));
        Span<short> ri = rawIndices.AsSpan();
        for (int i = 0; i < TrailVertices - 2; i++)
        {
            ri[i * 3] = (short)i;
            ri[i * 3 + 1] = (short)(i + 1);
            ri[i * 3 + 2] = (short)(i + 2);
        }
    }

    static void Shape(int trail, VertexPositionColorTexture[] target)
    {
        Vector2 origin = Main.LocalPlayer.Center - Main.screenPosition + new Vector2(-150 + trail % 10 * 30, -120 + trail / 10 * 12);
        float t = frame * 0.05f;
        for (int i = 0; i < TrailVertices; i++)
        {
            int along = i / 2;
            float x = origin.X + along * 4, y = origin.Y + MathF.Sin(t + along * 0.3f) * 6 + (i % 2 == 0 ? -3 : 3);
            target[i] = new VertexPositionColorTexture(new Vector3(x, y, 0), new Color(255, 160, 40, 90), new Vector2(along / (float)(TrailVertices / 2), i % 2));
        }
    }

    static int Draw(string name)
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;

        switch (name)
        {
            case "trails20":
            case "trails100":
                int count = name == "trails20" ? 20 : 100;
                basic!.World = Matrix.Identity;
                basic.View = Main.GameViewMatrix.TransformationMatrix;
                basic.Projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);
                for (int k = 0; k < count; k++)
                {
                    Shape(k, strip);
                    basic.CurrentTechnique.Passes[0].Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, strip, 0, TrailVertices - 2);
                }
                EffectPass.BoundEffect = default;
                return count;
            case "indexed20":
                device.Textures[0] = TextureAssets.MagicPixel.Value;
                for (int k = 0; k < 20; k++)
                {
                    Shape(k, quads);
                    projected.Apply();
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, quads, 0, TrailVertices, quadIndices, 0, quadIndices.Length / 3);
                }
                EffectPass.BoundEffect = default;
                return 20;
            case "raw20":
                basic!.CurrentTechnique.Passes[0].Apply();
                for (int k = 0; k < 20; k++)
                    device.DrawGeometry(new GameObject(rawVertices.Pointer), rawIndices, TrailVertices, (TrailVertices - 2) * 3, Vector3.Zero, Vector3.One);
                EffectPass.BoundEffect = default;
                return 20;
            case "bigarray20":
                basic!.World = Matrix.CreateTranslation(-200, -200, 0);
                basic.View = Matrix.Identity;
                basic.Projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);
                for (int k = 0; k < 20; k++)
                {
                    basic.CurrentTechnique.Passes[0].Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, big, k * 3, 2);
                }
                EffectPass.BoundEffect = default;
                return 20;
            default:
                return 0;
        }
    }

    static double PerCall(Action action, int times)
    {
        action();
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < times; i++) action();
        return (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / times;
    }

    static void Micro()
    {
        EffectParameter opacity = projected.Shader.Parameters["uOpacity"];
        EffectParameter matrix = projected.Shader.Parameters["MatrixTransform"];
        Matrix a = Matrix.CreateRotationZ(0.3f), b = Matrix.CreateTranslation(4, 5, 6);
        Player p = default;

        Log.Info($"cstprimperf micro: SetValue(float) {PerCall(() => opacity.SetValue(0.5f), 2000):0} ns, SetValue(Matrix) {PerCall(() => matrix.SetValue(a), 2000):0} ns, " +
                 $"GetValueMatrix {PerCall(() => _ = matrix.GetValueMatrix(), 2000):0} ns, Matrix * {PerCall(() => _ = a * b, 2000):0} ns, Matrix.Multiply (game) {PerCall(() => _ = Matrix.Multiply(a, b), 2000):0} ns, " +
                 $"Main.LocalPlayer {PerCall(() => p = Main.LocalPlayer, 2000):0} ns, Main.instance.GraphicsDevice {PerCall(() => _ = Main.instance.GraphicsDevice, 2000):0} ns, " +
                 $"BasicEffect.CurrentTechnique.Passes[0].Apply {PerCall(() => basic!.CurrentTechnique.Passes[0].Apply(), 500):0} ns, MiscShaderData.Apply {PerCall(() => projected.Apply(), 500):0} ns");
        EffectPass.BoundEffect = default;

        Shape(0, strip);
        basic!.World = Matrix.Identity;
        basic.View = Matrix.Identity;
        basic.Projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);
        basic.CurrentTechnique.Passes[0].Apply();
        GraphicsDevice device = Main.instance.GraphicsDevice;
        for (int i = 0; i < 300; i++) device.DrawUserPrimitives(PrimitiveType.TriangleStrip, strip, 0, TrailVertices - 2);
        double ours = PerCall(() => device.DrawUserPrimitives(PrimitiveType.TriangleStrip, strip, 0, TrailVertices - 2), 300);
        double game = PerCall(() => device.DrawGeometry(new GameObject(rawVertices.Pointer), rawIndices, TrailVertices, (TrailVertices - 2) * 3, Vector3.Zero, Vector3.One), 300);
        Log.Info($"cstprimperf micro keep: Shader {PerCall(() => _ = projected.Shader, 2000):0} ns, _transformMatrix {PerCall(() => _ = projected._transformMatrix, 2000):0} ns, _useProjectionMatrix {PerCall(() => _ = projected._useProjectionMatrix, 2000):0} ns, " +
                 $"Shader.Parameters {PerCall(() => _ = projected.Shader.Parameters, 2000):0} ns, Effects {PerCall(() => _ = Main.GameViewMatrix.Effects, 2000):0} ns, Zoom {PerCall(() => _ = Main.GameViewMatrix.Zoom, 2000):0} ns");
        Log.Info($"cstprimperf micro split: MiscShaderData.Apply_ (game only) {PerCall(() => projected.Apply_(default), 500):0} ns, NormalizedTransformationMatrix {PerCall(() => _ = Main.GameViewMatrix.NormalizedTransformationMatrix, 2000):0} ns, " +
                 $"Shape (test's own vertex generation) {PerCall(() => Shape(0, strip), 500) / 1000:0.0} us, DrawUserPrimitives 60-vertex strip {ours / 1000:0.0} us vs game DrawGeometry same size {game / 1000:0.0} us (warm pool)");
        EffectPass.BoundEffect = default;
    }
}
