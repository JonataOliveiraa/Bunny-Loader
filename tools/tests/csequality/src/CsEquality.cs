using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CsEquality;

public sealed class CsEqualityMod : Mod
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsEqualityMod>().Logger;

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
        Log.Info($"csequality END pass={pass} fail={fail}");
    }

    static void Check(string name, Func<string?> test)
    {
        string? problem;
        try { problem = test(); }
        catch (Exception e) { problem = e.GetType().Name + ": " + e.Message; }

        if (problem == null) pass++;
        else fail++;
        Log.Info($"csequality {name}: {(problem == null ? "ok" : "FAIL " + problem)}");
    }

    static string? Expect(bool condition, string what) => condition ? null : what;

    static void Run()
    {
        Check("rectangle", () =>
        {
            var a = new Rectangle(1, 2, 3, 4);
            var b = new Rectangle(1, 2, 3, 4);
            var c = new Rectangle(1, 2, 3, 5);
            if (!(a == b) || a != b) return "equal rectangles differ";
            if (a == c || !(a != c)) return "different rectangles are equal";
            if (!a.Equals(b) || !a.Equals((object)b) || a.Equals((object)c)) return "Equals is wrong";
            if (a.GetHashCode() != b.GetHashCode()) return "hash differs";
            return Expect(Rectangle.Empty == default, "Empty != default");
        });

        Check("rectangle_game_agrees", () =>
        {
            var a = new Rectangle(5, 6, 7, 8);
            var b = new Rectangle(5, 6, 7, 8);
            var c = new Rectangle(0, 6, 7, 8);
            return Expect(a.Equals_(b) == (a == b) && a.Equals_(c) == (a == c), $"game {a.Equals_(b)}/{a.Equals_(c)}");
        });

        Check("point", () =>
        {
            var set = new HashSet<Point> { new Point(1, 2), new Point(1, 2), new Point(2, 1) };
            if (set.Count != 2) return $"set has {set.Count}";
            return Expect(new Point(3, 4) == new Point(3, 4) && new Point(3, 4) != new Point(4, 3), "operators");
        });

        Check("vectors", () =>
        {
            if (new Vector2(1, 2) != new Vector2(1, 2)) return "Vector2";
            if (new Vector3(1, 2, 3) != new Vector3(1, 2, 3) || new Vector3(1, 2, 3) == new Vector3(1, 2, 4)) return "Vector3";
            if (new Vector4(1, 2, 3, 4) != new Vector4(1, 2, 3, 4) || new Vector4(1, 2, 3, 4) == new Vector4(0, 2, 3, 4)) return "Vector4";
            var nan = new Vector2(float.NaN, 0);
            Vector2 same = nan;
            return Expect(nan != same, "NaN compares equal");
        });

        Check("color", () =>
        {
            Color white = Color.White;
            if (white != new Color(255, 255, 255)) return $"White is {white}";
            if (white == Color.Black) return "White == Black";
            IEquatable<Color> eq = white;
            if (!eq.Equals(new Color(255, 255, 255, 255))) return "IEquatable";
            return Expect(white.Equals_(new Color(255, 255, 255)), "game says different");
        });

        Check("matrix", () =>
        {
            Matrix a = Matrix.Identity, b = Matrix.Identity;
            if (a != b) return "Identity != Identity";
            b.M42 = 3;
            if (a == b) return "changed matrix still equal";
            if (a.Equals_(b) != (a == b)) return "game disagrees";
            return Expect(Matrix.CreateTranslation(1, 2, 0) * Matrix.Identity == Matrix.CreateTranslation(1, 2, 0), "product");
        });

        Check("point16", () =>
        {
            var a = new Point16 { X = 10, Y = 20 };
            var b = new Point16 { X = 10, Y = 20 };
            var map = new Dictionary<Point16, int> { [a] = 7 };
            if (!map.TryGetValue(b, out int v) || v != 7) return "dictionary lookup failed";
            return Expect(a == b && a != new Point16 { X = 10, Y = 21 }, "operators");
        });

        Check("keyboard", () =>
        {
            KeyboardState now = Main.keyState;
            if (now != Main.keyState) return "same state differs";
            return Expect(default(KeyboardState) == new KeyboardState(), "default differs");
        });

        Check("mouse", () =>
        {
            var a = new MouseState { X = 3, Y = 4 };
            var b = new MouseState { X = 3, Y = 4 };
            if (a != b) return "equal states differ";
            b.Y = 5;
            return Expect(a != b, "different states are equal");
        });

        Check("comparer", () =>
        {
            if (!typeof(IEquatable<Rectangle>).IsAssignableFrom(typeof(Rectangle))) return "Rectangle is not IEquatable";
            return Expect(EqualityComparer<Vector3>.Default.Equals(new Vector3(1, 1, 1), new Vector3(1, 1, 1)), "comparer");
        });
    }
}
