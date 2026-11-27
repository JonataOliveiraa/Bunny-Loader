using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CsFnaPerf;

[StructLayout(LayoutKind.Sequential)]
public struct SeqVec2
{
    public float X, Y;

    public SeqVec2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static SeqVec2 operator +(SeqVec2 a, SeqVec2 b) => new(a.X + b.X, a.Y + b.Y);

    public static SeqVec2 operator *(SeqVec2 a, float s) => new(a.X * s, a.Y * s);
}

[StructLayout(LayoutKind.Explicit, Size = 8)]
public struct ExpVec2
{
    [FieldOffset(0)] public float X;
    [FieldOffset(4)] public float Y;

    public ExpVec2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static ExpVec2 operator +(ExpVec2 a, ExpVec2 b) => new(a.X + b.X, a.Y + b.Y);

    public static ExpVec2 operator *(ExpVec2 a, float s) => new(a.X * s, a.Y * s);
}

public sealed class CsFnaPerfMod : Mod
{
    internal static ILog Log => ModContent.GetInstance<CsFnaPerfMod>().Logger;

    internal static float Sink;

    public override void Load() => On_Player.Update += Update;

    static int frames, next;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = next = 0;
            Cases.Ready = false;
            Draws.Next = 0;
            return;
        }

        if (i != Main.myPlayer || ++frames < 90) return;

        if (!Cases.Ready) Cases.Setup();
        if (next < Cases.All.Length)
        {
            var (name, body) = Cases.All[next++];
            Report(name, Bench.Measure(body));
        }
    }

    internal static void Report(string name, (double Ns, double Bytes, int N) r) =>
        Log.Info($"csfnaperf {name} {r.Ns:0.0} ns/op {r.Bytes:0.0} B/op n={r.N}");

    internal static bool UpdateDone => Cases.Ready && next >= Cases.All.Length;
}

internal static class Bench
{
    internal static (double Ns, double Bytes, int N) Measure(Func<int, float> body)
    {
        CsFnaPerfMod.Sink += body(4);
        int n = 16;

        while (true)
        {
            long start = Stopwatch.GetTimestamp();
            CsFnaPerfMod.Sink += body(n);
            double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            if (ms >= 2.0 || n >= 1 << 22) break;
            n *= 2;
        }

        double best = double.MaxValue, bytes = 0;

        for (int r = 0; r < 5; r++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            CsFnaPerfMod.Sink += body(n);
            long ticks = Stopwatch.GetTimestamp() - start;
            long after = GC.GetAllocatedBytesForCurrentThread();
            best = Math.Min(best, ticks * 1e9 / Stopwatch.Frequency / n);
            if (r == 0) bytes = (double)(after - before) / n;
        }

        return (best, bytes, n);
    }

    internal static (double Ns, double Bytes, int N) Fixed(Func<int, float> body, int n)
    {
        CsFnaPerfMod.Sink += body(2);
        double best = double.MaxValue, bytes = 0;

        for (int r = 0; r < 5; r++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            CsFnaPerfMod.Sink += body(n);
            long ticks = Stopwatch.GetTimestamp() - start;
            long after = GC.GetAllocatedBytesForCurrentThread();
            best = Math.Min(best, ticks * 1e9 / Stopwatch.Frequency / n);
            if (r == 0) bytes = (double)(after - before) / n;
        }

        return (best, bytes, n);
    }
}

internal static class Cases
{
    internal static bool Ready;

    static readonly Vector2[] v2 = new Vector2[64];
    static readonly SeqVec2[] s2 = new SeqVec2[64];
    static readonly ExpVec2[] e2 = new ExpVec2[64];
    static readonly Vector3[] v3 = new Vector3[64];
    static readonly Vector4[] v4 = new Vector4[64];
    static readonly Point[] points = new Point[64];
    static readonly Rectangle[] rects = new Rectangle[64];
    static readonly Quaternion[] quats = new Quaternion[64];
    static readonly Matrix[] mats = new Matrix[16];
    static readonly Color[] colors = new Color[64];
    static readonly Plane[] planes = new Plane[64];
    static readonly BoundingBox[] boxes = new BoundingBox[64];
    static readonly BoundingSphere[] spheres = new BoundingSphere[64];
    static readonly Ray[] rays = new Ray[64];

    static readonly Dictionary<Point, int> byPoint = new();
    static readonly Dictionary<long, int> byLong = new();
    static readonly HashSet<Point16> point16s = new();
    static readonly Dictionary<Vector2, int> byVector = new();
    static readonly Dictionary<Color, int> byColor = new();
    static readonly Point[] lookups = new Point[1024];

    static BoundingFrustum frustum = null!;
    static Curve curve = null!;
    static Effect effect;
    static EffectParameter parameter;
    static byte[] fxc = Array.Empty<byte>();
    static Texture2D small, large;
    static readonly StringBuilder builder = new("Bunny Loader 123");

    internal static (string, Func<int, float>)[] All = Array.Empty<(string, Func<int, float>)>();

    internal static void Setup()
    {
        var random = new Random(7);
        float F() => (float)(random.NextDouble() * 200 - 100);

        for (int i = 0; i < 64; i++)
        {
            v2[i] = new Vector2(F(), F());
            s2[i] = new SeqVec2(v2[i].X, v2[i].Y);
            e2[i] = new ExpVec2(v2[i].X, v2[i].Y);
            v3[i] = new Vector3(F(), F(), F());
            v4[i] = new Vector4(F(), F(), F(), F());
            points[i] = new Point(random.Next(-50, 50), random.Next(-50, 50));
            rects[i] = new Rectangle(random.Next(10), random.Next(10), random.Next(10), random.Next(10));
            quats[i] = new Quaternion(F(), F(), F(), F());
            colors[i] = new Color(random.Next(256), random.Next(256), random.Next(256));
            planes[i] = new Plane(v3[i], F());
            boxes[i] = new BoundingBox(new Vector3(F(), F(), F()) - new Vector3(30), new Vector3(F(), F(), F()) + new Vector3(30));
            spheres[i] = new BoundingSphere(v3[i], Math.Abs(F()) * 0.3f + 1);
            rays[i] = new Ray(v3[i], new Vector3(F(), F(), F()));
        }

        for (int i = 0; i < 16; i++) mats[i] = Matrix.CreateRotationZ(i * 0.3f) * Matrix.CreateTranslation(i, -i, 2);

        for (int i = 0; i < 1024; i++)
        {
            var p = new Point(random.Next(-2000, 2000), random.Next(-2000, 2000));
            byPoint[p] = i;
            byLong[((long)p.X << 32) | (uint)p.Y] = i;
            point16s.Add(new Point16(p.X, p.Y));
            byVector[new Vector2(p.X, p.Y)] = i;
            byColor[new Color(i & 255, i >> 2, 7)] = i;
            lookups[i] = (i & 1) == 0 ? p : new Point(p.X + 5000, p.Y);
        }

        frustum = new BoundingFrustum(Matrix.CreateOrthographicOffCenter(0, 100, 0, 100, 0, 100));
        curve = new Curve();
        for (int i = 0; i < 8; i++) curve.Keys.Add(new CurveKey(i, F()));
        curve.ComputeTangents(CurveTangent.Smooth);

        fxc = ModContent.GetInstance<CsFnaPerfMod>().GetFileBytes("Effects/FnaGaps.fxc");
        effect = new Effect(Main.instance.GraphicsDevice, fxc);
        parameter = effect.Parameters["uTime"];

        small = Texture(64);
        large = Texture(256);
        var sized = new MemoryStream();
        large.SaveAsPng(sized, 256, 256);
        CsFnaPerfMod.Log.Info($"csfnaperf png256 size {sized.Length} bytes");

        All = new (string, Func<int, float>)[]
        {
            ("bridge vector2_zero_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Zero.X + i; return s; }),
            ("bridge vector2_zero_game_raw", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Zero_.X + i; return s; }),
            ("bridge vector2_zero_ctor", n => { float s = 0; for (int i = 0; i < n; i++) s += new Vector2(0f, 0f).X + i; return s; }),
            ("bridge vector2_one_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.One.X + i; return s; }),
            ("bridge matrix_identity_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Matrix.Identity.M11 + i; return s; }),
            ("bridge matrix_identity_game_raw", n => { float s = 0; for (int i = 0; i < n; i++) s += Matrix.Identity_.M11 + i; return s; }),
            ("bridge color_white_static", n => { float s = 0; for (int i = 0; i < n; i++) s += Color.White.R + i; return s; }),
            ("bridge vector2_distance_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Distance(v2[i & 63], v2[(i + 1) & 63]); return s; }),
            ("bridge vector2_distance_csharp", n => { float s = 0; for (int i = 0; i < n; i++) { Vector2 d = v2[i & 63] - v2[(i + 1) & 63]; s += MathF.Sqrt(d.X * d.X + d.Y * d.Y); } return s; }),
            ("bridge vector2_length_game", n => { float s = 0; for (int i = 0; i < n; i++) s += v2[i & 63].Length(); return s; }),
            ("bridge vector2_length_csharp", n => { float s = 0; for (int i = 0; i < n; i++) s += v2[i & 63].LengthValue; return s; }),
            ("bridge matrix_rotationz_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Matrix.CreateRotationZ(i * 0.001f).M12; return s; }),
            ("bridge matrix_rotationz_csharp", n => { float s = 0; for (int i = 0; i < n; i++) s += RotationZ(i * 0.001f).M12; return s; }),

            ("math vector2_distance", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Distance(v2[i & 63], v2[(i + 1) & 63]); return s; }),
            ("math vector2_distance_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Distance_(v2[i & 63], v2[(i + 1) & 63]); return s; }),
            ("math vector2_length", n => { float s = 0; for (int i = 0; i < n; i++) s += v2[i & 63].Length(); return s; }),
            ("math vector2_length_game", n => { float s = 0; for (int i = 0; i < n; i++) s += v2[i & 63].Length_(); return s; }),
            ("math vector2_normalize", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Normalize(v2[i & 63]).X; return s; }),
            ("math vector2_normalize_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Normalize_(v2[i & 63]).X; return s; }),
            ("math vector2_lerp", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Lerp(v2[i & 63], v2[(i + 1) & 63], 0.3f).X; return s; }),
            ("math vector2_lerp_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Lerp_(v2[i & 63], v2[(i + 1) & 63], 0.3f).X; return s; }),
            ("math vector2_transform", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Transform(v2[i & 63], mats[i & 15]).X; return s; }),
            ("math vector2_transform_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Vector2.Transform_(v2[i & 63], mats[i & 15]).X; return s; }),
            ("math mathhelper_clamp", n => { float s = 0; for (int i = 0; i < n; i++) s += MathHelper.Clamp(v2[i & 63].X, -10f, 10f); return s; }),
            ("math mathhelper_clamp_game", n => { float s = 0; for (int i = 0; i < n; i++) s += MathHelper.Clamp_(v2[i & 63].X, -10f, 10f); return s; }),
            ("math mathhelper_lerp", n => { float s = 0; for (int i = 0; i < n; i++) s += MathHelper.Lerp(v2[i & 63].X, v2[i & 63].Y, 0.3f); return s; }),
            ("math mathhelper_lerp_game", n => { float s = 0; for (int i = 0; i < n; i++) s += MathHelper.Lerp_(v2[i & 63].X, v2[i & 63].Y, 0.3f); return s; }),
            ("math rectangle_intersects", n => { float s = 0; for (int i = 0; i < n; i++) s += rects[i & 63].Intersects(rects[(i + 5) & 63]) ? 1 : 0; return s; }),
            ("math rectangle_intersects_game", n => { float s = 0; for (int i = 0; i < n; i++) s += rects[i & 63].Intersects_(rects[(i + 5) & 63]) ? 1 : 0; return s; }),
            ("math rectangle_center_bottom", n => { float s = 0; for (int i = 0; i < n; i++) s += rects[i & 63].Center.X + rects[i & 63].Bottom; return s; }),
            ("math rectangle_center_bottom_game", n => { float s = 0; for (int i = 0; i < n; i++) s += rects[i & 63].Center_.X + rects[i & 63].Bottom_; return s; }),
            ("math matrix_rotationz", n => { float s = 0; for (int i = 0; i < n; i++) s += Matrix.CreateRotationZ(i * 0.001f).M12; return s; }),
            ("math matrix_rotationz_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Matrix.CreateRotationZ_(i * 0.001f).M12; return s; }),
            ("math color_lerp", n => { float s = 0; for (int i = 0; i < n; i++) s += Color.Lerp(colors[i & 63], colors[(i + 1) & 63], 0.3f).R; return s; }),
            ("math color_lerp_game", n => { float s = 0; for (int i = 0; i < n; i++) s += Color.Lerp_(colors[i & 63], colors[(i + 1) & 63], 0.3f).R; return s; }),
            ("ops vector2_add", n => { float s = 0; for (int i = 0; i < n; i++) s += (v2[i & 63] + v2[(i + 7) & 63]).X; return s; }),
            ("ops expvec2_add_mod", n => { float s = 0; for (int i = 0; i < n; i++) s += (e2[i & 63] + e2[(i + 7) & 63]).X; return s; }),
            ("ops seqvec2_add_mod", n => { float s = 0; for (int i = 0; i < n; i++) s += (s2[i & 63] + s2[(i + 7) & 63]).X; return s; }),
            ("ops vector2_chain", n => { float s = 0; for (int i = 0; i < n; i++) s += ((v2[i & 63] + v2[(i + 7) & 63]) * 0.5f - v2[(i + 3) & 63]).Y; return s; }),
            ("ops seqvec2_chain_mod", n => { float s = 0; for (int i = 0; i < n; i++) s += ((s2[i & 63] + s2[(i + 7) & 63]) * 0.5f + s2[(i + 3) & 63]).Y; return s; }),
            ("ops vector2_equals", n => { float s = 0; for (int i = 0; i < n; i++) s += v2[i & 63] == v2[(i * 3) & 63] ? 1 : 0; return s; }),
            ("ops vector3_add", n => { float s = 0; for (int i = 0; i < n; i++) s += (v3[i & 63] + v3[(i + 7) & 63]).Z; return s; }),
            ("ops vector4_mul", n => { float s = 0; for (int i = 0; i < n; i++) s += (v4[i & 63] * v4[(i + 7) & 63]).W; return s; }),
            ("ops point_add", n => { float s = 0; for (int i = 0; i < n; i++) s += (points[i & 63] + points[(i + 7) & 63]).X; return s; }),
            ("ops quaternion_mul", n => { float s = 0; for (int i = 0; i < n; i++) s += (quats[i & 63] * quats[(i + 7) & 63]).W; return s; }),
            ("ops matrix_mul_value", n => { float s = 0; for (int i = 0; i < n; i++) s += (mats[i & 15] * mats[(i + 3) & 15]).M41; return s; }),
            ("ops matrix_mul_in", n => { float s = 0; for (int i = 0; i < n; i++) s += MulIn(in mats[i & 15], in mats[(i + 3) & 15]).M41; return s; }),
            ("ops matrix_sub", n => { float s = 0; for (int i = 0; i < n; i++) s += (mats[i & 15] - mats[(i + 3) & 15]).M41; return s; }),
            ("ops color_ctor_float", n => { float s = 0; for (int i = 0; i < n; i++) s += new Color(i * 0.001f, 0.5f, 0.25f).G; return s; }),
            ("ops color_mul_float", n => { float s = 0; for (int i = 0; i < n; i++) s += (colors[i & 63] * 0.7f).R; return s; }),

            ("equality point_equals", n => { float s = 0; for (int i = 0; i < n; i++) s += points[i & 63] == points[(i * 5) & 63] ? 1 : 0; return s; }),
            ("equality rectangle_equals", n => { float s = 0; for (int i = 0; i < n; i++) s += rects[i & 63] == rects[(i * 5) & 63] ? 1 : 0; return s; }),
            ("equality plane_equals", n => { float s = 0; for (int i = 0; i < n; i++) s += planes[i & 63] == planes[(i * 5) & 63] ? 1 : 0; return s; }),
            ("equality point_hash", n => { float s = 0; for (int i = 0; i < n; i++) s += points[i & 63].GetHashCode() & 1; return s; }),
            ("equality dict_point_lookup", n => { float s = 0; for (int i = 0; i < n; i++) s += byPoint.TryGetValue(lookups[i & 1023], out int v) ? v : 0; return s; }),
            ("equality dict_long_lookup", n => { float s = 0; for (int i = 0; i < n; i++) { Point p = lookups[i & 1023]; s += byLong.TryGetValue(((long)p.X << 32) | (uint)p.Y, out int v) ? v : 0; } return s; }),
            ("equality hashset_point16", n => { float s = 0; for (int i = 0; i < n; i++) { Point p = lookups[i & 1023]; s += point16s.Contains(new Point16(p.X, p.Y)) ? 1 : 0; } return s; }),
            ("equality dict_vector2_lookup", n => { float s = 0; for (int i = 0; i < n; i++) { Point p = lookups[i & 1023]; s += byVector.TryGetValue(new Vector2(p.X, p.Y), out int v) ? v : 0; } return s; }),
            ("equality dict_color_lookup", n => { float s = 0; for (int i = 0; i < n; i++) s += byColor.TryGetValue(new Color(i & 255, (i >> 2) & 255, 7), out int v) ? v : 0; return s; }),

            ("bounds box_intersects_box", n => { float s = 0; for (int i = 0; i < n; i++) s += boxes[i & 63].Intersects(boxes[(i + 7) & 63]) ? 1 : 0; return s; }),
            ("bounds box_contains_point", n => { float s = 0; for (int i = 0; i < n; i++) s += (int)boxes[i & 63].Contains(v3[(i + 7) & 63]); return s; }),
            ("bounds box_intersects_sphere", n => { float s = 0; for (int i = 0; i < n; i++) s += boxes[i & 63].Intersects(spheres[(i + 7) & 63]) ? 1 : 0; return s; }),
            ("bounds sphere_contains_box", n => { float s = 0; for (int i = 0; i < n; i++) s += (int)spheres[i & 63].Contains(boxes[(i + 7) & 63]); return s; }),
            ("bounds sphere_intersects_sphere", n => { float s = 0; for (int i = 0; i < n; i++) s += spheres[i & 63].Intersects(spheres[(i + 7) & 63]) ? 1 : 0; return s; }),
            ("bounds ray_intersects_box", n => { float s = 0; for (int i = 0; i < n; i++) s += rays[i & 63].Intersects(boxes[(i + 7) & 63]) ?? 0; return s; }),
            ("bounds frustum_contains_point", n => { float s = 0; for (int i = 0; i < n; i++) s += (int)frustum.Contains(v3[i & 63]); return s; }),
            ("bounds frustum_contains_box", n => { float s = 0; for (int i = 0; i < n; i++) s += (int)frustum.Contains(boxes[i & 63]); return s; }),
            ("bounds frustum_new", n => { float s = 0; for (int i = 0; i < n; i++) s += new BoundingFrustum(mats[i & 15]).Near.D; return s; }),
            ("bounds curve_evaluate", n => { float s = 0; for (int i = 0; i < n; i++) s += curve.Evaluate((i & 1023) * 0.007f); return s; }),

            ("a4 effect_parameter_class", n => { float s = 0; for (int i = 0; i < n; i++) s += (int)parameter.ParameterClass; return s; }),
            ("a4 effect_parameter_semantic", n => { float s = 0; for (int i = 0; i < n; i++) s += parameter.Semantic?.Length ?? 0; return s; }),
            ("a4 effect_parameters_getter", n => { float s = 0; for (int i = 0; i < n; i++) s += effect.Parameters.IsNull ? 0 : 1; return s; }),
            ("a4 effect_parameter_lookup_held", n => { float s = 0; var all = effect.Parameters; for (int i = 0; i < n; i++) s += all["uColor"].IsNull ? 0 : 1; return s; }),
            ("a4 effect_parameter_lookup", n => { float s = 0; for (int i = 0; i < n; i++) s += effect.Parameters["uColor"].IsNull ? 0 : 1; return s; }),
            ("a4 effect_clone", n => { float s = 0; for (int i = 0; i < n; i++) s += effect.Clone().IsNull ? 0 : 1; return s; }),
            ("a4 effect_from_bytes", n => { float s = 0; for (int i = 0; i < n; i++) s += new Effect(Main.instance.GraphicsDevice, fxc).IsNull ? 0 : 1; return s; }),
            ("a4 color_converter_parse", n => { float s = 0; var c = System.ComponentModel.TypeDescriptor.GetConverter(typeof(Color)); for (int i = 0; i < n; i++) s += ((Color)c.ConvertFromInvariantString("73, 94, 171, 255")!).B; return s; }),
            ("a4 save_png_64", n => { float s = 0; for (int i = 0; i < n; i++) { var m = new MemoryStream(); small.SaveAsPng(m, 64, 64); s += m.Length; } return s; }),
            ("a4 save_png_256", n => { float s = 0; for (int i = 0; i < n; i++) { var m = new MemoryStream(); large.SaveAsPng(m, 256, 256); s += m.Length; } return s; }),
            ("a4 measure_string", n => { float s = 0; var f = FontAssets.MouseText.Value; for (int i = 0; i < n; i++) s += f.MeasureString("Bunny Loader 123").X; return s; }),
            ("a4 measure_string_builder", n => { float s = 0; var f = FontAssets.MouseText.Value; for (int i = 0; i < n; i++) s += f.MeasureString(builder).X; return s; }),
        };

        Ready = true;
    }

    static Texture2D Texture(int size)
    {
        var texture = new Texture2D(Main.instance.GraphicsDevice, size, size);
        var data = new Color[size * size];
        for (int i = 0; i < data.Length; i++) data[i] = new Color((i * 7) & 255, (i * 13) & 255, (i / size) & 255);
        texture.SetData(data);
        return texture;
    }

    static Matrix RotationZ(float radians)
    {
        float c = MathF.Cos(radians), s = MathF.Sin(radians);
        return new Matrix(c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
    }

    static Matrix MulIn(in Matrix a, in Matrix b) => new(
        a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
        a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
        a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
        a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,
        a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
        a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
        a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
        a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,
        a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
        a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
        a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
        a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,
        a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
        a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
        a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
        a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44);
}

public sealed class Draws : ModSystem
{
    const int Rounds = 3;

    internal static int Next;

    static readonly Dictionary<string, (double Ns, double Bytes, int N)> best = new();

    static readonly StringBuilder builder = new("Bunny Loader 123");

    static readonly (string Name, bool Ended, Func<SpriteBatch, int, float> Body, int N)[] all =
    {
        ("draw drawstring_short", false, (b, n) => { var f = FontAssets.MouseText.Value; for (int i = 0; i < n; i++) b.DrawString(f, "Bunny Loader 123", new Vector2(40, 40 + (i & 7)), Color.White); return n; }, 200),
        ("draw drawstring_game4", false, (b, n) => { var f = FontAssets.MouseText.Value; for (int i = 0; i < n; i++) b.DrawString_(f, "Bunny Loader 123", new Vector2(40, 40 + (i & 7)), Color.White); return n; }, 200),
        ("draw drawstring_nine", false, (b, n) => { var f = FontAssets.MouseText.Value; for (int i = 0; i < n; i++) b.DrawString(f, "Bunny Loader 123", new Vector2(40, 40 + (i & 7)), Color.White, 0f, default, 1f, SpriteEffects.None, 0f); return n; }, 200),
        ("draw drawstring_builder", false, (b, n) => { var f = FontAssets.MouseText.Value; for (int i = 0; i < n; i++) b.DrawString(f, builder, new Vector2(40, 40 + (i & 7)), Color.White); return n; }, 200),
        ("draw sprite_draw", false, (b, n) => { var t = TextureAssets.MagicPixel.Value; for (int i = 0; i < n; i++) b.Draw(t, new Rectangle(40, 40 + (i & 7), 4, 4), Color.White); return n; }, 400),
        ("draw begin_end_wrapper", true, (b, n) => { for (int i = 0; i < n; i++) { b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend); b.End(); } return n; }, 100),
        ("draw begin_raw_end_raw", true, (b, n) => { for (int i = 0; i < n; i++) { b.Begin_(SpriteSortMode.Deferred, BlendState.AlphaBlend); b.End_(); } return n; }, 100),
        ("draw begin_wrapper_end_raw", true, (b, n) => { for (int i = 0; i < n; i++) { b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend); b.End_(); } return n; }, 100),
        ("draw begin_end_raw", true, (b, n) => { for (int i = 0; i < n; i++) { b.Begin_(SpriteSortMode.Deferred, BlendState.AlphaBlend); b.End(); } return n; }, 100),
        ("draw begin_end_identity", true, (b, n) => { for (int i = 0; i < n; i++) { b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity); b.End(); } return n; }, 100),
        ("draw begin_end_view", true, (b, n) => { for (int i = 0; i < n; i++) { b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix); b.End(); } return n; }, 100),
        ("draw begin_end_custom", true, (b, n) => { var m = Matrix.CreateScale(1.01f); for (int i = 0; i < n; i++) { b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, m); b.End(); } return n; }, 100),
        ("draw sprites_plain_batch", true, (b, n) => { var t = TextureAssets.MagicPixel.Value; b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend); for (int i = 0; i < n; i++) b.Draw(t, new Rectangle(40, 40 + (i & 7), 4, 4), Color.White); b.End(); return n; }, 400),
        ("draw sprites_matrix_batch", true, (b, n) => { var t = TextureAssets.MagicPixel.Value; var m = Matrix.CreateScale(1.01f); b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, m); for (int i = 0; i < n; i++) b.Draw(t, new Rectangle(40, 40 + (i & 7), 4, 4), Color.White); b.End(); return n; }, 400),
    };

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        int total = all.Length * Rounds;
        if (Main.gameMenu || !CsFnaPerfMod.UpdateDone || Next > total) return;

        if (Next == total)
        {
            Next++;
            foreach (var (name, _, _, _) in all)
                if (best.TryGetValue(name, out var r)) CsFnaPerfMod.Report(name, r);
            CsFnaPerfMod.Log.Info($"csfnaperf END cases={Cases.All.Length + all.Length} sink={CsFnaPerfMod.Sink:0}");
            return;
        }

        var (caseName, ended, body, n) = all[Next++ % all.Length];
        if (ended) spriteBatch.End();

        try
        {
            var result = Bench.Fixed(m => body(spriteBatch, m), n);
            if (!best.TryGetValue(caseName, out var old) || result.Ns < old.Ns) best[caseName] = result;
        }
        finally
        {
            if (ended) spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }
    }
}
