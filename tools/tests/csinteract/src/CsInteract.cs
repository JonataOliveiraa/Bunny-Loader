using System;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsInteract;

public sealed class CsInteractMod : Mod
{
    static int frame, pass, fail, step, waited, taps, proj = -1;
    static bool interacted;

    static ILog Log => ModContent.GetInstance<CsInteractMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.DrawProjDirect += DrawProjDirect;
    }

    static void DrawProjDirect(On_Main.orig_DrawProjDirect orig, Main self, Projectile projectile, Player overridePlayer)
    {
        Player player = Main.LocalPlayer;
        bool watched = projectile.whoAmI == proj && step == 2 && !player.tileInteractionHappened;
        orig(self, projectile, overridePlayer);
        if (watched && player.tileInteractionHappened)
            interacted = true;
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csinteract {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        self.creativeGodMode = true;
        frame++;
        try
        {
            Step(self);
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            Finish();
        }
    }

    static Vector2 ScreenFraction(Vector2 world)
    {
        var size = new Vector2(Main.screenWidth, Main.screenHeight);
        Vector2 zoom = Main.GameViewMatrix.Zoom;
        Vector2 screen = (world - Main.screenPosition - size / 2f) * zoom + size / 2f;
        return screen / size;
    }

    static void Step(Player self)
    {
        switch (step)
        {
            case 0 when frame == 30:
                Main.playerInventory = false;
                proj = Projectile.NewProjectile(self.GetSource_FromThis(), self.Center + new Vector2(70, -30), Vector2.Zero, ModContent.Find<ModProjectile>("ExampleMod/ExampleInteractableProjectile").Type, 0, 0, Main.myPlayer);
                waited = frame;
                step = 1;
                break;
            case 1 when frame == waited + 60:
            {
                Projectile target = Main.projectile[proj];
                Vector2 at = ScreenFraction(target.Center);
                Log.Info($"csinteract SHOT {++taps}");
                Log.Info($"csinteract FTAP {++taps} {at.X:0.0000} {at.Y:0.0000}");
                waited = frame;
                step = 2;
                break;
            }
            case 2:
                if (interacted || frame > waited + 900)
                {
                    Report("a real tap on the interactable mod projectile interacts with it", interacted);
                    Main.projectile[proj].Kill();
                    interacted = false;
                    proj = Projectile.NewProjectile(self.GetSource_FromThis(), self.Center + new Vector2(70, -30), Vector2.Zero, ProjectileID.FlyingPiggyBank, 0, 0, Main.myPlayer);
                    waited = frame;
                    step = 3;
                }
                break;
            case 3 when frame == waited + 60:
            {
                Vector2 at = ScreenFraction(Main.projectile[proj].Center);
                Log.Info($"csinteract FTAP {++taps} {at.X:0.0000} {at.Y:0.0000}");
                waited = frame;
                step = 4;
                break;
            }
            case 4:
                if (Main.LocalPlayer.chest != -1 || frame > waited + 900)
                {
                    Report("a real tap still opens the vanilla piggy bank", Main.LocalPlayer.chest != -1, $"chest={Main.LocalPlayer.chest}");
                    Main.LocalPlayer.chest = -1;
                    Main.projectile[proj].Kill();
                    Finish();
                }
                break;
        }
    }

    static void Finish()
    {
        step = 99;
        Log.Info($"csinteract END pass={pass} fail={fail}");
    }
}
