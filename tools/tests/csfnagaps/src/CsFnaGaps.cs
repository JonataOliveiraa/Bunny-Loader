using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Text;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CsFnaGaps;

public sealed class CsFnaGapsMod : Mod
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsFnaGapsMod>().Logger;

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
        if (done || i != Main.myPlayer || ++frames != 120) return;

        done = true;
        Run();
        Log.Info($"csfnagaps END pass={pass} fail={fail}");
    }

    static void Check(string name, Func<string?> test)
    {
        string? problem;
        try { problem = test(); }
        catch (Exception e) { problem = e.GetType().Name + ": " + e.Message; }

        if (problem == null) pass++;
        else fail++;
        Log.Info($"csfnagaps {name}: {(problem == null ? "ok" : "FAIL " + problem)}");
    }

    static string? Expect(bool condition, string what) => condition ? null : what;

    static bool Near(float a, float b) => MathF.Abs(a - b) <= 1e-5f * MathF.Max(1f, MathF.Abs(b));

    static bool Near(Quaternion a, Quaternion b) => Near(a.X, b.X) && Near(a.Y, b.Y) && Near(a.Z, b.Z) && Near(a.W, b.W);

    static void Run()
    {
        Check("point", () =>
        {
            var a = new Point(6, 8);
            var b = new Point(2, 4);
            if (a + b != new Point(8, 12) || a - b != new Point(4, 4)) return "+ or -";
            if (a * b != new Point(12, 32) || a / b != new Point(3, 2)) return "* or /";
            return Expect(a.ToString() == "{X:6 Y:8}", a.ToString());
        });

        Check("vector3", () =>
        {
            if (new Vector3(2f) != Vector3.New(2f)) return "ctor(float)";
            if (new Vector3(new Vector2(1, 2), 3) != Vector3.New(new Vector2(1, 2), 3)) return "ctor(Vector2, float)";

            Vector3 a = new(1, -2, 3), b = new(4, 5, -6);
            Vector3.Negate(ref a, out Vector3 negated);
            Vector3.Multiply(ref a, ref b, out Vector3 product);
            Vector3.Divide(ref a, ref b, out Vector3 quotient);
            if (-a != negated) return "unary -";
            return Expect(a * b == product && a / b == quotient, "* or / by vector");
        });

        Check("vector4", () =>
        {
            if (new Vector4(2f) != Vector4.New(2f)) return "ctor(float)";
            if (new Vector4(new Vector2(1, 2), 3, 4) != Vector4.New(new Vector2(1, 2), 3, 4)) return "ctor(Vector2, float, float)";
            if (new Vector4(new Vector3(1, 2, 3), 4) != Vector4.New(new Vector3(1, 2, 3), 4)) return "ctor(Vector3, float)";

            Vector4 a = new(1, -2, 3, 8), b = new(4, 5, -6, 2);
            Vector4.Add(ref a, ref b, out Vector4 sum);
            Vector4.Subtract(ref a, ref b, out Vector4 difference);
            Vector4.Negate(ref a, out Vector4 negated);
            Vector4.Multiply(ref a, ref b, out Vector4 product);
            Vector4.Multiply(ref a, 3f, out Vector4 scaled);
            Vector4.Divide(ref a, ref b, out Vector4 quotient);
            Vector4.Divide(ref a, 2f, out Vector4 halved);
            if (a + b != sum || a - b != difference || -a != negated) return "+ - or unary -";
            if (a * b != product || a * 3f != scaled || 3f * a != scaled) return "*";
            if (a / b != quotient || a / 2f != halved) return "/";
            return Expect(a.ToString() == "{X:1 Y:-2 Z:3 W:8}", a.ToString());
        });

        Check("quaternion", () =>
        {
            if (new Quaternion(1, 2, 3, 4) != Quaternion.New(1, 2, 3, 4)) return "ctor(x, y, z, w)";
            if (new Quaternion(new Vector3(1, 2, 3), 4) != Quaternion.New(new Vector3(1, 2, 3), 4)) return "ctor(Vector3, float)";

            Quaternion a = new(0.1f, 0.7f, -0.3f, 0.5f), b = new(-0.4f, 0.2f, 0.9f, 0.6f);
            if (!Near(a * b, Quaternion.Multiply(a, b))) return $"a*b {a * b} vs {Quaternion.Multiply(a, b)}";
            if (!Near(a / b, Quaternion.Divide(a, b))) return $"a/b {a / b} vs {Quaternion.Divide(a, b)}";
            if (a + b != Quaternion.Add(a, b) || a - b != Quaternion.Subtract(a, b)) return "+ or -";
            if (-a != Quaternion.Negate(a) || a * 2f != Quaternion.Multiply(a, 2f)) return "unary - or * float";
            return Expect(new Quaternion(1, 2, 3, 4).ToString() == "{X:1 Y:2 Z:3 W:4}", new Quaternion(1, 2, 3, 4).ToString());
        });

        Check("plane", () =>
        {
            if (new Plane(1, 2, 3, 4) != Plane.New(1, 2, 3, 4)) return "ctor(a, b, c, d)";
            if (new Plane(new Vector3(1, 2, 3), 4) != Plane.New(new Vector3(1, 2, 3), 4)) return "ctor(Vector3, float)";
            if (new Plane(new Vector4(1, 2, 3, 4)) != Plane.New(new Vector4(1, 2, 3, 4))) return "ctor(Vector4)";

            var p = new Plane(new Vector3(0, 0, 5), new Vector3(1, 0, 5), new Vector3(0, 1, 5));
            if (p.Normal != new Vector3(0, 0, 1) || p.D != -5f) return $"3 points gave {p}";
            return Expect(p.ToString() == "{Normal:{X:0 Y:0 Z:1} D:-5}", p.ToString());
        });

        Check("color", () =>
        {
            var v3 = new Vector3(0.2f, 0.4f, 0.6f);
            var v4 = new Vector4(0.2f, 0.4f, 0.6f, 1f);
            if (new Color(v3) != Color.New(v3)) return $"ctor(Vector3) {new Color(v3)} vs {Color.New(v3)}";
            if (new Color(v4) != Color.New(v4)) return $"ctor(Vector4) {new Color(v4)} vs {Color.New(v4)}";

            var half = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
            Log.Info($"csfnagaps color_half: ours {new Color(half)} game {Color.New(half)}");

            if (Color.FromNonPremultiplied(255, 128, 0, 128) != new Color(128, 64, 0, 128)) return $"FromNonPremultiplied(int) {Color.FromNonPremultiplied(255, 128, 0, 128)}";
            return Expect(Color.FromNonPremultiplied(new Vector4(1f, 0.4f, 0f, 0.5f)) == new Color(0.5f, 0.2f, 0f, 0.5f), "FromNonPremultiplied(Vector4)");
        });

        Check("keyboard", () =>
        {
            var state = new KeyboardState(Keys.A, Keys.Space, Keys.F12, Keys.OemClear);
            foreach (Keys key in new[] { Keys.A, Keys.Space, Keys.F12, Keys.OemClear })
            {
                if (!state.IsKeyDown(key)) return $"game says {key} is up";
                if (state[key] != KeyState.Down) return $"indexer says {key} is up";
            }
            if (state.IsKeyDown(Keys.B) || state[Keys.B] != KeyState.Up) return "B is down";
            if (new KeyboardState() != default) return "empty state";
            return Expect(new KeyboardState(Keys.A) == new KeyboardState(Keys.A), "equality");
        });

        Check("mouse", () =>
        {
            var state = new MouseState(10, 20, 30, ButtonState.Pressed, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            var game = MouseState.New(10, 20, 30, ButtonState.Pressed, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            if (state != game) return "differs from the game's constructor";
            if (state.X != 10 || state.Y != 20 || state.ScrollWheelValue != 30 || state.LeftButton != ButtonState.Pressed) return "getters";
            return Expect(state.ToString() == "[MouseState X=10, Y=20, Buttons=Left Right, Wheel=30]", state.ToString());
        });

        Check("viewport", () =>
        {
            var view = new Viewport(1, 2, 300, 400);
            if (view != Viewport.New(1, 2, 300, 400)) return $"ctor(int x4) {view} vs {Viewport.New(1, 2, 300, 400)}";
            var rect = new Rectangle(5, 6, 70, 80);
            if (new Viewport(rect) != Viewport.New(rect)) return "ctor(Rectangle)";
            if (view.Bounds != new Rectangle(1, 2, 300, 400)) return "Bounds";
            return Expect(view.ToString() == "{X:1 Y:2 Width:300 Height:400 MinDepth:0 MaxDepth:1}", view.ToString());
        });

        Check("gametime", () =>
        {
            var empty = new GameTime();
            if (empty.IsNull || empty.TotalGameTime != TimeSpan.Zero || empty.IsRunningSlowly) return "GameTime()";

            var time = new GameTime(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(16));
            if (time.TotalGameTime != TimeSpan.FromSeconds(5) || time.ElapsedGameTime != TimeSpan.FromMilliseconds(16)) return "GameTime(total, elapsed)";

            var slow = new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(33), true);
            return Expect(slow.IsRunningSlowly && slow.ElapsedGameTime == TimeSpan.FromMilliseconds(33), "GameTime(total, elapsed, slow)");
        });

        Check("point16", () =>
        {
            if (new Point16(new Point(3, 4)) != Point16.New(new Point(3, 4))) return "ctor(Point)";
            if (new Point16((short)3, (short)4) != Point16.New((short)3, (short)4)) return "ctor(short, short)";
            if (new Point16(7) != new Point16(7, 7) || new Point16((short)7) != new Point16(7, 7)) return "ctor(size)";

            var a = new Point16(9, 12);
            var b = new Point16(3, 4);
            if (a + b != new Point16(12, 16) || a - b != new Point16(6, 8)) return "+ or -";
            if (a * b != new Point16(27, 48) || a / b != new Point16(3, 3) || a % 5 != new Point16(4, 2)) return "* / or %";

            Point asPoint = a;
            if (asPoint != new Point(9, 12)) return "implicit Point";
            return Expect(a.ToString() == "{9, 12}", a.ToString());
        });

        Check("bounding_box", () =>
        {
            var box = new BoundingBox(Vector3.Zero, new Vector3(10));
            if (box.Contains(new Vector3(5)) != ContainmentType.Contains || box.Contains(new Vector3(11, 5, 5)) != ContainmentType.Disjoint) return "Contains(point)";
            if (box.Contains(new BoundingBox(new Vector3(2), new Vector3(3))) != ContainmentType.Contains) return "Contains(inner box)";
            if (box.Contains(new BoundingBox(new Vector3(5), new Vector3(15))) != ContainmentType.Intersects) return "Contains(overlapping box)";
            if (!box.Intersects(new BoundingSphere(new Vector3(12, 5, 5), 3)) || box.Intersects(new BoundingSphere(new Vector3(12, 5, 5), 1))) return "Intersects(sphere)";
            if (box.Intersects(new Plane(new Vector3(1, 0, 0), -5)) != PlaneIntersectionType.Intersecting) return "plane through";
            if (box.Intersects(new Plane(new Vector3(1, 0, 0), -20)) != PlaneIntersectionType.Back || box.Intersects(new Plane(new Vector3(1, 0, 0), 5)) != PlaneIntersectionType.Front) return "plane outside";

            var fromPoints = BoundingBox.CreateFromPoints(new[] { new Vector3(1, -2, 3), new Vector3(-4, 5, 0), new Vector3(2, 2, 2) });
            if (fromPoints.Min != new Vector3(-4, -2, 0) || fromPoints.Max != new Vector3(2, 5, 3)) return $"CreateFromPoints {fromPoints}";
            if (BoundingBox.CreateMerged(box, fromPoints) != new BoundingBox(new Vector3(-4, -2, 0), new Vector3(10))) return "CreateMerged";

            Vector3[] corners = box.GetCorners();
            if (corners.Length != 8 || corners[0] != new Vector3(0, 10, 10) || corners[7] != Vector3.Zero) return "GetCorners";
            return Expect(new Plane(new Vector3(0, 1, 0), -3).Intersects(box) == PlaneIntersectionType.Intersecting, "Plane.Intersects(box)");
        });

        Check("bounding_sphere", () =>
        {
            var sphere = BoundingSphere.CreateFromBoundingBox(new BoundingBox(Vector3.Zero, new Vector3(10)));
            if (sphere.Center != new Vector3(5) || !Near(sphere.Radius, MathF.Sqrt(75))) return $"CreateFromBoundingBox {sphere}";
            if (sphere.Contains(new Vector3(5, 5, 13)) != ContainmentType.Contains || sphere.Contains(new Vector3(5, 5, 14)) != ContainmentType.Disjoint) return "Contains(point)";
            if (!sphere.Intersects(new BoundingSphere(new Vector3(20, 5, 5), 7)) || sphere.Intersects(new BoundingSphere(new Vector3(20, 5, 5), 6))) return "Intersects(sphere)";

            var moved = new BoundingSphere(new Vector3(1, 2, 3), 4).Transform(Matrix.CreateTranslation(10, 20, 30));
            if (moved.Center != new Vector3(11, 22, 33) || !Near(moved.Radius, 4)) return $"Transform {moved}";

            var merged = BoundingSphere.CreateMerged(new BoundingSphere(Vector3.Zero, 1), new BoundingSphere(new Vector3(4, 0, 0), 1));
            if (!Near(merged.Center.X, 2) || !Near(merged.Radius, 3)) return $"CreateMerged {merged}";
            return Expect(new Plane(new Vector3(0, 0, 1), -100).Intersects(sphere) == PlaneIntersectionType.Back, "Plane.Intersects(sphere)");
        });

        Check("ray", () =>
        {
            var box = new BoundingBox(Vector3.Zero, new Vector3(10));
            float? hit = new Ray(new Vector3(-5, 5, 5), new Vector3(1, 0, 0)).Intersects(box);
            if (hit == null || !Near(hit.Value, 5)) return $"box hit {hit}";
            if (new Ray(new Vector3(-5, 5, 5), new Vector3(-1, 0, 0)).Intersects(box) != null) return "box behind";

            float? ball = new Ray(Vector3.Zero, new Vector3(1, 0, 0)).Intersects(new BoundingSphere(new Vector3(10, 0, 0), 2));
            if (ball == null || !Near(ball.Value, 8)) return $"sphere hit {ball}";

            float? plane = new Ray(Vector3.Zero, new Vector3(1, 0, 0)).Intersects(new Plane(new Vector3(1, 0, 0), -3));
            if (plane == null || !Near(plane.Value, 3)) return $"plane hit {plane}";
            return Expect(new Ray(Vector3.Zero, new Vector3(0, 1, 0)).Intersects(new Plane(new Vector3(1, 0, 0), -3)) == null, "parallel plane");
        });

        Check("bounding_frustum", () =>
        {
            var frustum = new BoundingFrustum(Matrix.CreateOrthographicOffCenter(0, 10, 0, 10, 0, 10));
            if (frustum.Contains(new Vector3(5, 5, -5)) != ContainmentType.Contains) return "inside point";
            if (frustum.Contains(new Vector3(5, 5, 5)) != ContainmentType.Disjoint || frustum.Contains(new Vector3(15, 5, -5)) != ContainmentType.Disjoint) return "outside point";
            if (frustum.Contains(new BoundingBox(new Vector3(1, 1, -3), new Vector3(2, 2, -2))) != ContainmentType.Contains) return "inner box";
            if (frustum.Contains(new BoundingBox(new Vector3(8, 8, -3), new Vector3(12, 12, -2))) != ContainmentType.Intersects) return "overlapping box";
            if (!frustum.Intersects(new BoundingSphere(new Vector3(11, 5, -5), 2)) || frustum.Intersects(new BoundingSphere(new Vector3(14, 5, -5), 2))) return "Intersects(sphere)";

            Vector3[] corners = frustum.GetCorners();
            foreach (Vector3 corner in corners)
            {
                bool onEdge = (Near(corner.X, 0) || Near(corner.X, 10)) && (Near(corner.Y, 0) || Near(corner.Y, 10)) && (Near(corner.Z, 0) || Near(corner.Z, -10));
                if (!onEdge) return $"corner {corner}";
            }

            float? inside = new Ray(new Vector3(5, 5, -5), new Vector3(0, 0, -1)).Intersects(frustum);
            float? outside = new Ray(new Vector3(50, 5, -5), new Vector3(0, 0, -1)).Intersects(frustum);
            return Expect(inside == 0f && outside == null, $"ray {inside} / {outside}");
        });

        Check("curve", () =>
        {
            var curve = new Curve();
            curve.Keys.Add(new CurveKey(1, 10));
            curve.Keys.Add(new CurveKey(0, 0));
            if (curve.Keys.Count != 2 || curve.Keys[0].Position != 0) return "keys are not sorted";
            if (!Near(curve.Evaluate(0.5f), 5)) return $"smooth midpoint {curve.Evaluate(0.5f)}";
            if (!Near(curve.Evaluate(-1), 0) || !Near(curve.Evaluate(2), 10)) return "constant loops";

            curve.ComputeTangents(CurveTangent.Linear);
            if (!Near(curve.Evaluate(0.25f), 2.5f)) return $"linear {curve.Evaluate(0.25f)}";

            curve.PostLoop = CurveLoopType.Linear;
            if (!Near(curve.Evaluate(2), 20)) return $"linear post loop {curve.Evaluate(2)}";

            curve.PostLoop = CurveLoopType.Cycle;
            if (!Near(curve.Evaluate(1.25f), 2.5f)) return $"cycle {curve.Evaluate(1.25f)}";
            return Expect(!curve.IsConstant && curve.Clone().Keys.Count == 2, "IsConstant or Clone");
        });

        Check("color_converter", () =>
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Color));
            if (converter is not Microsoft.Xna.Framework.Design.ColorConverter) return $"converter is {converter.GetType().Name}";

            object? parsed = converter.ConvertFromInvariantString("73, 94, 171, 255");
            if (parsed is not Color color || color != new Color(73, 94, 171, 255)) return $"ConvertFrom {parsed}";
            if (converter.ConvertToInvariantString(color) != "73, 94, 171, 255") return $"ConvertTo {converter.ConvertToInvariantString(color)}";

            var values = new Hashtable { ["R"] = 1, ["G"] = 2, ["B"] = 3, ["A"] = 4 };
            return Expect(converter.GetCreateInstanceSupported() && (Color)converter.CreateInstance(values)! == new Color(1, 2, 3, 4), "CreateInstance");
        });

        Check("effect_bytes", () =>
        {
            var effect = new Effect(Main.instance.GraphicsDevice, ModContent.GetInstance<CsFnaGapsMod>().GetFileBytes("Effects/FnaGaps.fxc"));
            EffectParameter time = effect.Parameters["uTime"];
            if (time.IsNull) return "no uTime";
            if (time.ParameterClass != EffectParameterClass.Scalar || time.ParameterType != EffectParameterType.Single) return $"uTime {time.ParameterClass} {time.ParameterType}";
            if (time.RowCount != 1 || time.ColumnCount != 1 || time.Semantic != "TIME") return $"uTime {time.RowCount}x{time.ColumnCount} '{time.Semantic}'";

            EffectAnnotationCollection notes = time.Annotations;
            if (notes.Count != 4) return $"uTime has {notes.Count} annotations";
            if (notes["UIName"]?.GetValueString() != "Time" || notes["UIName"]!.ParameterType != EffectParameterType.String) return "UIName";
            if (!Near(notes["UIMin"]!.GetValueSingle(), 0.5f) || notes["Steps"]!.GetValueInt32() != 3 || !notes["Visible"]!.GetValueBoolean()) return "UIMin, Steps or Visible";

            EffectParameter color = effect.Parameters["uColor"], matrix = effect.Parameters["uMatrix"], image = effect.Parameters["uImage0"];
            if (color.ParameterClass != EffectParameterClass.Vector || color.ColumnCount != 3) return $"uColor {color.ParameterClass} {color.RowCount}x{color.ColumnCount}";
            if (matrix.ParameterClass != EffectParameterClass.Matrix || matrix.RowCount != 4 || matrix.ColumnCount != 4) return $"uMatrix {matrix.ParameterClass} {matrix.RowCount}x{matrix.ColumnCount}";
            if (image.ParameterClass != EffectParameterClass.Object || image.ParameterType != EffectParameterType.Texture) return $"uImage0 {image.ParameterClass} {image.ParameterType}";
            if (!color.Elements.IsNull) return "Elements of a plain parameter";

            try
            {
                _ = effect.Parameters["uArray"].Elements;
                return "array Elements did not throw";
            }
            catch (NotSupportedException) { }

            if (effect.Parameters.GetParameterBySemantic("TIME") != time) return "GetParameterBySemantic";
            if (effect.CurrentTechnique.Annotations["Kind"]?.GetValueString() != "test") return "technique annotation";
            if (effect.CurrentTechnique.Passes["Main"].Annotations["Order"]?.GetValueInt32() != 7) return "pass annotation";
            return Expect(effect.Parameters["uOpacity"].ParameterType == EffectParameterType.Void, "BL's extra parameter");
        });

        Check("effect_clone", () =>
        {
            var effect = new Effect(Main.instance.GraphicsDevice, ModContent.GetInstance<CsFnaGapsMod>().GetFileBytes("Effects/FnaGaps.fxc"));
            effect.Parameters["uTime"].SetValue(0.25f);
            effect.Parameters["uColor"].SetValue(new Vector3(1, 2, 3));

            Effect clone = effect.Clone();
            var constructed = new Effect(effect);
            if (clone == effect || constructed == effect || clone == constructed) return "the clone is the same object";
            if (!Near(clone.Parameters["uTime"].GetValueSingle(), 0.25f) || clone.Parameters["uColor"].GetValueVector3() != new Vector3(1, 2, 3)) return "values were not copied";
            if (!Near(constructed.Parameters["uTime"].GetValueSingle(), 0.25f)) return "new Effect(Effect) did not copy";

            clone.Parameters["uTime"].SetValue(0.75f);
            if (!Near(effect.Parameters["uTime"].GetValueSingle(), 0.25f)) return "the clone shares its values";
            return Expect(clone.CurrentTechnique.Name == "Technique1" && clone.Parameters["uTime"].Semantic == "TIME", "technique or metadata of the clone");
        });

        Check("save_png", () =>
        {
            var colors = new[] { Color.Red, Color.Lime, Color.Blue, new Color(10, 20, 30), new Color(200, 100, 50), Color.White };
            var texture = new Texture2D(Main.instance.GraphicsDevice, 3, 2);
            texture.SetData(colors);

            var png = new MemoryStream();
            texture.SaveAsPng(png, 3, 2);
            byte[] bytes = png.ToArray();
            if (bytes.Length < 8 || bytes[1] != 'P' || bytes[2] != 'N' || bytes[3] != 'G') return "not a PNG";

            png.Position = 0;
            Texture2D back = Texture2D.FromStream(Main.instance.GraphicsDevice, png);
            var read = new Color[6];
            back.GetData(read);
            if (back.Width != 3 || back.Height != 2) return $"size {back.Width}x{back.Height}";
            for (int i = 0; i < 6; i++)
                if (read[i] != colors[i]) return $"pixel {i}: {read[i]} vs {colors[i]}";

            var scaled = new MemoryStream();
            texture.SaveAsPng(scaled, 6, 4);
            scaled.Position = 0;
            Texture2D big = Texture2D.FromStream(Main.instance.GraphicsDevice, scaled);
            var large = new Color[24];
            big.GetData(large);
            return Expect(big.Width == 6 && big.Height == 4 && large[0] == colors[0] && large[23] == colors[5] && large[3 * 6 + 2] == colors[4], $"scaled {big.Width}x{big.Height}");
        });

        Check("string_builder", () =>
        {
            SpriteFont font = FontAssets.MouseText.Value;
            if (font.MeasureString(new StringBuilder("Bunny Loader")) != font.MeasureString("Bunny Loader")) return "MeasureString";
            return GapsDraw.Drawn ? GapsDraw.DrawProblem : "PostDrawInterface never ran";
        });

        Check("sort_modes", () =>
        {
            if ((int)SpriteSortMode.Texture != 2 || (int)SpriteSortMode.BackToFront != 3 || (int)SpriteSortMode.FrontToBack != 4) return "enum values";
            return GapsDraw.Drawn ? GapsDraw.SortProblem : "PostDrawInterface never ran";
        });
    }
}

public sealed class GapsDraw : ModSystem
{
    internal static bool Drawn;

    internal static string? DrawProblem, SortProblem;

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (Main.gameMenu || SortProblem != null || DrawProblem != null) return;
        SpriteFont font = FontAssets.MouseText.Value;
        var text = new StringBuilder("csfnagaps");
        var position = new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.25f);

        try
        {
            spriteBatch.DrawString(font, text, position, Color.Gold);
            spriteBatch.DrawString(font, text, position + new Vector2(0, 30), Color.Gold, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, position + new Vector2(0, 60), Color.Gold, 0f, Vector2.Zero, new Vector2(1.5f, 1f), SpriteEffects.None, 0f);

            foreach (int x in new[] { 100, 400, 700 })
                spriteBatch.DrawString(font, $"short {x}", new Vector2(x, Main.screenHeight * 0.6f), Color.Cyan);
            spriteBatch.DrawString(font, "two\nlines", new Vector2(100, Main.screenHeight * 0.6f + 40), Color.Cyan);
        }
        catch (Exception e)
        {
            DrawProblem = "DrawString: " + e.Message;
        }

        try
        {
            spriteBatch.End();
            foreach (SpriteSortMode mode in new[] { SpriteSortMode.Texture, SpriteSortMode.BackToFront, SpriteSortMode.FrontToBack })
            {
                spriteBatch.Begin(mode, BlendState.AlphaBlend);
                spriteBatch.DrawString(font, new StringBuilder(mode.ToString()), position + new Vector2(0, 30 + 30 * (int)mode), Color.White);
                spriteBatch.End();
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }
        catch (Exception e)
        {
            SortProblem = "Begin: " + e.Message;
        }

        Drawn = true;
    }
}
