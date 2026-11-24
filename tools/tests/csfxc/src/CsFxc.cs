using System;
using System.IO;
using System.Linq;
using BunnyLoader;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
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
                    break;
                }
                case 60:
                    Log.Info("csfxc SHOT control");
                    break;
                case 200:
                    shockwave = ScreenEffects.Spawn<FxcShockwave>(player);
                    break;
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
