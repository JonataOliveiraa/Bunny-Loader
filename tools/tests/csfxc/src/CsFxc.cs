using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BunnyLoader;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CsFxc
{
    public sealed class FxcShockwave : FxcScreenEffect
    {
        public override void SetDefaults()
        {
            Effect = CsFxcMod.Shockwave;
            PassName = "Shockwave";
            Duration = 600;
            Color = new Color(255, 210, 150);
            SecondaryColor = new Vector3(0.2f, 0.9f, 1f);
            ShaderOpacity = 0.7f;
            Intensity = 1.5f;
        }
    }

    public sealed class FxcStripes : FxcScreenEffect
    {
        public override void SetDefaults()
        {
            Effect = CsFxcMod.Shockwave;
            PassName = "Stripes";
            Color = new Color(255, 80, 80);
            SecondaryColor = new Vector3(0.3f, 0.5f, 1f);
            ShaderOpacity = 0.8f;
        }
    }

    public sealed class FxcExampleDye : FxcScreenEffect
    {
        public override void SetDefaults()
        {
            Effect = CsFxcMod.ExampleDye;
            PassName = "ExampleDyePass";
        }
    }

    public sealed class FxcProbe : FxcScreenEffect
    {
    }

    public sealed class CsFxcMod : Mod
    {
        const int End = 1800;
        static int frame, pass, fail;
        static FxcShockwave? shockwave;
        static FxcStripes? stripes;
        static FxcExampleDye? dye;

        internal static FxcEffect? Shockwave, ExampleDye;

        static ILog Log => ModContent.GetInstance<CsFxcMod>().Logger;

        public override void Load()
        {
            Shockwave = FxcEffect.Load(this, "Effects/BLShockwave");
            ExampleDye = FxcEffect.Load(this, "Effects/ExampleEffect");
            On_Player.Update += Update;
        }

        static void Report(string name, bool ok, string detail)
        {
            if (ok) pass++;
            else fail++;
            Log.Info($"csfxc {name}: {(ok ? "ok" : "FAIL")} {detail}");
        }

        static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;

        static void Update(On_Player.orig_Update orig, Player self, int i)
        {
            orig(self, i);
            if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
            frame++;
            try { Step(self); }
            catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
            if (frame == End) Log.Info($"csfxc END pass={pass} fail={fail}");
        }

        static void Step(Player player)
        {
            switch (frame)
            {
                case 30:
                {
                    Main.dayTime = true;
                    Main.time = 27000;
                    FxcEffect fx = Shockwave!;
                    Report("the effect lists its passes", string.Join(",", fx.Passes.Select(p => p.Name)) == "Shockwave,Stripes", string.Join(",", fx.Passes));
                    Report("the effect lists its parameters", fx.Has("uColor") && fx.Has("uScreenResolution") && fx.Has("uImage0") && fx.Parameters.Count == 17, $"{fx.Parameters.Count}");
                    Report("the pass names its sampler", fx.FindPass("Shockwave").Samplers.TryGetValue(0, out string? s0) && s0 == "uImage0", "");
                    string source = fx.Source("Shockwave");
                    Report("the pass converts to the screen shader format", source.Contains("void blFxcMain()") && source.Contains("#define ps_s0 uScreen") && !source.Contains("#version"), $"{source.Length} chars");
                    fx.SetValue("uScreenResolution", new Vector2(1000, 500))
                        .SetValue("uScreenPosition", new Vector2(100, 200))
                        .SetValue("uTargetPosition", new Vector2(600, 450))
                        .SetValue("uProgress", 0.5f)
                        .SetValue("uColor", new Vector3(1, 0.5f, 0.25f));
                    float[] r = fx.Commit("Shockwave").Values;
                    Report("the preshader computes the registers", r.Length == 32 && Near(r[0], 0.5f) && Near(r[1], 0.5f) && Near(r[4], 2) && Near(r[8], 0.3f) && Near(r[12], 0.5f) && Near(r[17], 0.5f),
                        string.Join(" ", r.Take(20).Select(v => v.ToString("0.###"))));
                    fx.SetValue("uTime", 5.5f);
                    float[] stripes = fx.Commit("Stripes").Values;
                    Report("a ps_3_0 pass runs its own preshader", stripes.Length == 16 && Near(stripes[0], 33), string.Join(" ", stripes.Take(8).Select(v => v.ToString("0.###"))));
                    bool threw = false;
                    try { FxcEffect.FromBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, "garbage"); }
                    catch (InvalidDataException) { threw = true; }
                    Report("garbage is refused", threw, "");
                    byte[] truncated = { 0x01, 0x09, 0xFF, 0xFE, 0xBC, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    string cut = "";
                    try { FxcEffect.FromBytes(truncated, "cut"); }
                    catch (InvalidDataException e) { cut = e.Message; }
                    Report("a cut effect is refused", cut.Contains("Unexpected EOF"), cut);
                    Mod mod = ModContent.GetInstance<CsFxcMod>();
                    Report("Load returns the same effect for the same file", ReferenceEquals(FxcEffect.Load(mod, "Effects/BLShockwave"), fx), "");
                    var probe = new FxcProbe { Effect = fx };
                    string bad = "";
                    try { probe.PassName = "Nope"; }
                    catch (KeyNotFoundException e) { bad = e.Message; }
                    Report("a wrong pass name fails when it is set", bad.Contains("Shockwave"), bad);
                    probe.PassName = "Stripes";
                    Report("setting the pass resolves its shader", probe.Shader != null && ReferenceEquals(probe.Shader, fx.Shader("Stripes")), $"{probe.Shader}");
                    FxcEffect arrays = FxcEffect.Load(mod, "Effects/BLArrays");
                    arrays.SetValue("uPoints", new[] { new Vector2(1, 2), new Vector2(3, 4), new Vector2(5, 6) }).SetValue("uTint", new Vector3(7, 8, 9));
                    float[] a = arrays.Commit("Points").Values;
                    Report("float2 arrays are padded to registers", a.Length == 16 && a[0] == 1 && a[1] == 2 && a[4] == 3 && a[5] == 4 && a[8] == 5 && a[9] == 6 && a[12] == 7 && a[14] == 9,
                        string.Join(" ", a.Select(v => v.ToString("0.#"))));
                    long sanity = GC.GetAllocatedBytesForCurrentThread();
                    byte[] probeBytes = new byte[4096];
                    sanity = GC.GetAllocatedBytesForCurrentThread() - sanity;
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int k = 0; k < 200; k++)
                        fx.SetValue("uTime", (float)k).SetValue("uTargetPosition", new Vector2(k, k)).SetValue("uColor", new Vector3(1, 1, 1));
                    long used = GC.GetAllocatedBytesForCurrentThread() - before;
                    Report("SetValue does not allocate", sanity >= 4096 && used < 1024, $"{used} bytes for 600 calls (counter saw {sanity} for {probeBytes.Length})");
                    break;
                }
                case 60:
                    Log.Info("csfxc SHOT control");
                    break;
                case 200:
                    shockwave = ScreenEffects.Spawn<FxcShockwave>(player);
                    break;
                case 300:
                {
                    Vector2 size = ScreenShaderData.UnscaledScreenSize, position = ScreenShaderData.UnscaledScreenPosition;
                    Vector2 space = ScreenUtils.Space.Size;
                    Report("the game's unscaled screen matches the effect space", Vector2.Distance(size, space) < 1 && Vector2.Distance(position, Main.screenPosition) < 1,
                        $"size {size} vs {space}, position {position} vs {Main.screenPosition}, zoom {Main.GameViewMatrix.RenderZoom}");
                    FxcEffect fx = Shockwave!;
                    int version = fx.Version;
                    System.Threading.Thread.Sleep(1100);
                    File.WriteAllBytes(fx.Path!, File.ReadAllBytes(fx.Path!));
                    int changed = ScreenEffects.Reload();
                    Report("ScreenEffects.Reload reloads a changed .fxc", changed >= 1 && fx.Version == version + 1 && fx.Passes.Count == 2, $"changed {changed}, version {version} -> {fx.Version}");
                    break;
                }
                case 420:
                    Log.Info($"csfxc SHOT shockwave progress={shockwave!.Progress:0.00}");
                    break;
                case 500:
                    Log.Info($"csfxc SHOT shockwave_late progress={shockwave!.Progress:0.00}");
                    break;
                case 560:
                    Report("the shockwave shader compiled", shockwave!.Shader?.Status == ScreenGraphics.ShaderStatus.Ready, $"{shockwave.Shader?.Status} {shockwave.Shader?.Error}");
                    shockwave.Kill();
                    shockwave = ScreenEffects.Spawn<FxcShockwave>(player.Center - new Vector2(0, 150), fx => fx.Age = 200);
                    break;
                case 700:
                    Log.Info($"csfxc SHOT shockwave_up center={shockwave!.Center} player={player.Center}");
                    break;
                case 760:
                    shockwave!.Kill();
                    stripes = ScreenEffects.Spawn<FxcStripes>();
                    break;
                case 800:
                    Log.Info("csfxc SHOT stripes");
                    break;
                case 1000:
                    Report("the ps_3_0 shader compiled", stripes!.Shader?.Status == ScreenGraphics.ShaderStatus.Ready, $"{stripes.Shader?.Status} {stripes.Shader?.Error}");
                    stripes.Kill();
                    dye = ScreenEffects.Spawn<FxcExampleDye>();
                    break;
                case 1240:
                    Log.Info("csfxc SHOT exampledye");
                    break;
                case 1300:
                    Log.Info($"csfxc SHOT exampledye_next time={Main.GlobalTimeWrappedHourly:0.00}");
                    break;
                case 1440:
                    Report("the tModLoader ExampleEffect compiled", dye!.Shader?.Status == ScreenGraphics.ShaderStatus.Ready, $"{dye.Shader?.Status} {dye.Shader?.Error}");
                    dye.Kill();
                    break;
                case 1600:
                    Log.Info("csfxc SHOT after");
                    ScreenStats stats = ScreenEffects.Stats;
                    Log.Info($"csfxc stats {stats}");
                    break;
            }
        }
    }
}
