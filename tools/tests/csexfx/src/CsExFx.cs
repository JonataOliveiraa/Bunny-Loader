using System;
using System.Linq;
using BunnyLoader;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CsExFx;

public sealed class CsExFxMod : Mod
{
    const int End = 1000;
    static int frame, pass, fail;
    static bool channel;
    static int holdout = -1, rift = -1;

    static ILog Log => ModContent.GetInstance<CsExFxMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexfx {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static bool Live(string name) => ScreenEffects.Active.Any(fx => fx.GetType().Name == name);

    static string Names() => string.Join(",", ScreenEffects.Active.Select(fx => fx.GetType().Name));

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
        frame++;
        try { Step(self); }
        catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
        if (channel) self.channel = true;
        if (frame == End) Log.Info($"csexfx END pass={pass} fail={fail}");
    }

    static void Step(Player player)
    {
        switch (frame)
        {
            case 20:
                Log.Info("csexfx SHOT rift");
                break;
            case 30:
            {
                Main.dayTime = true;
                Main.time = 27000;
                Report("the Example Mod is loaded", ModLoader.TryGetMod("ExampleMod", out _), "");
                int type = ModContent.Find<ModProjectile>("ExampleMod/ExampleRiftProjectile").Type;
                rift = Projectile.NewProjectile(player.GetSource_FromThis(), player.Center + new Vector2(160, -40), new Vector2(0.5f, 0), type, 0, 0, Main.myPlayer);
                break;
            }
            case 70:
                Report("the rift projectile opens its screen effect", Live("ExampleRiftEffect"), Names());
                break;
            case 140:
                Main.projectile[rift].Kill();
                break;
            case 146:
                Report("the rift closes into a shockwave, a flash and chromatic aberration", Live("Shockwave") && Live("Flash") && Live("ChromaticAberration"), Names());
                Log.Info("csexfx SHOT boom");
                break;
            case 300:
            {
                ModPlayer lowLife = player.GetModPlayer(ModContent.Find<ModPlayer>("ExampleMod/ExampleLowLifePlayer"));
                int life = player.statLife;
                player.statLife = 10;
                lowLife.PostUpdate();
                player.statLife = life;
                Report("low life tints the screen", ScreenEffects.Find("ExampleMod:lowLife") is ScreenPresets.Tint, Names());
                break;
            }
            case 400:
                Report("healed, the low life tint fades and dies", ScreenEffects.Find("ExampleMod:lowLife") == null, Names());
                break;
            case 420:
            {
                int type = ModContent.Find<ModProjectile>("ExampleMod/ExampleLastPrismHoldout").Type;
                player.inventory[player.selectedItem].SetDefaults(ModContent.Find<ModItem>("ExampleMod/ExampleLastPrism").Type, default);
                channel = true;
                holdout = Projectile.NewProjectile(player.GetSource_FromThis(), player.Center, new Vector2(30, 0), type, 42, 0, Main.myPlayer);
                break;
            }
            case 560:
                Log.Info("csexfx SHOT prism");
                break;
            case 640:
            {
                int beam = ModContent.Find<ModProjectile>("ExampleMod/ExampleLastPrismBeam").Type;
                int beams = 0;
                for (int k = 0; k < Main.maxProjectiles; k++)
                    if (Main.projectile[k].active && Main.projectile[k].type == beam) beams++;
                Report("the Last Prism fires its ten beams", beams == 10, $"beams={beams}");
                Report("the charged beam ripples the screen", Live("Ripple"), Names());
                break;
            }
            case 860:
                channel = false;
                break;
            case 920:
                Report("letting go ends the prism and its ripple", !Main.projectile[holdout].active && !Live("Ripple"), Names());
                break;
        }
    }
}
