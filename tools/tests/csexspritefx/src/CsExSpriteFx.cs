using System;
using BunnyLoader;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExSpriteFx
{
    public sealed class CsExSpriteFxMod : Mod
    {
        const int End = 1700;
        static int frame, pass, fail, npc = -1;
        static float hold;

        static ILog Log => ModContent.GetInstance<CsExSpriteFxMod>().Logger;

        public override void Load() => On_Player.Update += Update;

        static void Report(string name, bool ok, string detail)
        {
            if (ok) pass++;
            else fail++;
            Log.Info($"csexspritefx {name}: {(ok ? "ok" : "FAIL")} {detail}");
        }

        static void Update(On_Player.orig_Update orig, Player self, int i)
        {
            orig(self, i);
            if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
            frame++;
            self.creativeGodMode = true;
            self.statLife = self.statLifeMax2;

            if (hold > 0 && npc >= 0 && Main.npc[npc].active)
            {
                NPC held = Main.npc[npc];
                held.ai[3] = hold;
                held.Center = self.Center + new Vector2(-105, -40);
                held.rotation = 0f;
                held.velocity = Vector2.Zero;
            }

            try { Step(self); }
            catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
            if (frame == End) Log.Info($"csexspritefx END pass={pass} fail={fail}");
        }

        static void Step(Player player)
        {
            switch (frame)
            {
                case 30:
                {
                    Main.dayTime = true;
                    Main.time = 27000;
                    int dye = ModContent.Find<ModItem>("ExampleMod/ExampleDye").Type;
                    int shader = GameShaders.Armor.GetShaderIdFromItemId(dye);
                    Report("the dye is bound to its .fxc shader", shader > 0, $"shader {shader}");
                    int[] armor = { ItemID.SilverHelmet, ItemID.SilverChainmail, ItemID.SilverGreaves };

                    for (int k = 0; k < 3; k++)
                    {
                        player.armor[k] = new Item(armor[k]);
                        player.dye[k] = new Item(0);
                    }

                    Report("GameShaders.Misc has the death shader", GameShaders.Misc.ContainsKey("ExampleMod:DeathAnimation"), "");
                    break;
                }
                case 150:
                    Log.Info("csexspritefx SHOT nodye");
                    break;
                case 240:
                {
                    int dye = ModContent.Find<ModItem>("ExampleMod/ExampleDye").Type;
                    for (int k = 0; k < 3; k++) player.dye[k] = new Item(dye);
                    break;
                }
                case 300:
                    Report("the player wears the dye", player.cHead > 0 && player.cBody > 0 && player.cLegs > 0, $"{player.cHead} {player.cBody} {player.cLegs}");
                    Log.Info("csexspritefx SHOT dye_1");
                    break;
                case 360:
                    Log.Info("csexspritefx SHOT dye_2");
                    break;
                case 420:
                    Log.Info("csexspritefx SHOT dye_3");
                    break;
                case 480:
                    Log.Info("csexspritefx SHOT dye_4");
                    break;
                case 600:
                {
                    long[] s = FxcEffect.SpriteDrawStats();
                    Report("the dye draws through the .fxc", s[1] > 0 && s[2] == 0 && s[4] == 0, string.Join(" ", s));
                    npc = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 200, (int)player.Center.Y - 100, ModContent.Find<ModNPC>("ExampleMod/ExampleDeathAnimation").Type);
                    Report("the NPC spawned", Main.npc[npc].active, $"index {npc}");
                    break;
                }
                case 800:
                    Log.Info("csexspritefx SHOT npc");
                    break;
                case 1050:
                {
                    NPC target = Main.npc[npc];
                    long before = FxcEffect.SpriteDrawStats()[1];
                    target.StrikeNPC(99999, 0f, 1);
                    Report("the first death starts the animation", target.active && target.ai[3] > 0, $"ai3 {target.ai[3]}");
                    hold = 50;
                    Log.Info($"csexspritefx draws before dissolving {before}");
                    break;
                }
                case 1100:
                    Log.Info($"csexspritefx SHOT dissolve_1 ai3={Main.npc[npc].ai[3]}");
                    break;
                case 1150:
                    hold = 100;
                    break;
                case 1200:
                    Log.Info($"csexspritefx SHOT dissolve_2 ai3={Main.npc[npc].ai[3]}");
                    break;
                case 1250:
                    hold = 140;
                    break;
                case 1300:
                    Log.Info($"csexspritefx SHOT dissolve_3 ai3={Main.npc[npc].ai[3]}");
                    break;
                case 1400:
                {
                    long[] s = FxcEffect.SpriteDrawStats();
                    Report("the dissolve draws through the .fxc", s[1] > 0 && s[2] == 0 && s[4] == 0, string.Join(" ", s));
                    hold = 0;
                    break;
                }
                case 1650:
                    Report("the NPC dies after the animation", !Main.npc[npc].active || Main.npc[npc].type != ModContent.Find<ModNPC>("ExampleMod/ExampleDeathAnimation").Type, "");
                    break;
            }
        }
    }
}
