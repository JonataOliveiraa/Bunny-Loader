using System;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CsXnaMath;

public sealed class CsXnaMathMod : Mod
{
    const int Samples = 300;

    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsXnaMathMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = pass = fail = 0;
            done = false;
            return;
        }

        if (done || i != Main.myPlayer || ++frames != 90) return;

        done = true;
        Run();
        Log.Info($"csxnamath END pass={pass} fail={fail}");
    }

    static Random rng = new(1);

    static float F(float range = 100f) => (float)(rng.NextDouble() * 2 - 1) * range;

    static float Amount() => (float)(rng.NextDouble() * 2 - 0.5);

    static Vector2 V2(int i) => i == 0 ? default : new(F(), F());

    static Vector3 V3(int i) => i == 0 ? default : new(F(), F(), F());

    static Vector4 V4(int i) => i == 0 ? default : new(F(), F(), F(), F());

    static Quaternion Q(int i) => i == 0 ? default : i == 1 ? Quaternion.Identity : new(F(2), F(2), F(2), F(2));

    static Matrix M(int i)
    {
        if (i == 0) return Matrix.Identity;
        if (i % 3 == 0) return Matrix.CreateFromYawPitchRoll_(F(4), F(4), F(4)) * Matrix.CreateTranslation_(F(), F(), F());
        return new Matrix(F(3), F(3), F(3), F(3), F(3), F(3), F(3), F(3), F(3), F(3), F(3), F(3), F(), F(), F(), F(3));
    }

    static Plane P(int i) => new(new Vector3(F(2), F(2), F(2)), F());

    static Rectangle R() => new(rng.Next(-5, 6), rng.Next(-5, 6), rng.Next(0, 7), rng.Next(0, 7));

    static Point Pt() => new(rng.Next(-6, 13), rng.Next(-6, 13));

    static Color C() => new(rng.Next(256), rng.Next(256), rng.Next(256), rng.Next(256));

    static bool Near(float a, float b)
    {
        if (float.IsNaN(a) || float.IsNaN(b)) return float.IsNaN(a) && float.IsNaN(b);
        if (float.IsInfinity(a) || float.IsInfinity(b)) return a == b || Math.Abs(b) > 1e30f && Math.Sign(a) == Math.Sign(b);
        return Math.Abs(a - b) <= 2e-5f * Math.Max(1f, Math.Max(Math.Abs(a), Math.Abs(b)));
    }

    static bool Near(Vector2 a, Vector2 b) => Near(a.X, b.X) && Near(a.Y, b.Y);

    static bool Near(Vector3 a, Vector3 b) => Near(a.X, b.X) && Near(a.Y, b.Y) && Near(a.Z, b.Z);

    static bool Near(Vector4 a, Vector4 b) => Near(a.X, b.X) && Near(a.Y, b.Y) && Near(a.Z, b.Z) && Near(a.W, b.W);

    static bool Near(Quaternion a, Quaternion b) => Near(a.X, b.X) && Near(a.Y, b.Y) && Near(a.Z, b.Z) && Near(a.W, b.W);

    static bool Near(Plane a, Plane b) => Near(a.Normal, b.Normal) && Near(a.D, b.D);

    static bool Near(Matrix a, Matrix b) =>
        Near(a.M11, b.M11) && Near(a.M12, b.M12) && Near(a.M13, b.M13) && Near(a.M14, b.M14) &&
        Near(a.M21, b.M21) && Near(a.M22, b.M22) && Near(a.M23, b.M23) && Near(a.M24, b.M24) &&
        Near(a.M31, b.M31) && Near(a.M32, b.M32) && Near(a.M33, b.M33) && Near(a.M34, b.M34) &&
        Near(a.M41, b.M41) && Near(a.M42, b.M42) && Near(a.M43, b.M43) && Near(a.M44, b.M44);

    static void Same<T>(string name, Func<int, (T Ours, T Game, string Input)> run, Func<T, T, bool> equal)
    {
        rng = new Random(name.GetHashCode() & 0x7fffffff);
        string? problem = null;

        for (int i = 0; i < Samples && problem == null; i++)
        {
            try
            {
                var (ours, game, input) = run(i);
                if (!equal(ours, game)) problem = $"{input} ours {ours} game {game}";
            }
            catch (Exception e)
            {
                problem = $"sample {i}: {e.GetType().Name}: {e.Message}";
            }
        }

        if (problem == null) pass++;
        else fail++;
        Log.Info($"csxnamath {name}: {(problem == null ? "ok" : "FAIL " + problem)}");
    }

    static void Run()
    {
        Vectors2();
        Vectors3();
        Vectors4();
        Matrices();
        Quaternions();
        Planes();
        Helpers();
        Rectangles();
        Colors();
    }

    static void Vectors2()
    {
        Same("Vector2.Add", i => { var a = V2(i); var b = V2(i); return (Vector2.Add(a, b), Vector2.Add_(a, b), $"{a} {b}"); }, Near);
        Same("Vector2.Subtract", i => { var a = V2(i); var b = V2(i); return (Vector2.Subtract(a, b), Vector2.Subtract_(a, b), $"{a} {b}"); }, Near);
        Same("Vector2.Multiply(v,v)", i => { var a = V2(i); var b = V2(i); return (Vector2.Multiply(a, b), Vector2.Multiply_(a, b), $"{a} {b}"); }, Near);
        Same("Vector2.Multiply(v,f)", i => { var a = V2(i); float f = F(); return (Vector2.Multiply(a, f), Vector2.Multiply_(a, f), $"{a} {f}"); }, Near);
        Same("Vector2.Divide(v,v)", i => { var a = V2(i); var b = V2(1); return (Vector2.Divide(a, b), Vector2.Divide_(a, b), $"{a} {b}"); }, Near);
        Same("Vector2.Divide(v,f)", i => { var a = V2(i); float f = F() + 200f; return (Vector2.Divide(a, f), Vector2.Divide_(a, f), $"{a} {f}"); }, Near);
        Same("Vector2.Negate", i => { var a = V2(i); return (Vector2.Negate(a), Vector2.Negate_(a), $"{a}"); }, Near);
        Same("Vector2.Dot", i => { var a = V2(i); var b = V2(i); return (Vector2.Dot(a, b), Vector2.Dot_(a, b), $"{a} {b}"); }, Near);
        Same("Vector2.Distance", i => { var a = V2(i); var b = V2(i); return (Vector2.Distance(a, b), Vector2.Distance_(a, b), $"{a} {b}"); }, Near);
        Same("Vector2.DistanceSquared", i => { var a = V2(i); var b = V2(i); return (Vector2.DistanceSquared(a, b), Vector2.DistanceSquared_(a, b), $"{a} {b}"); }, Near);
        Same("Vector2.Length", i => { var a = V2(i); return (a.Length(), a.Length_(), $"{a}"); }, Near);
        Same("Vector2.LengthSquared", i => { var a = V2(i); return (a.LengthSquared(), a.LengthSquared_(), $"{a}"); }, Near);
        Same("Vector2.Normalize()", i => { var a = V2(i); var b = a; var c = a; b.Normalize(); c.Normalize_(); return (b, c, $"{a}"); }, Near);
        Same("Vector2.Normalize(v)", i => { var a = V2(i); return (Vector2.Normalize(a), Vector2.Normalize_(a), $"{a}"); }, Near);
        Same("Vector2.Lerp", i => { var a = V2(i); var b = V2(i); float t = Amount(); return (Vector2.Lerp(a, b, t), Vector2.Lerp_(a, b, t), $"{a} {b} {t}"); }, Near);
        Same("Vector2.SmoothStep", i => { var a = V2(i); var b = V2(i); float t = Amount(); return (Vector2.SmoothStep(a, b, t), Vector2.SmoothStep_(a, b, t), $"{a} {b} {t}"); }, Near);
        Same("Vector2.Barycentric", i => { var a = V2(i); var b = V2(i); var c = V2(i); float s = Amount(), t = Amount(); return (Vector2.Barycentric(a, b, c, s, t), Vector2.Barycentric_(a, b, c, s, t), $"{a} {b} {c} {s} {t}"); }, Near);
        Same("Vector2.Clamp", i => { var a = V2(i); var lo = new Vector2(-30, -20); var hi = new Vector2(40, 10); return (Vector2.Clamp(a, lo, hi), Vector2.Clamp_(a, lo, hi), $"{a}"); }, Near);
        Same("Vector2.Max", i => { var a = V2(i); var b = V2(i); return (Vector2.Max(a, b), Vector2.Max_(a, b), $"{a} {b}"); }, Near);
        Same("Vector2.Min", i => { var a = V2(i); var b = V2(i); return (Vector2.Min(a, b), Vector2.Min_(a, b), $"{a} {b}"); }, Near);
        Same("Vector2.Reflect", i => { var a = V2(i); var n = Vector2.Normalize_(V2(1)); return (Vector2.Reflect(a, n), Vector2.Reflect_(a, n), $"{a} {n}"); }, Near);
        Same("Vector2.Transform(m)", i => { var a = V2(i); var m = M(i); return (Vector2.Transform(a, m), Vector2.Transform_(a, m), $"{a} {m}"); }, Near);
        Same("Vector2.Transform(q) [game: not implemented]", i => { var a = V2(i); var q = Quaternion.Normalize_(Q(2)); return (Vector2.Transform(a, q), Vector2.Transform_(a, Matrix.CreateFromQuaternion_(q)), $"{a} {q}"); }, Near);
        Same("Vector2.TransformNormal", i => { var a = V2(i); var m = M(i); return (Vector2.TransformNormal(a, m), Vector2.TransformNormal_(a, m), $"{a} {m}"); }, Near);
    }

    static void Vectors3()
    {
        Same("Vector3.Add", i => { var a = V3(i); var b = V3(i); return (Vector3.Add(a, b), Vector3.Add_(a, b), $"{a} {b}"); }, Near);
        Same("Vector3.Subtract", i => { var a = V3(i); var b = V3(i); return (Vector3.Subtract(a, b), Vector3.Subtract_(a, b), $"{a} {b}"); }, Near);
        Same("Vector3.Multiply(v,v)", i => { var a = V3(i); var b = V3(i); return (Vector3.Multiply(a, b), Vector3.Multiply_(a, b), $"{a} {b}"); }, Near);
        Same("Vector3.Multiply(v,f)", i => { var a = V3(i); float f = F(); return (Vector3.Multiply(a, f), Vector3.Multiply_(a, f), $"{a} {f}"); }, Near);
        Same("Vector3.Divide(v,v)", i => { var a = V3(i); var b = V3(1); return (Vector3.Divide(a, b), Vector3.Divide_(a, b), $"{a} {b}"); }, Near);
        Same("Vector3.Divide(v,f)", i => { var a = V3(i); float f = F() + 200f; return (Vector3.Divide(a, f), Vector3.Divide_(a, f), $"{a} {f}"); }, Near);
        Same("Vector3.Negate", i => { var a = V3(i); return (Vector3.Negate(a), Vector3.Negate_(a), $"{a}"); }, Near);
        Same("Vector3.Dot", i => { var a = V3(i); var b = V3(i); return (Vector3.Dot(a, b), Vector3.Dot_(a, b), $"{a} {b}"); }, Near);
        Same("Vector3.Cross", i => { var a = V3(i); var b = V3(i); return (Vector3.Cross(a, b), Vector3.Cross_(a, b), $"{a} {b}"); }, Near);
        Same("Vector3.Distance", i => { var a = V3(i); var b = V3(i); return (Vector3.Distance(a, b), Vector3.Distance_(a, b), $"{a} {b}"); }, Near);
        Same("Vector3.DistanceSquared", i => { var a = V3(i); var b = V3(i); return (Vector3.DistanceSquared(a, b), Vector3.DistanceSquared_(a, b), $"{a} {b}"); }, Near);
        Same("Vector3.Length", i => { var a = V3(i); return (a.Length(), a.Length_(), $"{a}"); }, Near);
        Same("Vector3.LengthSquared", i => { var a = V3(i); return (a.LengthSquared(), a.LengthSquared_(), $"{a}"); }, Near);
        Same("Vector3.Normalize()", i => { var a = V3(i); var b = a; var c = a; b.Normalize(); c.Normalize_(); return (b, c, $"{a}"); }, Near);
        Same("Vector3.Normalize(v)", i => { var a = V3(i); return (Vector3.Normalize(a), Vector3.Normalize_(a), $"{a}"); }, Near);
        Same("Vector3.Lerp", i => { var a = V3(i); var b = V3(i); float t = Amount(); return (Vector3.Lerp(a, b, t), Vector3.Lerp_(a, b, t), $"{a} {b} {t}"); }, Near);
        Same("Vector3.SmoothStep", i => { var a = V3(i); var b = V3(i); float t = Amount(); return (Vector3.SmoothStep(a, b, t), Vector3.SmoothStep_(a, b, t), $"{a} {b} {t}"); }, Near);
        Same("Vector3.Max", i => { var a = V3(i); var b = V3(i); return (Vector3.Max(a, b), Vector3.Max_(a, b), $"{a} {b}"); }, Near);
        Same("Vector3.Min", i => { var a = V3(i); var b = V3(i); return (Vector3.Min(a, b), Vector3.Min_(a, b), $"{a} {b}"); }, Near);
        Same("Vector3.Reflect", i => { var a = V3(i); var n = Vector3.Normalize_(V3(1)); return (Vector3.Reflect(a, n), Vector3.Reflect_(a, n), $"{a} {n}"); }, Near);
        Same("Vector3.Transform(m)", i => { var a = V3(i); var m = M(i); return (Vector3.Transform(a, m), Vector3.Transform_(a, m), $"{a} {m}"); }, Near);
        Same("Vector3.Transform(q) [game: not implemented]", i => { var a = V3(i); var q = Quaternion.Normalize_(Q(2)); return (Vector3.Transform(a, q), Vector3.Transform_(a, Matrix.CreateFromQuaternion_(q)), $"{a} {q}"); }, Near);
        Same("Vector3.TransformNormal", i => { var a = V3(i); var m = M(i); return (Vector3.TransformNormal(a, m), Vector3.TransformNormal_(a, m), $"{a} {m}"); }, Near);
    }

    static void Vectors4()
    {
        Same("Vector4.Add", i => { var a = V4(i); var b = V4(i); return (Vector4.Add(a, b), Vector4.Add_(a, b), $"{a} {b}"); }, Near);
        Same("Vector4.Subtract", i => { var a = V4(i); var b = V4(i); return (Vector4.Subtract(a, b), Vector4.Subtract_(a, b), $"{a} {b}"); }, Near);
        Same("Vector4.Multiply(v,v)", i => { var a = V4(i); var b = V4(i); return (Vector4.Multiply(a, b), Vector4.Multiply_(a, b), $"{a} {b}"); }, Near);
        Same("Vector4.Multiply(v,f)", i => { var a = V4(i); float f = F(); return (Vector4.Multiply(a, f), Vector4.Multiply_(a, f), $"{a} {f}"); }, Near);
        Same("Vector4.Divide(v,v)", i => { var a = V4(i); var b = V4(1); return (Vector4.Divide(a, b), Vector4.Divide_(a, b), $"{a} {b}"); }, Near);
        Same("Vector4.Divide(v,f)", i => { var a = V4(i); float f = F() + 200f; return (Vector4.Divide(a, f), Vector4.Divide_(a, f), $"{a} {f}"); }, Near);
        Same("Vector4.Negate", i => { var a = V4(i); return (Vector4.Negate(a), Vector4.Negate_(a), $"{a}"); }, Near);
        Same("Vector4.Dot", i => { var a = V4(i); var b = V4(i); return (Vector4.Dot(a, b), Vector4.Dot_(a, b), $"{a} {b}"); }, Near);
        Same("Vector4.Distance", i => { var a = V4(i); var b = V4(i); return (Vector4.Distance(a, b), Vector4.Distance_(a, b), $"{a} {b}"); }, Near);
        Same("Vector4.DistanceSquared", i => { var a = V4(i); var b = V4(i); return (Vector4.DistanceSquared(a, b), Vector4.DistanceSquared_(a, b), $"{a} {b}"); }, Near);
        Same("Vector4.Length", i => { var a = V4(i); return (a.Length(), a.Length_(), $"{a}"); }, Near);
        Same("Vector4.LengthSquared", i => { var a = V4(i); return (a.LengthSquared(), a.LengthSquared_(), $"{a}"); }, Near);
        Same("Vector4.Normalize()", i => { var a = V4(i); var b = a; var c = a; b.Normalize(); c.Normalize_(); return (b, c, $"{a}"); }, Near);
        Same("Vector4.Normalize(v)", i => { var a = V4(i); return (Vector4.Normalize(a), Vector4.Normalize_(a), $"{a}"); }, Near);
        Same("Vector4.Max", i => { var a = V4(i); var b = V4(i); return (Vector4.Max(a, b), Vector4.Max_(a, b), $"{a} {b}"); }, Near);
        Same("Vector4.Min", i => { var a = V4(i); var b = V4(i); return (Vector4.Min(a, b), Vector4.Min_(a, b), $"{a} {b}"); }, Near);
        Same("Vector4.Transform(v2,m)", i => { var a = V2(i); var m = M(i); return (Vector4.Transform(a, m), Vector4.Transform_(a, m), $"{a} {m}"); }, Near);
        Same("Vector4.Transform(v3,m)", i => { var a = V3(i); var m = M(i); return (Vector4.Transform(a, m), Vector4.Transform_(a, m), $"{a} {m}"); }, Near);
        Same("Vector4.Transform(v4,m)", i => { var a = V4(i); var m = M(i); return (Vector4.Transform(a, m), Vector4.Transform_(a, m), $"{a} {m}"); }, Near);
        Same("Vector4.Transform(v2,q) [game: not implemented]", i => { var a = V2(i); var q = Quaternion.Normalize_(Q(2)); return (Vector4.Transform(a, q), Vector4.Transform_(a, Matrix.CreateFromQuaternion_(q)), $"{a} {q}"); }, Near);
        Same("Vector4.Transform(v3,q) [game: not implemented]", i => { var a = V3(i); var q = Quaternion.Normalize_(Q(2)); return (Vector4.Transform(a, q), Vector4.Transform_(a, Matrix.CreateFromQuaternion_(q)), $"{a} {q}"); }, Near);
        Same("Vector4.Transform(v4,q) [game: not implemented]", i => { var a = V4(i); var q = Quaternion.Normalize_(Q(2)); return (Vector4.Transform(a, q), Vector4.Transform_(a, Matrix.CreateFromQuaternion_(q)), $"{a} {q}"); }, Near);
    }

    static void Matrices()
    {
        Same("Matrix.Add", i => { var a = M(i); var b = M(i + 1); return (Matrix.Add(a, b), Matrix.Add_(a, b), $"{a} {b}"); }, Near);
        Same("Matrix.Subtract", i => { var a = M(i); var b = M(i + 1); return (Matrix.Subtract(a, b), Matrix.Subtract_(a, b), $"{a} {b}"); }, Near);
        Same("Matrix.Multiply(m,m)", i => { var a = M(i); var b = M(i + 1); return (Matrix.Multiply(a, b), Matrix.Multiply_(a, b), $"{a} {b}"); }, Near);
        Same("Matrix.Multiply(m,f)", i => { var a = M(i); float f = F(); return (Matrix.Multiply(a, f), Matrix.Multiply_(a, f), $"{a} {f}"); }, Near);
        Same("Matrix.Divide(m,m) [FNA: per element; game: times the inverse]", i => { var a = M(i); var b = M(1) + Matrix.CreateScale(50f); return (Matrix.Divide(a, b), new Matrix(a.M11 / b.M11, a.M12 / b.M12, a.M13 / b.M13, a.M14 / b.M14, a.M21 / b.M21, a.M22 / b.M22, a.M23 / b.M23, a.M24 / b.M24, a.M31 / b.M31, a.M32 / b.M32, a.M33 / b.M33, a.M34 / b.M34, a.M41 / b.M41, a.M42 / b.M42, a.M43 / b.M43, a.M44 / b.M44), $"{a} {b}"); }, Near);
        Same("Matrix.Divide(m,f)", i => { var a = M(i); float f = F() + 200f; return (Matrix.Divide(a, f), Matrix.Divide_(a, f), $"{a} {f}"); }, Near);
        Same("Matrix.Negate", i => { var a = M(i); return (Matrix.Negate(a), Matrix.Negate_(a), $"{a}"); }, Near);
        Same("Matrix.Transpose", i => { var a = M(i); return (Matrix.Transpose(a), Matrix.Transpose_(a), $"{a}"); }, Near);
        Same("Matrix.Invert", i => { var a = M(i); return (Matrix.Invert(a), Matrix.Invert_(a), $"{a}"); }, (x, y) => Near(x * 1e-3f, y * 1e-3f) || Near(x * 1e-1f, y * 1e-1f));
        Same("Matrix.Invert (times the matrix)", i => { var a = M(i); return (a * Matrix.Invert(a), Matrix.Identity, $"{a}"); }, (x, y) => Near(x * 1e-2f, y * 1e-2f));
        Same("Matrix.Determinant", i => { var a = M(i); return (a.Determinant(), a.Determinant_(), $"{a}"); }, (x, y) => Math.Abs(x - y) <= 1e-4f * Math.Max(1f, Math.Abs(y)));
        Same("Matrix.Lerp [game: not implemented]", i => { var a = M(i); var b = M(i + 1); float t = Amount(); return (Matrix.Lerp(a, b, t), Matrix.Add_(a, Matrix.Multiply_(Matrix.Subtract_(b, a), t)), $"{a} {b} {t}"); }, Near);
        Same("Matrix.Transform", i => { var a = M(i); var q = Q(i); return (Matrix.Transform(a, q), Matrix.Transform_(a, q), $"{a} {q}"); }, Near);
        Same("Matrix.CreateTranslation(f)", i => { float x = F(), y = F(), z = F(); return (Matrix.CreateTranslation(x, y, z), Matrix.CreateTranslation_(x, y, z), $"{x} {y} {z}"); }, Near);
        Same("Matrix.CreateTranslation(v)", i => { var v = V3(i); return (Matrix.CreateTranslation(v), Matrix.CreateTranslation_(v), $"{v}"); }, Near);
        Same("Matrix.CreateScale(f)", i => { float s = F(); return (Matrix.CreateScale(s), Matrix.CreateScale_(s), $"{s}"); }, Near);
        Same("Matrix.CreateScale(fff)", i => { float x = F(), y = F(), z = F(); return (Matrix.CreateScale(x, y, z), Matrix.CreateScale_(x, y, z), $"{x} {y} {z}"); }, Near);
        Same("Matrix.CreateScale(v)", i => { var v = V3(i); return (Matrix.CreateScale(v), Matrix.CreateScale_(v), $"{v}"); }, Near);
        Same("Matrix.CreateRotationX", i => { float r = F(10); return (Matrix.CreateRotationX(r), Matrix.CreateRotationX_(r), $"{r}"); }, Near);
        Same("Matrix.CreateRotationY [FNA: M13 = -sin; game: +sin]", i => { float r = F(10); return (Matrix.CreateRotationY(r), Matrix.CreateFromAxisAngle(Vector3.UnitY, r), $"{r}"); }, Near);
        Same("Matrix.CreateRotationZ", i => { float r = F(10); return (Matrix.CreateRotationZ(r), Matrix.CreateRotationZ_(r), $"{r}"); }, Near);
        Same("Matrix.CreateFromAxisAngle [game: not implemented]", i => { var v = Vector3.Normalize_(V3(1)); float r = F(10); return (Matrix.CreateFromAxisAngle(v, r), Matrix.CreateFromQuaternion_(Quaternion.CreateFromAxisAngle_(v, r)), $"{v} {r}"); }, Near);
        Same("Matrix.CreateFromQuaternion", i => { var q = Q(i); return (Matrix.CreateFromQuaternion(q), Matrix.CreateFromQuaternion_(q), $"{q}"); }, Near);
        Same("Matrix.CreateFromYawPitchRoll", i => { float y = F(10), p = F(10), r = F(10); return (Matrix.CreateFromYawPitchRoll(y, p, r), Matrix.CreateFromYawPitchRoll_(y, p, r), $"{y} {p} {r}"); }, Near);
        Same("Matrix.CreateOrthographic", i => { float w = Math.Abs(F()) + 1, h = Math.Abs(F()) + 1, n = F(), f = n + Math.Abs(F()) + 1; return (Matrix.CreateOrthographic(w, h, n, f), Matrix.CreateOrthographic_(w, h, n, f), $"{w} {h} {n} {f}"); }, Near);
        Same("Matrix.CreateOrthographicOffCenter", i => { float l = F(), r = l + Math.Abs(F()) + 1, b = F(), t = b + Math.Abs(F()) + 1, n = F(), f = n + Math.Abs(F()) + 1; return (Matrix.CreateOrthographicOffCenter(l, r, b, t, n, f), Matrix.CreateOrthographicOffCenter_(l, r, b, t, n, f), $"{l} {r} {b} {t} {n} {f}"); }, Near);
        Same("Matrix.CreatePerspective [game: not implemented]", i => { float w = Math.Abs(F()) + 1, h = Math.Abs(F()) + 1, n = Math.Abs(F(10)) + 0.1f, f = n + Math.Abs(F()) + 1; return (Matrix.CreatePerspective(w, h, n, f), Matrix.CreatePerspectiveFieldOfView_(2f * MathF.Atan(h / (2f * n)), w / h, n, f), $"{w} {h} {n} {f}"); }, (x, y) => Near(x * 1e-2f, y * 1e-2f));
        Same("Matrix.CreatePerspectiveFieldOfView", i => { float fov = 0.1f + (float)rng.NextDouble() * 2.9f, a = Math.Abs(F(3)) + 0.2f, n = Math.Abs(F(10)) + 0.1f, f = n + Math.Abs(F()) + 1; return (Matrix.CreatePerspectiveFieldOfView(fov, a, n, f), Matrix.CreatePerspectiveFieldOfView_(fov, a, n, f), $"{fov} {a} {n} {f}"); }, Near);
        Same("Matrix.CreatePerspectiveOffCenter [game: not implemented]", i => { float w = Math.Abs(F()) + 1, h = Math.Abs(F()) + 1, n = Math.Abs(F(10)) + 0.1f, f = n + Math.Abs(F()) + 1; return (Matrix.CreatePerspectiveOffCenter(-w / 2, w / 2, -h / 2, h / 2, n, f), Matrix.CreatePerspectiveFieldOfView_(2f * MathF.Atan(h / (2f * n)), w / h, n, f), $"{w} {h} {n} {f}"); }, (x, y) => Near(x * 1e-2f, y * 1e-2f));
        Same("Matrix.CreateShadow (normalized plane)", i => { var d = V3(1); var p = Plane.Normalize_(P(i)); return (Matrix.CreateShadow(d, p), Matrix.CreateShadow_(d, p), $"{d} {p}"); }, Near);
        Same("Matrix.CreateReflection", i => { var p = P(i); return (Matrix.CreateReflection(p), Matrix.CreateReflection_(p), $"{p}"); }, Near);

        Same("Matrix.Up/Down/Left/Right", i =>
        {
            var m = M(i);
            var ours = new Matrix(m.Up.X, m.Down.Y, m.Left.Z, m.Right.X, m.Forward.X, m.Backward.Y, m.Translation.Z, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var game = new Matrix(m.Up_.X, m.Down_.Y, m.Left_.Z, m.Right_.X, m.Forward_.X, m.Backward_.Y, m.Translation_.Z, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            return (ours, game, $"{m}");
        }, Near);

        Same("Matrix property setters", i =>
        {
            var m = M(i);
            Vector3 a = V3(1), b = V3(1), c = V3(1), d = V3(1);
            var ours = m;
            var game = m;
            ours.Up = a; ours.Left = b; ours.Forward = c; ours.Translation = d;
            game.Up_ = a; game.Left_ = b; game.Forward_ = c; game.Translation_ = d;
            var down = m;
            var down2 = m;
            down.Down = a; down.Right = b; down.Backward = c;
            down2.Down_ = a; down2.Right_ = b; down2.Backward_ = c;
            return (ours * 1f + down, game * 1f + down2, $"{m} {a} {b} {c} {d}");
        }, Near);
    }

    static void Quaternions()
    {
        Same("Quaternion.Add", i => { var a = Q(i); var b = Q(i + 2); return (Quaternion.Add(a, b), Quaternion.Add_(a, b), $"{a} {b}"); }, Near);
        Same("Quaternion.Subtract", i => { var a = Q(i); var b = Q(i + 2); return (Quaternion.Subtract(a, b), Quaternion.Subtract_(a, b), $"{a} {b}"); }, Near);
        Same("Quaternion.Multiply(q,q)", i => { var a = Q(i); var b = Q(i + 2); return (Quaternion.Multiply(a, b), Quaternion.Multiply_(a, b), $"{a} {b}"); }, Near);
        Same("Quaternion.Multiply(q,f)", i => { var a = Q(i); float f = F(); return (Quaternion.Multiply(a, f), Quaternion.Multiply_(a, f), $"{a} {f}"); }, Near);
        Same("Quaternion.Divide", i => { var a = Q(i); var b = Q(i + 2); return (Quaternion.Divide(a, b), Quaternion.Divide_(a, b), $"{a} {b}"); }, Near);
        Same("Quaternion.Negate", i => { var a = Q(i); return (Quaternion.Negate(a), Quaternion.Negate_(a), $"{a}"); }, Near);
        Same("Quaternion.Dot", i => { var a = Q(i); var b = Q(i + 2); return (Quaternion.Dot(a, b), Quaternion.Dot_(a, b), $"{a} {b}"); }, Near);
        Same("Quaternion.Concatenate [FNA: value2 * value1]", i => { var a = Q(i); var b = Q(i + 2); return (Quaternion.Concatenate(a, b), Quaternion.Multiply_(b, a), $"{a} {b}"); }, Near);
        Same("Quaternion.Conjugate()", i => { var a = Q(i); var b = a; var c = a; b.Conjugate(); c.Conjugate_(); return (b, c, $"{a}"); }, Near);
        Same("Quaternion.Conjugate(q)", i => { var a = Q(i); return (Quaternion.Conjugate(a), Quaternion.Conjugate_(a), $"{a}"); }, Near);
        Same("Quaternion.Inverse", i => { var a = Q(i); return (Quaternion.Inverse(a), Quaternion.Inverse_(a), $"{a}"); }, Near);
        Same("Quaternion.Length", i => { var a = Q(i); return (a.Length(), a.Length_(), $"{a}"); }, Near);
        Same("Quaternion.LengthSquared", i => { var a = Q(i); return (a.LengthSquared(), a.LengthSquared_(), $"{a}"); }, Near);
        Same("Quaternion.Normalize()", i => { var a = Q(i); var b = a; var c = a; b.Normalize(); c.Normalize_(); return (b, c, $"{a}"); }, Near);
        Same("Quaternion.Normalize(q)", i => { var a = Q(i); return (Quaternion.Normalize(a), Quaternion.Normalize_(a), $"{a}"); }, Near);
        Same("Quaternion.CreateFromAxisAngle", i => { var v = Vector3.Normalize_(V3(1)); float r = F(10); return (Quaternion.CreateFromAxisAngle(v, r), Quaternion.CreateFromAxisAngle_(v, r), $"{v} {r}"); }, Near);
        Same("Quaternion.CreateFromYawPitchRoll", i => { float y = F(10), p = F(10), r = F(10); return (Quaternion.CreateFromYawPitchRoll(y, p, r), Quaternion.CreateFromYawPitchRoll_(y, p, r), $"{y} {p} {r}"); }, Near);
        Same("Quaternion.CreateFromRotationMatrix", i => { var m = Matrix.CreateFromYawPitchRoll_(F(4), F(4), F(4)); return (Quaternion.CreateFromRotationMatrix(m), Quaternion.CreateFromRotationMatrix_(m), $"{m}"); }, Near);
    }

    static void Planes()
    {
        Same("Plane.Dot", i => { var p = P(i); var v = V4(i); return (p.Dot(v), p.Dot_(v), $"{p} {v}"); }, Near);
        Same("Plane.DotCoordinate", i => { var p = P(i); var v = V3(i); return (p.DotCoordinate(v), p.DotCoordinate_(v), $"{p} {v}"); }, Near);
        Same("Plane.DotNormal", i => { var p = P(i); var v = V3(i); return (p.DotNormal(v), p.DotNormal_(v), $"{p} {v}"); }, Near);
        Same("Plane.Normalize()", i => { var p = P(i); var a = p; var b = p; a.Normalize(); b.Normalize_(); return (a, b, $"{p}"); }, Near);
        Same("Plane.Normalize(p)", i => { var p = P(i); return (Plane.Normalize(p), Plane.Normalize_(p), $"{p}"); }, Near);
        Same("Plane.Transform(m) [game: not implemented]", i =>
        {
            var p = Plane.Normalize_(P(i));
            var m = Matrix.CreateFromYawPitchRoll_(F(4), F(4), F(4)) * Matrix.CreateTranslation_(F(), F(), F());
            Vector3 onPlane = p.Normal * -p.D + Vector3.Cross_(p.Normal, V3(1)) * 0.01f;
            onPlane -= p.Normal * p.DotCoordinate_(onPlane);
            return (Plane.Transform(p, m).DotCoordinate_(Vector3.Transform_(onPlane, m)), 0f, $"{p} {m}");
        }, (x, y) => Math.Abs(x - y) <= 1e-2f);
        Same("Plane.Transform(q) [game: not implemented]", i =>
        {
            var p = Plane.Normalize_(P(i));
            var r = Quaternion.Normalize_(Q(2));
            Vector3 onPlane = p.Normal * -p.D;
            return (Plane.Transform(p, r).DotCoordinate_(Vector3.Transform_(onPlane, Matrix.CreateFromQuaternion_(r))), 0f, $"{p} {r}");
        }, (x, y) => Math.Abs(x - y) <= 1e-2f);
    }

    static void Helpers()
    {
        Same("MathHelper.Barycentric", i => { float a = F(), b = F(), c = F(), s = Amount(), t = Amount(); return (MathHelper.Barycentric(a, b, c, s, t), MathHelper.Barycentric_(a, b, c, s, t), $"{a} {b} {c} {s} {t}"); }, Near);
        Same("MathHelper.CatmullRom", i => { float a = F(), b = F(), c = F(), d = F(), t = Amount(); return (MathHelper.CatmullRom(a, b, c, d, t), MathHelper.CatmullRom_(a, b, c, d, t), $"{a} {b} {c} {d} {t}"); }, Near);
        Same("MathHelper.Clamp", i => { float v = F(), lo = -30f, hi = 40f; return (MathHelper.Clamp(v, lo, hi), MathHelper.Clamp_(v, lo, hi), $"{v} {lo} {hi}"); }, Near);
        Same("MathHelper.Clamp min > max [FNA: max first]", i => (MathHelper.Clamp(-4.712692f, 40f, -30f), 40f, "-4.71 40 -30"), Near);
        Same("MathHelper.Distance", i => { float a = F(), b = F(); return (MathHelper.Distance(a, b), MathHelper.Distance_(a, b), $"{a} {b}"); }, Near);
        Same("MathHelper.Hermite", i => { float a = F(), b = F(), c = F(), d = F(), t = i == 1 ? 0f : i == 2 ? 1f : Amount(); return (MathHelper.Hermite(a, b, c, d, t), MathHelper.Hermite_(a, b, c, d, t), $"{a} {b} {c} {d} {t}"); }, Near);
        Same("MathHelper.Lerp", i => { float a = F(), b = F(), t = Amount(); return (MathHelper.Lerp(a, b, t), MathHelper.Lerp_(a, b, t), $"{a} {b} {t}"); }, Near);
        Same("MathHelper.Max", i => { float a = F(), b = i == 1 ? a : F(); return (MathHelper.Max(a, b), MathHelper.Max_(a, b), $"{a} {b}"); }, Near);
        Same("MathHelper.Min", i => { float a = F(), b = i == 1 ? a : F(); return (MathHelper.Min(a, b), MathHelper.Min_(a, b), $"{a} {b}"); }, Near);
        Same("MathHelper.SmoothStep", i => { float a = F(), b = F(), t = Amount(); return (MathHelper.SmoothStep(a, b, t), MathHelper.SmoothStep_(a, b, t), $"{a} {b} {t}"); }, Near);
        Same("MathHelper.ToDegrees", i => { float r = F(10); return (MathHelper.ToDegrees(r), MathHelper.ToDegrees_(r), $"{r}"); }, Near);
        Same("MathHelper.ToRadians", i => { float d = F(1000); return (MathHelper.ToRadians(d), MathHelper.ToRadians_(d), $"{d}"); }, Near);
        Same("MathHelper.WrapAngle", i => { float r = F(50); return (MathHelper.WrapAngle(r), MathHelper.WrapAngle_(r), $"{r}"); }, (x, y) => Near(x, y) || Math.Abs(Math.Abs(x) - MathF.PI) < 1e-5f && Math.Abs(Math.Abs(y) - MathF.PI) < 1e-5f);
        Same("MathHelper.WrapAngle(-pi) [FNA: pi]", i => (MathHelper.WrapAngle(-MathHelper.Pi), MathHelper.Pi, "-pi"), Near);
    }

    static void Rectangles()
    {
        Same("Rectangle edges", i => { var r = R(); return ((r.Left, r.Right, r.Top, r.Bottom, r.Center, r.Location, r.IsEmpty), (r.Left_, r.Right_, r.Top_, r.Bottom_, r.Center_, r.Location_, r.IsEmpty_), $"{r}"); }, (a, b) => a == b);
        Same("Rectangle.IsEmpty(empty)", i => { var r = i % 2 == 0 ? default : R(); return (r.IsEmpty, r.IsEmpty_, $"{r}"); }, (a, b) => a == b);
        Same("Rectangle.Location set", i => { var r = R(); var p = Pt(); var a = r; var b = r; a.Location = p; b.Location_ = p; return (a, b, $"{r} {p}"); }, (a, b) => a == b);
        Same("Rectangle.Contains(x,y)", i => { var r = R(); var p = Pt(); return (r.Contains(p.X, p.Y), r.Contains_(p.X, p.Y), $"{r} {p}"); }, (a, b) => a == b);
        Same("Rectangle.Contains(p)", i => { var r = R(); var p = Pt(); return (r.Contains(p), r.Contains_(p), $"{r} {p}"); }, (a, b) => a == b);
        Same("Rectangle.Contains(r)", i => { var r = R(); var s = R(); return (r.Contains(s), r.Contains_(s), $"{r} {s}"); }, (a, b) => a == b);
        Same("Rectangle.Intersects", i => { var r = R(); var s = R(); if (r.Width == 0 || r.Height == 0 || s.Width == 0 || s.Height == 0) return (true, true, ""); return (r.Intersects(s), r.Intersects_(s), $"{r} {s}"); }, (a, b) => a == b);
        Same("Rectangle.Intersects(empty edge) [FNA: false]", i => (new Rectangle(3, -2, 3, 0).Intersects(new Rectangle(5, -2, 6, 3)), false, "zero height"), (a, b) => a == b);
        Same("Rectangle.Intersect", i => { var r = R(); var s = R(); if (r.Width == 0 || r.Height == 0 || s.Width == 0 || s.Height == 0) return (default, default, ""); return (Rectangle.Intersect(r, s), Rectangle.Intersect_(r, s), $"{r} {s}"); }, (a, b) => a == b);
        Same("Rectangle.Union", i => { var r = R(); var s = R(); return (Rectangle.Union(r, s), Rectangle.Union_(r, s), $"{r} {s}"); }, (a, b) => a == b);
        Same("Rectangle.Offset(x,y)", i => { var r = R(); var p = Pt(); var a = r; var b = r; a.Offset(p.X, p.Y); b.Offset_(p.X, p.Y); return (a, b, $"{r} {p}"); }, (a, b) => a == b);
        Same("Rectangle.Offset(p)", i => { var r = R(); var p = Pt(); var a = r; var b = r; a.Offset(p); b.Offset_(p); return (a, b, $"{r} {p}"); }, (a, b) => a == b);
        Same("Rectangle.Inflate", i => { var r = R(); var p = Pt(); var a = r; var b = r; a.Inflate(p.X, p.Y); b.Inflate_(p.X, p.Y); return (a, b, $"{r} {p}"); }, (a, b) => a == b);
    }

    static void Colors()
    {
        Same("Color.PackedValue", i => { var c = C(); return (c.PackedValue, c.PackedValue_, $"{c}"); }, (a, b) => a == b);
        Same("Color.PackedValue set", i => { uint v = (uint)rng.Next() ^ ((uint)rng.Next() << 16); var a = default(Color); var b = default(Color); a.PackedValue = v; b.PackedValue_ = v; return (a, b, $"{v:X8}"); }, (a, b) => a == b);
        Same("Color.Multiply", i => { var c = C(); float f = (float)rng.NextDouble(); return (Color.Multiply(c, f), Color.Multiply_(c, f), $"{c} {f}"); }, (a, b) => a == b);
        Same("Color.Multiply out of range [FNA: clamps]", i => (Color.Multiply(new Color(149, 2, 186, 181), -0.5f), new Color(0, 0, 0, 0), "negative"), (a, b) => a == b);
        Same("Color.Lerp", i => { var c = C(); var d = C(); float t = (float)rng.NextDouble(); return (Color.Lerp(c, d, t), Color.Lerp_(c, d, t), $"{c} {d} {t}"); }, (a, b) => Math.Abs(a.R - b.R) <= 1 && Math.Abs(a.G - b.G) <= 1 && Math.Abs(a.B - b.B) <= 1 && Math.Abs(a.A - b.A) <= 1);
        Same("Color.Lerp [FNA: truncates]", i => (Color.Lerp(new Color(232, 172, 110, 10), new Color(162, 116, 26, 100), 0.41147053f), new Color(203, 148, 75, 47), "example"), (a, b) => a == b);
    }
}
