using System;
using System.Linq;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsDualUse;

public sealed class CsDualUseMod : Mod
{
    const float StickX = 869f, StickY = 485f;
    static int frame, pass, fail, step, waited, taps, slot, tries;
    static bool bees, thrust, swing, altUse, used;
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
        if (step is not (2 or 4)) return;
        if (self.itemAnimation > 0)
        {
            used = true;
            int style = self.HeldItem.useStyle;
            thrust |= style == ItemUseStyleID.Thrust;
            swing |= style == ItemUseStyleID.Swing;
            altUse |= self.altFunctionUse == 2;
        }
        bees |= Main.projectile.AsSpan().ToArray().Any(p => !p.IsNull && p.active && p.type == ProjectileID.Bee && p.owner == Main.myPlayer);
    }

    static void Reset()
    {
        bees = thrust = swing = altUse = used = false;
        tries = 0;
        foreach (Projectile p in Main.projectile.AsSpan().ToArray())
            if (!p.IsNull && p.active && p.type == ProjectileID.Bee) p.Kill();
    }

    static void Fire()
    {
        Log.Info($"csdualuse DRAG {++taps} {StickX} {StickY} {StickX + 54} {StickY}");
        waited = frame;
    }

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
                Finish(self);
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
