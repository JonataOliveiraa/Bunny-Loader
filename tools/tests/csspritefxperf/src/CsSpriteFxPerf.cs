using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BunnyLoader;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace CsSpriteFxPerf
{
    public sealed class PerfSystem : ModSystem
    {
        public override void PostDrawInterface(SpriteBatch spriteBatch) => CsSpriteFxPerfMod.Draw();
    }

    public sealed class CsSpriteFxPerfMod : Mod
    {
        enum Mode { Idle, CtrlDeferred, FxDeferred, CtrlImmediate, FxImmediate, FxLayers }

        sealed record Phase(string Name, Mode Mode, int Count);

        const int PhaseFrames = 540;
        const int Skip = 90;

        static readonly Phase[] Phases =
        {
            new("warmup", Mode.Idle, 0),
            new("idle_nohooks", Mode.Idle, 0),
            new("idle_hooks", Mode.Idle, 0),
            new("ctrl_deferred", Mode.CtrlDeferred, 200),
            new("fx_deferred", Mode.FxDeferred, 200),
            new("ctrl_immediate", Mode.CtrlImmediate, 200),
            new("fx_immediate", Mode.FxImmediate, 200),
            new("fx_layers", Mode.FxLayers, 200),
            new("fx_immediate_2", Mode.FxImmediate, 200),
            new("ctrl_immediate_2", Mode.CtrlImmediate, 200),
            new("fx_deferred_2", Mode.FxDeferred, 200),
            new("ctrl_deferred_2", Mode.CtrlDeferred, 200),
            new("idle_hooks_2", Mode.Idle, 0),
        };

        static int frame, phaseIndex = -1, phaseFrame, pass, fail;
        static Effect effect;
        static bool loaded;
        static Asset<Texture2D>? square, gradientH, gradientV;
        static long lastTick;
        static readonly List<double> frameMs = new(), drawMs = new();
        static long[] statsAtStart = Array.Empty<long>();

        static ILog Log => ModContent.GetInstance<CsSpriteFxPerfMod>().Logger;

        public override void Load()
        {
            square = Assets.Request<Texture2D>("Content/Square");
            gradientH = Assets.Request<Texture2D>("Content/Gradient");
            gradientV = Assets.Request<Texture2D>("Content/GradientV");
            On_Player.Update += Update;
        }

        static void Update(On_Player.orig_Update orig, Player self, int i)
        {
            orig(self, i);
            if (Main.gameMenu || i != Main.myPlayer || phaseIndex >= Phases.Length) return;
            frame++;

            if (frame == 30)
            {
                Main.dayTime = true;
                Main.time = 27000;
            }

            if (frame < 60) return;
            phaseFrame++;

            if (phaseIndex < 0 || phaseFrame >= PhaseFrames)
            {
                if (phaseIndex >= 0) Finish(Phases[phaseIndex]);
                phaseIndex++;
                phaseFrame = 0;
                frameMs.Clear();
                drawMs.Clear();

                if (phaseIndex >= Phases.Length)
                {
                    Log.Info($"csspritefxperf END pass={pass} fail={fail}");
                    return;
                }

                if (Phases[phaseIndex].Name == "idle_hooks") LoadEffect();
            }

            if (phaseFrame == Skip && loaded) statsAtStart = FxcEffect.SpriteDrawStats();
        }

        static void LoadEffect()
        {
            Mod mod = ModContent.GetInstance<CsSpriteFxPerfMod>();
            effect = mod.Assets.Request<Effect>("Effects/SpriteFx").Value;
            loaded = true;
            pass++;
            Log.Info("csspritefxperf effect loaded, sprite hooks installed");
        }

        static double Mean(List<double> v) => v.Count == 0 ? 0 : v.Average();

        static double P95(List<double> v)
        {
            if (v.Count == 0) return 0;
            var sorted = v.OrderBy(x => x).ToList();
            return sorted[(int)(sorted.Count * 0.95)];
        }

        static void Finish(Phase phase)
        {
            if (phase.Name == "warmup") return;
            int frames = frameMs.Count;
            string native = "";

            if (loaded && statsAtStart.Length > 0)
            {
                long[] now = FxcEffect.SpriteDrawStats();
                long D(int k) => k < now.Length && k < statsAtStart.Length ? now[k] - statsAtStart[k] : 0;
                double perFrame(long v) => frames == 0 ? 0 : (double)v / frames;
                long records = D(0), hits = D(1), checks = D(13);
                native = $" marked/frame={perFrame(hits):0.0} records/frame={perFrame(records):0.0}"
                    + $" apply_us/record={(records == 0 ? 0 : D(9) / 1000.0 / records):0.00}"
                    + $" material_us/frame={perFrame(D(10)) / 1000.0:0.00}"
                    + $" draw_us/marked={(hits == 0 ? 0 : D(11) / 1000.0 / hits):0.00}"
                    + $" checks/frame={perFrame(checks):0.0} check_us/frame={perFrame(D(12)) / 1000.0:0.00}"
                    + $" check_ns/check={(checks == 0 ? 0 : (double)D(12) / checks):0}"
                    + $" lost={D(2)} link_errors={D(4)}";
            }

            Log.Info($"csspritefxperf PHASE {phase.Name} frames={frames} frame_ms={Mean(frameMs):0.00} p95={P95(frameMs):0.00} draw_ms={Mean(drawMs):0.000} draw_p95={P95(drawMs):0.000}{native}");

            if (loaded && statsAtStart.Length > 0)
            {
                long[] now = FxcEffect.SpriteDrawStats();
                if (now[2] - statsAtStart[2] != 0) { fail++; Log.Info($"csspritefxperf {phase.Name}: FAIL marked draws were lost"); }
                else pass++;
            }
        }

        static Vector2 Spot(int k)
        {
            int columns = 20;
            return new Vector2(40 + k % columns * 24, 40 + k / columns * 22);
        }

        internal static void Draw()
        {
            if (phaseIndex < 0 || phaseIndex >= Phases.Length || square == null) return;
            long tick = Stopwatch.GetTimestamp();
            if (lastTick != 0 && phaseFrame >= Skip) frameMs.Add((tick - lastTick) * 1000.0 / Stopwatch.Frequency);
            lastTick = tick;
            Phase phase = Phases[phaseIndex];
            if (phase.Mode == Mode.Idle) return;

            long started = Stopwatch.GetTimestamp();
            SpriteBatch sb = Main.spriteBatch;
            Matrix matrix = Main.UIScaleMatrix;
            Texture2D texture = square.Value;
            sb.End();

            switch (phase.Mode)
            {
                case Mode.CtrlDeferred:
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, matrix);
                    for (int k = 0; k < phase.Count; k++) sb.Draw(texture, Spot(k), Color.White);
                    sb.End();
                    break;
                case Mode.FxDeferred:
                    effect.Parameters["uColor"].SetValue(new Vector3(0, 1, 0));
                    effect.Parameters["uOpacity"].SetValue(1f);
                    effect.CurrentTechnique = effect.Techniques[0];
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, effect, matrix);
                    for (int k = 0; k < phase.Count; k++) sb.Draw(texture, Spot(k), Color.White);
                    sb.End();
                    break;
                case Mode.CtrlImmediate:
                    sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, matrix);
                    for (int k = 0; k < phase.Count; k++) sb.Draw(texture, Spot(k), Color.White);
                    sb.End();
                    break;
                case Mode.FxImmediate:
                {
                    sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, matrix);
                    EffectPass tint = effect.CurrentTechnique.Passes["Tint"];
                    EffectParameter color = effect.Parameters["uColor"];
                    effect.Parameters["uOpacity"].SetValue(1f);

                    for (int k = 0; k < phase.Count; k++)
                    {
                        color.SetValue(new Vector3(k / (float)phase.Count, 1 - k / (float)phase.Count, 0.5f));
                        tint.Apply();
                        sb.Draw(texture, Spot(k), Color.White);
                    }

                    sb.End();
                    break;
                }
                case Mode.FxLayers:
                {
                    sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, matrix);
                    effect.Parameters["uImage1"].SetValue(gradientH!.Value);
                    effect.Parameters["uImage2"].SetValue(gradientV!.Value);
                    effect.Parameters["uImage3"].SetValue(texture);
                    EffectPass layers = effect.CurrentTechnique.Passes["Layers"];

                    for (int k = 0; k < phase.Count; k++)
                    {
                        layers.Apply();
                        sb.Draw(texture, Spot(k), Color.White);
                    }

                    sb.End();
                    effect.Parameters["uImage1"].SetValue(default(Texture2D));
                    effect.Parameters["uImage2"].SetValue(default(Texture2D));
                    effect.Parameters["uImage3"].SetValue(default(Texture2D));
                    break;
                }
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, matrix);
            if (phaseFrame >= Skip) drawMs.Add((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
        }
    }
}
