using System;
using System.IO;
using System.Linq;
using BunnyLoader;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsScreen.Effects
{
    public sealed class TestFragEffect : ModScreenEffect
    {
        public override void SetDefaults()
        {
            Radius = 110;
            Color = new Color(180, 90, 255);
            Uniforms["uOpen"] = 1f;
        }
    }
}

namespace CsScreen
{
    public sealed class CountingFlash : ScreenPresets.Flash
    {
        public static int Kills;

        public override void OnKill() => Kills++;
    }

    public sealed class ShaderProbe : ModScreenEffect
    {
        public static ScreenGraphics.Shader? Probe;

        public override void SetDefaults() => Fullscreen = true;

        public override void Draw(ScreenCommands cmd)
        {
            if (Probe != null) cmd.DrawShader(Probe);
        }
    }

    public sealed class CsScreenMod : Mod
    {
        const int End = 2160;
        static int frame, pass, fail;
        static ScreenGraphics.Shader? good, bad;
        static ScreenStats before;
        static ScreenPresets.Twirl? twirl, tweened, follower;
        static ScreenPresets.Tint? keyed;
        static ScreenPresets.Vignette? replaced;
        static CountingFlash? flash;
        static int projectile = -1;
        static ScreenPresets.ColorGrade? grade;
        static CsScreen.Effects.TestFragEffect? frag;

        static ILog Log => ModContent.GetInstance<CsScreenMod>().Logger;

        public override void Load() => On_Player.Update += Update;

        static void Report(string name, bool ok, string detail)
        {
            if (ok) pass++;
            else fail++;
            Log.Info($"csscreen {name}: {(ok ? "ok" : "FAIL")} {detail}");
        }

        static void Probe(ScreenGraphics.Shader shader)
        {
            ShaderProbe.Probe = shader;
            ScreenEffects.Spawn<ShaderProbe>(fx => fx.Duration = 20);
        }

        static void Update(On_Player.orig_Update orig, Player self, int i)
        {
            orig(self, i);
            if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
            frame++;
            Player player = self;
            try { Step(player); }
            catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
            if (frame == End) Log.Info($"csscreen END pass={pass} fail={fail}");
        }

        static void Step(Player player)
        {
            switch (frame)
            {
                case 30:
                {
                    Main.dayTime = true;
                    Main.time = 27000;
                    Type fragType = typeof(CsScreen.Effects.TestFragEffect);
                    Report("the mod effect is registered", ScreenEffects.Classes.Contains(fragType) && ScreenEffects.Classes.Contains(typeof(ScreenPresets.Twirl)), $"{ScreenEffects.Classes.Count} classes");
                    good = ScreenGraphics.Shader.FromSource("void main() {\n    fragColor = blScreen(vUV);\n}\n", "csscreen/good");
                    bad = ScreenGraphics.Shader.FromSource("void main() {\n    fragColor = missingThing;\n}\n", "csscreen/bad");
                    Report("shaders get ids", good.Id > 0 && bad.Id > 0 && good.Id != bad.Id, $"good={good.Id} bad={bad.Id}");
                    Probe(good);
                    before = ScreenEffects.Stats;
                    twirl = ScreenEffects.Spawn<ScreenPresets.Twirl>(player, fx => fx.Radius = 120);
                    Report("Spawn on the player follows it", twirl.Active && twirl.Following && twirl.Center == player.Center, $"{twirl.Center}");
                    break;
                }
                case 60:
                    Log.Info("csscreen SHOT twirl");
                    break;
                case 120:
                {
                    ScreenStats now = ScreenEffects.Stats;
                    Log.Info($"csscreen stats {now}");
                    Report("a shader is pending until it draws", bad!.Status == ScreenGraphics.ShaderStatus.Pending, $"{bad.Status}");
                    Report("the effects draw", now.Passes > before.Passes + 30 && now.HookInstalled, $"passes {before.Passes} -> {now.Passes}");
                    Report("the marker puts them under the interface", now.MarkerReady && now.MarkerHits > before.MarkerHits, $"hits {before.MarkerHits} -> {now.MarkerHits}");
                    Report("a good shader compiles", good!.Status == ScreenGraphics.ShaderStatus.Ready, $"{good.Status}");
                    Report("the twirl moves with the player", twirl!.Center is Vector2 c && Math.Abs(c.X - player.Center.X) < 1, $"{twirl.Center} vs {player.Center}");
                    break;
                }
                case 400:
                {
                    twirl!.Kill();
                    keyed = ScreenEffects.Spawn<ScreenPresets.Tint>(fx =>
                    {
                        fx.Key = "csscreen:key";
                        fx.Color = new Color(255, 0, 0);
                    });
                    ScreenPresets.Tint again = ScreenEffects.Spawn<ScreenPresets.Tint>(fx =>
                    {
                        fx.Key = "csscreen:key";
                        fx.Amount = 0.25f;
                    });
                    Report("the same Key renews the effect", ReferenceEquals(keyed, again) && keyed.Amount == 0.25f && ScreenEffects.Find("csscreen:key") == keyed, $"amount {keyed.Amount}");
                    replaced = ScreenEffects.Spawn<ScreenPresets.Vignette>(fx => fx.Key = "csscreen:key");
                    Report("the same Key with another class replaces it", !keyed.Active && replaced.Active, "");
                    flash = ScreenEffects.Spawn<CountingFlash>();
                    Report("a preset subclass from a mod gets the preset shader", flash.Shader != null, $"{flash.Shader}");
                    tweened = ScreenEffects.Spawn<ScreenPresets.Twirl>(player.Center + new Vector2(-150, 0), fx => fx.Radius = 50);
                    tweened.Tween(nameof(tweened.Radius), 200, 20, ScreenUtils.Easing.OutQuad);
                    projectile = Projectile.NewProjectile(player.GetSource_FromThis(), player.Center + new Vector2(0, -60), Vector2.Zero, ProjectileID.WoodenArrowFriendly, 0, 0, Main.myPlayer);
                    follower = ScreenEffects.Spawn<ScreenPresets.Twirl>(Main.projectile[projectile]);
                    break;
                }
                case 410:
                    Main.projectile[projectile].Kill();
                    break;
                case 460:
                    Report("Duration ends the effect and calls OnKill once", !flash!.Active && CountingFlash.Kills == 1, $"kills={CountingFlash.Kills}");
                    Report("Tween reaches the value", Math.Abs(tweened!.Radius - 200) < 0.01f, $"radius={tweened.Radius}");
                    Report("an effect dies with the entity it follows", !follower!.Active, "");
                    ScreenEffects.KillAll();
                    break;
                case 480:
                    ScreenEffects.Enabled = false;
                    ScreenEffects.Spawn<ScreenPresets.Twirl>(player, fx => fx.Radius = 120);
                    break;
                case 490:
                    before = ScreenEffects.Stats;
                    break;
                case 540:
                {
                    ScreenStats now = ScreenEffects.Stats;
                    Report("turned off, the effects live but do not draw", now.Passes - before.Passes <= 4 && ScreenEffects.Active.Count == 1, $"passes +{now.Passes - before.Passes}");
                    ScreenEffects.Enabled = true;
                    ScreenEffects.KillAll();
                    break;
                }
                case 560:
                    ScreenEffects.Spawn<ScreenPresets.Vignette>(fx => fx.Strength = 0.8f);
                    ScreenEffects.Spawn<ScreenPresets.Lens>(player, fx => fx.Radius = 90);
                    ScreenEffects.Spawn<ScreenPresets.BlackHole>(player.Center + new Vector2(220, -60));
                    ScreenEffects.Spawn<ScreenPresets.HeatHaze>(player.Center + new Vector2(-220, -40), fx => fx.Strength = 4);
                    break;
                case 590:
                    Log.Info("csscreen SHOT blackhole");
                    break;
                case 900:
                    ScreenEffects.KillAll();
                    ScreenEffects.Spawn<ScreenPresets.Bloom>(fx => fx.Threshold = 0.4f);
                    ScreenEffects.Spawn<ScreenPresets.Blur>(player.Center + new Vector2(200, 0), fx => fx.Radius = 140);
                    ScreenEffects.Spawn<ScreenPresets.Echo>(player);
                    break;
                case 910:
                    before = ScreenEffects.Stats;
                    break;
                case 960:
                {
                    ScreenStats now = ScreenEffects.Stats;
                    long frames = Math.Max(now.WorldFrames - before.WorldFrames, 1);
                    Report("off-screen targets add passes", (now.Passes - before.Passes) / (double)frames >= 6, $"{(now.Passes - before.Passes) / (double)frames:0.0} passes/frame");
                    Log.Info("csscreen SHOT bloom");
                    break;
                }
                case 1260:
                {
                    ScreenEffects.KillAll();
                    Mod mod = ModContent.GetInstance<CsScreenMod>();
                    ScreenGraphics.Texture gradient = ScreenGraphics.Texture.Load(mod, "Effects/Gradient");
                    ScreenGraphics.Texture lut = ScreenGraphics.Texture.Load(mod, "Effects/InvertLut.png", nearest: true);
                    Report("a mod PNG loads with its size", gradient.Width == 64 && gradient.Height == 64 && lut.Width == 256 && lut.Height == 16, $"{gradient.Width}x{gradient.Height}, lut {lut.Width}x{lut.Height}");
                    grade = ScreenEffects.Spawn<ScreenPresets.ColorGrade>(fx =>
                    {
                        fx.Lut = lut;
                        fx.Mask = ScreenUtils.Mask.Circle(player.Center, 200, 60);
                    });
                    ScreenEffects.Spawn<ScreenPresets.Tint>(player.Center + new Vector2(-260, 0), fx =>
                    {
                        fx.Fullscreen = false;
                        fx.Color = new Color(0, 255, 120);
                        fx.Amount = 0.8f;
                        fx.Mask = ScreenUtils.Mask.Texture(gradient);
                    });
                    ScreenEffects.Spawn<ScreenPresets.Pixelate>(player.Center + new Vector2(260, -40), fx => fx.Size = 10);
                    break;
                }
                case 1320:
                    Report("ColorGrade with a LUT keeps drawing", grade!.Active && grade.Shader?.Status == ScreenGraphics.ShaderStatus.Ready, $"{grade.Shader?.Status}");
                    Log.Info("csscreen SHOT colorgrade");
                    break;
                case 1620:
                {
                    ScreenEffects.KillAll();
                    string lutFile = Path.Combine(Main.SavePath, "csscreen-lut.png");
                    if (File.Exists(lutFile)) File.Delete(lutFile);
                    ScreenGraphics.Texture.SaveNeutralLut(lutFile, 16);
                    Report("SaveNeutralLut writes the strip", File.Exists(lutFile) && new FileInfo(lutFile).Length > 100, lutFile);
                    frag = ScreenEffects.Spawn<CsScreen.Effects.TestFragEffect>(player.Center + new Vector2(160, -60));
                    Report("the .frag next to the class is its shader", frag.Shader?.Path?.EndsWith("TestFragEffect.frag") == true, frag.Shader?.Path ?? "none");
                    ScreenEffects.Spawn<ScreenPresets.ChromaticAberration>(fx => fx.Amount = 3);
                    break;
                }
                case 1680:
                    Report("the .frag compiles", frag!.Shader!.Status == ScreenGraphics.ShaderStatus.Ready, $"{frag.Shader.Status} {frag.Shader.Error}");
                    Log.Info("csscreen SHOT frag");
                    break;
                case 1980:
                {
                    ScreenEffects.KillAll();
                    Filter heat = Filters.Scene["HeatDistortion"];
                    Report("Filters.Scene has the game's filters", !heat.IsNull && !Filters.Scene["WaterDistortion"].IsNull, "");
                    Filter active = Filters.Scene.Activate("HeatDistortion", player.Center);
                    Report("Filters.Scene.Activate turns a filter on", !active.IsNull && heat.IsActive() && !heat.GetShader().IsNull, "");
                    Filters.Scene.Deactivate("HeatDistortion");
                    Report("Filters.Scene.Deactivate turns it off", !heat.IsActive(), "");
                    player.ManageSpecialBiomeVisuals("HeatDistortion", true, player.Center);
                    Report("ManageSpecialBiomeVisuals turns the filter on", heat.IsActive(), "");
                    player.ManageSpecialBiomeVisuals("HeatDistortion", false);
                    CustomSky sky = SkyManager.Instance["MoonLord"];
                    Report("SkyManager has the game's skies", !sky.IsNull, "");
                    SkyManager.Instance.Activate("MoonLord", player.Center);
                    Report("SkyManager.Activate turns a sky on", sky.IsActive(), "");
                    break;
                }
                case 2040:
                    SkyManager.Instance.Deactivate("MoonLord");
                    Probe(bad!);
                    Report("the filter stays off after ManageSpecialBiomeVisuals(false)", !Filters.Scene["HeatDistortion"].IsActive(), "");
                    break;
                case 2100:
                    Report("a broken shader fails with the error", bad!.Status == ScreenGraphics.ShaderStatus.Failed && bad.Error.Length > 0, $"{bad.Status}: {bad.Error.Split('\n').FirstOrDefault()}");
                    Report("no effect is left", ScreenEffects.Active.Count == 0, $"{ScreenEffects.Active.Count}");
                    break;
            }
        }
    }
}
