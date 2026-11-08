using System;
using System.Linq;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsDualUse;

public sealed class CsDualUseMod : Mod
{
    const float StickX = 869f, StickY = 485f, InteractX = 69f, InteractY = 420f;
    static int frame, pass, fail, step, waited, taps, slot, tries;
    static bool bees, thrust, swing, altUse, used, visualThrust, visualSwing;
    static int firstVisual, animFrames;
    static bool tapMode;
    static Item kept;
    static VirtualControlsProfile_Layout profile;

    static ILog Log => ModContent.GetInstance<CsDualUseMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csdualuse {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void SetMode(bool on)
    {
        VirtualControllerInputState input = GUIInstance.Active.VirtualControllerInputState;
        input.ToggleAlternativeFireMode = on;
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
            Watch(self);
            Step(self);
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            Finish(self);
        }
    }

    static void Watch(Player self)
    {
        if (step is not (2 or 4 or 6)) return;
        if (self.itemAnimation > 0)
        {
            used = true;
            Item visual = self.lastVisualizedSelectedItem;
            int shown = visual.IsNull ? -1 : visual.useStyle;
            if (animFrames++ < 6)
                Log.Info($"csdualuse   frame {animFrames} step={step} anim={self.itemAnimation}/{self.itemAnimationMax} time={self.itemTime}/{self.itemTimeMax} shown={shown} held={self.HeldItem.useStyle} alt={self.altFunctionUse}");
            if (firstVisual == 0) firstVisual = shown;
            visualThrust |= shown == ItemUseStyleID.Thrust;
            visualSwing |= shown == ItemUseStyleID.Swing;
            int style = self.HeldItem.useStyle;
            thrust |= style == ItemUseStyleID.Thrust;
            swing |= style == ItemUseStyleID.Swing;
            altUse |= self.altFunctionUse == 2;
        }
        bees |= Main.projectile.AsSpan().ToArray().Any(p => !p.IsNull && p.active && p.type == ProjectileID.Bee && p.owner == Main.myPlayer);
    }

    static void Reset()
    {
        bees = thrust = swing = altUse = used = visualThrust = visualSwing = false;
        firstVisual = 0;
        animFrames = 0;
        tries = 0;
        foreach (Projectile p in Main.projectile.AsSpan().ToArray())
            if (!p.IsNull && p.active && p.type == ProjectileID.Bee) p.Kill();
    }

    static void Fire()
    {
        if (tapMode) Log.Info($"csdualuse FHOLD {++taps} 0.62 0.45");
        else Log.Info($"csdualuse DRAG {++taps} {StickX} {StickY} {StickX + 54} {StickY}");
        waited = frame;
    }

    static bool Mode() => GUIInstance.Active.VirtualControllerInputState.ToggleAlternativeFireMode;

    static void Step(Player self)
    {
        switch (step)
        {
            case 0 when frame == 30:
            {
                Main.playerInventory = false;
                slot = self.selectedItem;
                kept = self.inventory[slot].Clone();
                VirtualControlsStyles_Layout styles = VirtualControlsStyles_Layout.Instance;
                profile = styles.ActiveProfile;
                foreach (VirtualControlsProfile_Layout candidate in styles.Profiles.AsSpan().ToArray())
                    if (!candidate.IsNull && candidate.Id == "Advanced") styles.SetActiveProfile(candidate);
                self.inventory[slot] = new Item(ModContent.Find<ModItem>("ExampleMod/ExampleDualUseWeapon").Type);
                Report("the weapon has an alternative use", self.CanAlternativeFire(true));
                waited = frame;
                step = 1;
                break;
            }
            case 1 when frame == waited + 60:
                SetMode(true);
                Reset();
                Fire();
                step = 2;
                break;
            case 2 or 4 when !used && frame == waited + 300 && tries < 3:
                tries++;
                Fire();
                break;
            case 2 when frame == waited + 420:
                Report("the alternative use thrusts and shoots bees", altUse && thrust && bees, $"alt={altUse} thrust={thrust} swing={swing} bees={bees}");
                Report("the alternative use is drawn as a thrust", visualThrust && !visualSwing, $"first={firstVisual} shownThrust={visualThrust} shownSwing={visualSwing}");
                SetMode(false);
                Reset();
                waited = frame;
                step = 3;
                break;
            case 3 when frame == waited + 60:
                Reset();
                Fire();
                step = 4;
                break;
            case 4 when frame == waited + 420:
                Report("the normal use swings without bees", !altUse && swing && !thrust && !bees, $"alt={altUse} thrust={thrust} swing={swing} bees={bees}");
                Report($"{(tapMode ? "with taps, " : "")}right after the alternative use, the first normal swing is drawn as a swing", visualSwing && !visualThrust && firstVisual == ItemUseStyleID.Swing, $"first={firstVisual} shownThrust={visualThrust} shownSwing={visualSwing}");
                if (tapMode)
                {
                    Finish(self);
                    break;
                }
                tapMode = true;
                SetMode(false);
                Log.Info($"csdualuse TAP {++taps} {InteractX} {InteractY}");
                waited = frame;
                step = 5;
                break;
            case 5 when Mode() || frame == waited + 600:
                Report("tapping Interact turns the alternative mode on", Mode(), $"frames={frame - waited}");
                Reset();
                Fire();
                step = 6;
                break;
            case 6 when frame == waited + 300:
                Report("with taps, the alternative use thrusts and shoots bees", altUse && thrust && bees, $"alt={altUse} thrust={thrust} swing={swing} bees={bees} first={firstVisual}");
                Log.Info($"csdualuse TAP {++taps} {InteractX} {InteractY}");
                waited = frame;
                step = 7;
                break;
            case 7 when !Mode() || frame == waited + 600:
                Report("tapping Interact turns the alternative mode off", !Mode(), $"frames={frame - waited}");
                Reset();
                Fire();
                step = 4;
                break;
        }
    }

    static void Finish(Player self)
    {
        if (step != 99 && kept != null)
        {
            self.inventory[slot] = kept;
            SetMode(false);
            if (!profile.IsNull) VirtualControlsStyles_Layout.Instance.SetActiveProfile(profile);
        }
        step = 99;
        Log.Info($"csdualuse END pass={pass} fail={fail}");
    }
}
