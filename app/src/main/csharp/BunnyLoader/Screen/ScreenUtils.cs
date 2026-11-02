using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader.Core;

namespace BunnyLoader;

public static class ScreenUtils
{
    public static class Space
    {
        static int frame = -1;
        static float x, y, width = 1, height = 1;

        internal static void BeginFrame(int at)
        {
            frame = at;
            Vector2 position = Main.screenPosition;
            x = position.X;
            y = position.Y;
            width = Main.screenWidth > 0 ? Main.screenWidth : 1;
            height = Main.screenHeight > 0 ? Main.screenHeight : 1;
        }

        static void Fresh()
        {
            if (frame != ScreenEffects.Frame) BeginFrame(ScreenEffects.Frame);
        }

        public static Vector2 WorldToUV(Vector2 position)
        {
            Fresh();
            return new Vector2((position.X - x) / width, 1f - (position.Y - y) / height);
        }

        public static Vector2 UVToWorld(Vector2 uv)
        {
            Fresh();
            return new Vector2(x + uv.X * width, y + (1f - uv.Y) * height);
        }

        public static float PixelsToUV(float pixels)
        {
            Fresh();
            return pixels / height;
        }

        public static float UVToPixels(float fraction)
        {
            Fresh();
            return fraction * height;
        }

        public static float Aspect
        {
            get
            {
                Fresh();
                return width / height;
            }
        }

        public static Vector2 Size
        {
            get
            {
                Fresh();
                return new Vector2(width, height);
            }
        }

        public static bool IsOnScreen(Vector2 position, float margin = 0)
        {
            Fresh();
            float px = position.X - x, py = position.Y - y;
            return px >= -margin && py >= -margin && px <= width + margin && py <= height + margin;
        }
    }

    public static class Easing
    {
        public static readonly Func<float, float> Linear = t => t;
        public static readonly Func<float, float> InSine = t => 1 - MathF.Cos(t * MathF.PI / 2);
        public static readonly Func<float, float> OutSine = t => MathF.Sin(t * MathF.PI / 2);
        public static readonly Func<float, float> InOutSine = t => -(MathF.Cos(MathF.PI * t) - 1) / 2;
        public static readonly Func<float, float> InQuad = t => t * t;
        public static readonly Func<float, float> OutQuad = t => 1 - (1 - t) * (1 - t);
        public static readonly Func<float, float> InOutQuad = t => t < 0.5f ? 2 * t * t : 1 - MathF.Pow(-2 * t + 2, 2) / 2;
        public static readonly Func<float, float> InCubic = t => t * t * t;
        public static readonly Func<float, float> OutCubic = t => 1 - MathF.Pow(1 - t, 3);
        public static readonly Func<float, float> InOutCubic = t => t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2;
        public static readonly Func<float, float> InExpo = t => t == 0 ? 0 : MathF.Pow(2, 10 * t - 10);
        public static readonly Func<float, float> OutExpo = t => t == 1 ? 1 : 1 - MathF.Pow(2, -10 * t);
        public static readonly Func<float, float> InOutExpo = t => t == 0 ? 0 : t == 1 ? 1 : t < 0.5f ? MathF.Pow(2, 20 * t - 10) / 2 : (2 - MathF.Pow(2, -20 * t + 10)) / 2;
        public static readonly Func<float, float> InBack = t => 2.70158f * t * t * t - 1.70158f * t * t;
        public static readonly Func<float, float> OutBack = t => 1 + 2.70158f * MathF.Pow(t - 1, 3) + 1.70158f * MathF.Pow(t - 1, 2);
        public static readonly Func<float, float> OutElastic = t => t == 0 ? 0 : t == 1 ? 1 : MathF.Pow(2, -10 * t) * MathF.Sin((t * 10 - 0.75f) * (2 * MathF.PI / 3)) + 1;

        public static readonly Func<float, float> OutBounce = t =>
        {
            const float n = 7.5625f, d = 2.75f;
            if (t < 1 / d) return n * t * t;
            if (t < 2 / d) return n * (t -= 1.5f / d) * t + 0.75f;
            if (t < 2.5f / d) return n * (t -= 2.25f / d) * t + 0.9375f;
            return n * (t -= 2.625f / d) * t + 0.984375f;
        };
    }

    public sealed class Mask
    {
        public enum Falloff
        {
            Linear = 0,
            Smooth = 1,
            Exponential = 2,
        }

        public enum Direction
        {
            Up,
            Down,
            Left,
            Right,
        }

        readonly int type;
        readonly bool inverted;
        readonly Falloff falloff;
        readonly Vector2 center;
        readonly float radius, width, feather, start, end, softness, roundness;
        readonly Rectangle rect;
        readonly Vector2 direction;
        readonly ScreenGraphics.Target? texture;
        readonly bool luma;

        Mask(int type, Falloff falloff, bool inverted = false, Vector2 center = default, float radius = 0, float width = 0, float feather = 0,
            Rectangle rect = default, Vector2 direction = default, float start = 0, float end = 0, float softness = 0, float roundness = 0,
            ScreenGraphics.Target? texture = null, bool luma = false)
        {
            this.type = type;
            this.falloff = falloff;
            this.inverted = inverted;
            this.center = center;
            this.radius = radius;
            this.width = width;
            this.feather = feather;
            this.rect = rect;
            this.direction = direction;
            this.start = start;
            this.end = end;
            this.softness = softness;
            this.roundness = roundness;
            this.texture = texture;
            this.luma = luma;
        }

        public static Mask None() => new(0, Falloff.Smooth);

        public static Mask Circle(Vector2 center, float radius, float? feather = null, Falloff falloff = Falloff.Smooth) =>
            new(1, falloff, center: center, radius: radius, feather: feather ?? radius * 0.25f);

        public static Mask Ring(Vector2 center, float radius, float width, float? feather = null, Falloff falloff = Falloff.Smooth) =>
            new(2, falloff, center: center, radius: radius, width: width, feather: feather ?? width * 0.5f);

        public static Mask Rect(Rectangle rect, float feather = 16, Falloff falloff = Falloff.Smooth) =>
            new(3, falloff, rect: rect, feather: feather);

        public static Mask Gradient(Direction direction = Direction.Up, float start = 0, float end = 1, Falloff falloff = Falloff.Smooth) =>
            Gradient(direction switch
            {
                Direction.Down => new Vector2(0, -1),
                Direction.Left => new Vector2(-1, 0),
                Direction.Right => new Vector2(1, 0),
                _ => new Vector2(0, 1),
            }, start, end, falloff);

        public static Mask Gradient(Vector2 direction, float start = 0, float end = 1, Falloff falloff = Falloff.Smooth)
        {
            float length = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            if (length <= 0) length = 1;
            return new Mask(4, falloff, direction: direction / length, start: start, end: end);
        }

        public static Mask Vignette(float start = 0.45f, float softness = 0.55f, float roundness = 1, Falloff falloff = Falloff.Smooth) =>
            new(5, falloff, start: start, softness: softness, roundness: roundness);

        public static Mask Texture(ScreenGraphics.Target texture, bool luma = false, Falloff falloff = Falloff.Smooth) =>
            new(6, falloff, texture: texture ?? throw new ArgumentNullException(nameof(texture)), luma: luma);

        public Mask Inverted() =>
            new(type, falloff, !inverted, center, radius, width, feather, rect, direction, start, end, softness, roundness, texture, luma);

        public ScreenGraphics.Target? TextureSource => type == 6 ? texture : null;

        public Dictionary<string, object> ToUniforms()
        {
            float[] a = { 0, 0, 0, 0 };
            float[] b = { (int)falloff, inverted ? 1 : 0, 0, 0 };
            if (type == 1 || type == 2)
            {
                Vector2 uv = Space.WorldToUV(center);
                float r = Space.PixelsToUV(radius);
                if (type == 1)
                {
                    a = new[] { uv.X, uv.Y, r, MathF.Min(Space.PixelsToUV(feather), r) };
                }
                else
                {
                    a = new[] { uv.X, uv.Y, r, Space.PixelsToUV(width) };
                    b[2] = Space.PixelsToUV(feather);
                }
            }
            else if (type == 3)
            {
                a = RectUV();
                b[2] = Space.PixelsToUV(feather);
            }
            else if (type == 4)
            {
                a = new[] { direction.X, direction.Y, start, end };
            }
            else if (type == 5)
            {
                a = new[] { start, roundness, softness, 0f };
            }
            else if (type == 6)
            {
                b[2] = luma ? 1 : 0;
            }
            return new Dictionary<string, object> { ["uMaskType"] = (float)type, ["uMaskA"] = a, ["uMaskB"] = b };
        }

        public float[]? Bounds()
        {
            if (inverted) return null;
            if (type == 1 || type == 2)
            {
                Vector2 uv = Space.WorldToUV(center);
                float r = Space.PixelsToUV(radius + (type == 2 ? width / 2 + feather : 0));
                float rx = r / Space.Aspect;
                return new[] { uv.X - rx, uv.Y - r, uv.X + rx, uv.Y + r };
            }
            if (type == 3)
            {
                float f = Space.PixelsToUV(feather);
                float[] u = RectUV();
                return new[] { u[0] - f, u[1] - f, u[2] + f, u[3] + f };
            }
            return null;
        }

        float[] RectUV()
        {
            Vector2 lo = Space.WorldToUV(new Vector2(rect.X, rect.Y + rect.Height));
            Vector2 hi = Space.WorldToUV(new Vector2(rect.X + rect.Width, rect.Y));
            return new[] { lo.X, lo.Y, hi.X, hi.Y };
        }
    }

    public sealed class Tween
    {
        static List<Tween> active = new();

        bool started, killed;
        readonly Func<float> get;
        readonly Action<float> set;

        public object Target { get; }

        public string Property { get; }

        public float Start { get; private set; }

        public float End { get; }

        public float Duration { get; }

        public Func<float, float> Easing { get; }

        public float Delay { get; }

        public float Age { get; private set; }

        public Action<object>? OnDone { get; }

        Tween(object target, string property, Func<float> get, Action<float> set, float to, float duration, Func<float, float>? easing, float delay, Action<object>? onDone)
        {
            Target = target;
            Property = property;
            this.get = get;
            this.set = set;
            End = to;
            Duration = MathF.Max(duration, 0);
            Easing = easing ?? ScreenUtils.Easing.Linear;
            Delay = MathF.Max(delay, 0);
            OnDone = onDone;
        }

        public static Tween To(object target, string property, float to, float duration, Func<float, float>? easing = null, float delay = 0, Action<object>? onDone = null)
        {
            ArgumentNullException.ThrowIfNull(target);
            var member = target.GetType().GetProperty(property);
            Func<float> get;
            Action<float> set;
            if (member != null && member.CanRead && member.CanWrite && IsNumber(member.PropertyType))
            {
                get = () => Convert.ToSingle(member.GetValue(target));
                set = v => member.SetValue(target, Convert.ChangeType(v, member.PropertyType));
            }
            else if (target.GetType().GetField(property) is { IsInitOnly: false } field && IsNumber(field.FieldType))
            {
                get = () => Convert.ToSingle(field.GetValue(target));
                set = v => field.SetValue(target, Convert.ChangeType(v, field.FieldType));
            }
            else
            {
                throw new ArgumentException($"Tween: {property} is not a writable number of {target.GetType().Name}");
            }
            return Add(new Tween(target, property, get, set, to, duration, easing, delay, onDone));
        }

        public static Tween To(object target, Func<float> get, Action<float> set, float to, float duration, Func<float, float>? easing = null, float delay = 0, Action<object>? onDone = null) =>
            Add(new Tween(target, "", get, set, to, duration, easing, delay, onDone));

        static Tween Add(Tween tween)
        {
            if (tween.Property.Length > 0)
            {
                foreach (Tween t in active)
                    if (ReferenceEquals(t.Target, tween.Target) && t.Property == tween.Property) t.Kill();
            }
            active.Add(tween);
            ScreenEffects.Install();
            return tween;
        }

        static bool IsNumber(Type type) => type == typeof(float) || type == typeof(double) || type == typeof(int);

        public static int Count => active.Count;

        public bool Done => killed;

        public void Kill() => killed = true;

        internal static void Update(float delta)
        {
            if (active.Count == 0) return;
            foreach (Tween t in active.ToArray())
            {
                if (t.killed) continue;
                try { t.Step(delta); }
                catch (Exception e)
                {
                    ItemHooks.Report($"Tween {t.Property}", e);
                    t.Kill();
                }
            }
            active = active.Where(t => !t.killed).ToList();
        }

        void Step(float delta)
        {
            if (Target is ModScreenEffect fx && !fx.Active)
            {
                Kill();
                return;
            }
            Age += delta;
            float time = Age - Delay;
            if (time < 0) return;
            if (!started)
            {
                started = true;
                Start = get();
            }
            float t = Duration > 0 ? MathF.Min(time / Duration, 1) : 1;
            set(Start + (End - Start) * Easing(t));
            if (t >= 1)
            {
                Kill();
                OnDone?.Invoke(Target);
            }
        }
    }

    public sealed class Curve
    {
        readonly (float T, float Value)[] points;
        readonly Func<float, float> easing;

        public Curve(IEnumerable<(float T, float Value)> points, Func<float, float>? easing = null)
        {
            this.points = points.OrderBy(p => p.T).ToArray();
            if (this.points.Length == 0) throw new ArgumentException("Curve: pass at least one (t, value) point");
            this.easing = easing ?? Easing.Linear;
        }

        public float At(float t)
        {
            if (t <= points[0].T) return points[0].Value;
            for (int i = 1; i < points.Length; i++)
            {
                (float t1, float v1) = points[i];
                if (t > t1) continue;
                (float t0, float v0) = points[i - 1];
                float local = t1 > t0 ? (t - t0) / (t1 - t0) : 1;
                return v0 + (v1 - v0) * easing(local);
            }
            return points[^1].Value;
        }
    }

    public static class ColorMath
    {
        public static float[] ToFloats(Color color) => new[] { color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f };

        public static Color FromHex(string hex)
        {
            string h = hex.TrimStart('#');
            if ((h.Length != 6 && h.Length != 8) || !h.All(Uri.IsHexDigit)) throw new FormatException("ColorMath.FromHex: expected #rrggbb or #rrggbbaa");
            int Byte(int i) => Convert.ToInt32(h.Substring(i, 2), 16);
            return new Color(Byte(0), Byte(2), Byte(4), h.Length == 8 ? Byte(6) : 255);
        }

        public static Color Hsv(float h, float s, float v, float a = 1)
        {
            float hue = ((h % 360 + 360) % 360) / 60;
            float F(float n)
            {
                float k = (n + hue) % 6;
                return v - v * s * MathF.Max(MathF.Min(MathF.Min(k, 4 - k), 1), 0);
            }
            return new Color(F(5), F(3), F(1), a);
        }

        public static Color Lerp(Color a, Color b, float t) =>
            new((a.R + (b.R - a.R) * t) / 255f, (a.G + (b.G - a.G) * t) / 255f, (a.B + (b.B - a.B) * t) / 255f, (a.A + (b.A - a.A) * t) / 255f);

        public static Color Temperature(float kelvin)
        {
            float t = Math.Clamp(kelvin, 1000, 40000) / 100;
            static float Clamp(double v) => (float)(Math.Clamp(v, 0, 255) / 255);
            double r = t <= 66 ? 255 : 329.698727446 * Math.Pow(t - 60, -0.1332047592);
            double g = t <= 66 ? 99.4708025861 * Math.Log(t) - 161.1195681661 : 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
            double b = t >= 66 ? 255 : t <= 19 ? 0 : 138.5177312231 * Math.Log(t - 10) - 305.0447927307;
            return new Color(Clamp(r), Clamp(g), Clamp(b), 1f);
        }

        public static float Luma(Color color) => (0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B) / 255f;
    }
}
