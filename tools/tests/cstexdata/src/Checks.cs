using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CsTexData;

public static class Checks
{
    static int pass, fail;

    public static Texture2D Created, FromStream, ModImage;

    static ILog Log => ModContent.GetInstance<CsTexDataMod>().Logger;

    static GraphicsDevice Device => Main.graphics.GraphicsDevice;

    public static void Reset() => pass = fail = 0;

    static void Check(string name, Func<string?> test)
    {
        string? problem;
        try { problem = test(); }
        catch (Exception e) { problem = e.GetType().Name + ": " + e.Message; }

        if (problem == null) pass++;
        else fail++;
        Log.Info($"cstexdata {name}: {(problem == null ? "ok" : "FAIL " + problem)}");
    }

    static void Throws<TException>(string name, Action action) where TException : Exception => Check(name, () =>
    {
        try { action(); }
        catch (TException e) { return e.Message.Length > 0 ? null : "empty message"; }
        catch (AggregateException e) when (e.InnerException is TException) { return null; }
        return $"no {typeof(TException).Name}";
    });

    public static void Run()
    {
        Created = new Texture2D(Device, Pattern.Width, Pattern.Height);

        Check("create_size", () => Created.Width == Pattern.Width && Created.Height == Pattern.Height ? null : $"{Created.Width}x{Created.Height}");

        Check("create_is_clear", () =>
        {
            var data = new Color[Pattern.Width * Pattern.Height];
            Created.GetData(data);
            return Array.TrueForAll(data, c => c == default) ? null : "a new texture has pixels";
        });

        Check("set_get_round_trip", () =>
        {
            Created.SetData(Pattern.Pixels());
            var back = new Color[Pattern.Width * Pattern.Height];
            Created.GetData(back);
            return Pattern.Compare(back, Pattern.Pixels());
        });

        Check("alpha_kept", () =>
        {
            var tex = new Texture2D(Device, 1, 1);
            tex.SetData(new[] { new Color(10, 20, 30, 128) });
            var back = new Color[1];
            tex.GetData(back);
            return back[0] == new Color(10, 20, 30, 128) ? null : Pattern.Text(back[0]);
        });

        Check("set_region", () =>
        {
            var tex = new Texture2D(Device, Pattern.Width, Pattern.Height);
            tex.SetData(Pattern.Pixels());
            var red = new Color(255, 0, 0, 255);
            tex.SetData(0, new Rectangle(1, 1, 2, 1), new[] { red, red }, 0, 2);

            var back = new Color[Pattern.Width * Pattern.Height];
            tex.GetData(back);
            Color[] expected = Pattern.Pixels();
            expected[1 * Pattern.Width + 1] = red;
            expected[1 * Pattern.Width + 2] = red;
            return Pattern.Compare(back, expected);
        });

        Check("get_region", () =>
        {
            var cell = new Color[2];
            Created.GetData(0, new Rectangle(2, 2, 2, 1), cell, 0, 2);
            return cell[0] == Pattern.At(2, 2) && cell[1] == Pattern.At(3, 2) ? null : $"{Pattern.Text(cell[0])} {Pattern.Text(cell[1])}";
        });

        Check("start_index", () =>
        {
            var tex = new Texture2D(Device, 1, 1);
            var data = new[] { default(Color), new Color(1, 2, 3, 4), default };
            tex.SetData(data, 1, 1);
            var back = new Color[3];
            tex.GetData(back, 2, 1);
            return back[2] == new Color(1, 2, 3, 4) && back[0] == default ? null : Pattern.Text(back[2]);
        });

        Check("uint_data", () =>
        {
            var tex = new Texture2D(Device, 1, 1);
            tex.SetData(new[] { 0x80302010u });
            var back = new Color[1];
            tex.GetData(back);
            return back[0] == new Color(0x10, 0x20, 0x30, 0x80) ? null : Pattern.Text(back[0]);
        });

        Check("byte_data", () =>
        {
            var tex = new Texture2D(Device, 1, 1);
            tex.SetData(new byte[] { 9, 8, 7, 6 });
            var back = new byte[4];
            tex.GetData(back);
            return back[0] == 9 && back[1] == 8 && back[2] == 7 && back[3] == 6 ? null : string.Join(",", back);
        });

        Check("from_stream", () =>
        {
            using var stream = new MemoryStream(Pattern.PngBytes());
            FromStream = Texture2D.FromStream(Device, stream);
            if (FromStream.Width != Pattern.Width || FromStream.Height != Pattern.Height) return $"{FromStream.Width}x{FromStream.Height}";
            var back = new Color[Pattern.Width * Pattern.Height];
            FromStream.GetData(back);
            return Pattern.Compare(back, Pattern.Pixels());
        });

        Check("mod_image_get", () =>
        {
            ModImage = ModContent.Request<Texture2D>("CsTexData/Content/Pattern", AssetRequestMode.ImmediateLoad).Value;
            var back = new Color[Pattern.Width * Pattern.Height];
            ModImage.GetData(back);
            return Pattern.Compare(back, Pattern.Pixels());
        });

        Check("mutable_constructor", () =>
        {
            var tex = new Texture2D(Device, 2, 2, false, SurfaceFormat.Color);
            return tex.Width == 2 && tex.Height == 2 ? null : $"{tex.Width}x{tex.Height}";
        });

        Throws<ArgumentException>("error_wrong_size", () => Created.SetData(new Color[5]));
        Throws<NotSupportedException>("error_level", () => Created.SetData(1, null, new Color[12], 0, 12));
        Throws<ArgumentOutOfRangeException>("error_region", () => Created.SetData(0, new Rectangle(3, 2, 2, 2), new Color[4], 0, 4));
        Throws<NotSupportedException>("error_format", () => new Texture2D(Device, 2, 2, false, SurfaceFormat.Alpha8));
        Throws<InvalidOperationException>("error_other_thread", () => Task.Run(() => Created.SetData(Pattern.Pixels())).Wait());
        Throws<InvalidDataException>("error_not_png", () => Texture2D.FromStream(Device, new MemoryStream(new byte[] { 1, 2, 3 })));

        Check("vanilla_texture", () =>
        {
            Texture2D vanilla = TextureAssets.Item[1].Value;
            var data = new Color[vanilla.Width * vanilla.Height];
            try
            {
                vanilla.GetData(data);
                Log.Info($"cstexdata vanilla Item_1 {vanilla.Width}x{vanilla.Height}: GetData works");
            }
            catch (NotSupportedException e)
            {
                Log.Info($"cstexdata vanilla Item_1 {vanilla.Width}x{vanilla.Height}: {e.Message}");
            }
            return null;
        });

        Perf(16);
        Perf(64);
        Perf(256);

        Log.Info($"cstexdata END pass={pass} fail={fail}");
    }

    static void Perf(int size)
    {
        var tex = new Texture2D(Device, size, size);
        var data = new Color[size * size];
        for (int i = 0; i < data.Length; i++) data[i] = new Color(i & 255, (i >> 8) & 255, 128, 255);

        double Time(Action action)
        {
            action();
            var runs = new double[9];
            for (int r = 0; r < runs.Length; r++)
            {
                long t0 = Stopwatch.GetTimestamp();
                action();
                runs[r] = (Stopwatch.GetTimestamp() - t0) * 1e6 / Stopwatch.Frequency;
            }
            Array.Sort(runs);
            return runs[runs.Length / 2];
        }

        double set = Time(() => tex.SetData(data));
        double get = Time(() => tex.GetData(data));
        double create = Time(() => new Texture2D(Device, size, size));
        Log.Info($"cstexdata perf {size}x{size}: SetData {set:0.0} us, GetData {get:0.0} us, new Texture2D {create:0.0} us");
    }
}
