using System;
using System.Linq;
using BunnyLoader;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CsExFxc
{
    public sealed class CsExFxcMod : Mod
    {
        const int End = 1100;
        static int frame, pass, fail, boss = -1;
        static float firstIntensity;

        static ILog Log => ModContent.GetInstance<CsExFxcMod>().Logger;

        public override void Load() => On_Player.Update += Update;

        static void Report(string name, bool ok, string detail)
        {
            if (ok) pass++;
            else fail++;
            Log.Info($"csexfxc {name}: {(ok ? "ok" : "FAIL")} {detail}");
        }

        static int NpcType(string name) => ModContent.Find<ModNPC>("ExampleMod/" + name).Type;

        static FxcScreenEffect? Aura() =>
            ScreenEffects.Active.OfType<FxcScreenEffect>().FirstOrDefault(fx => fx.GetType().Name == "ExampleBossAuraEffect");

        static void Update(On_Player.orig_Update orig, Player self, int i)
        {
            orig(self, i);
            if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
            frame++;
            self.creativeGodMode = true;
            self.statLife = self.statLifeMax2;
            try { Step(self); }
            catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
            if (frame == End) Log.Info($"csexfxc END pass={pass} fail={fail}");
        }

        static void Step(Player player)
        {
            switch (frame)
            {
                case 30:
                    Main.dayTime = true;
                    Main.time = 27000;
                    Report("no aura before the boss", Aura() == null, "");
                    boss = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 160, (int)player.Center.Y - 120, NpcType("MinionBossBody"));
                    Report("boss spawned", Main.npc[boss].active, $"index {boss}");
                    break;
                case 150:
                {
                    FxcScreenEffect? aura = Aura();
                    Report("the aura follows the boss", aura != null && aura.Following && aura.Center is Vector2 c && Vector2.Distance(c, Main.npc[boss].Center) < 64, $"{aura?.Center} vs {Main.npc[boss].Center}");
                    Report("the aura draws its .fxc pass", aura?.Shader?.Status == ScreenGraphics.ShaderStatus.Ready && aura.PassName == "BossAura", $"{aura?.Shader?.Status} {aura?.Shader?.Error}");
                    Report("one aura per boss", ScreenEffects.Active.Count(fx => fx.GetType().Name == "ExampleBossAuraEffect") == 1, "");
                    firstIntensity = aura?.Intensity ?? 0;
                    Report("full life gives the lowest intensity", Math.Abs(firstIntensity - 0.35f) < 0.02f, $"{firstIntensity}");
                    Log.Info("csexfxc SHOT aura");
                    break;
                }
                case 300:
                    for (int k = 0; k < Main.maxNPCs; k++)
                        if (Main.npc[k].active && Main.npc[k].type == NpcType("MinionBossMinion")) Main.npc[k].StrikeNPC(99999, 0f, 1);
                    break;
                case 600:
                {
                    NPC npc = Main.npc[boss];
                    npc.dontTakeDamage = false;
                    npc.life = npc.lifeMax / 4;
                    break;
                }
                case 700:
                {
                    FxcScreenEffect? aura = Aura();
                    float now = aura?.Intensity ?? 0;
                    Report("the aura grows as the boss is hurt", now > firstIntensity + 0.3f, $"{firstIntensity} -> {now}, life {Main.npc[boss].life}/{Main.npc[boss].lifeMax}");
                    Report("second stage turns it red", aura?.Color is Microsoft.Xna.Framework.Graphics.Color color && color.R == 255 && color.G == 90, $"{aura?.Color}");
                    Log.Info("csexfxc SHOT aura_hurt");
                    break;
                }
                case 900:
                {
                    NPC npc = Main.npc[boss];
                    npc.dontTakeDamage = false;
                    npc.StrikeNPC(9999999, 0f, 1);
                    break;
                }
                case 1000:
                    Report("the aura ends with the boss", Aura() == null, $"boss active {Main.npc[boss].active}");
                    Log.Info("csexfxc SHOT after");
                    break;
            }
        }
    }
}
