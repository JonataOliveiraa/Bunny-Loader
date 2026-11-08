using System;
using System.Linq;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsAltFire;

public class AltWand : ModItem
{
    public static int AltUses, NormalUses;

    public override string Texture => $"Terraria/Images/Item_{ItemID.WandofSparking}";

    public override void SetDefaults()
    {
        Item.width = Item.height = 28;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = Item.useAnimation = 20;
        Item.damage = 10;
        Item.knockBack = 2f;
        Item.DamageType = DamageClass.Melee;
    }

    public override bool AltFunctionUse(Player player) => true;

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer)
        {
            if (player.altFunctionUse == 2) AltUses++;
            else NormalUses++;
        }
        return true;
    }
}

public sealed class CsAltFireMod : Mod
{
    const float StickX = 869f, StickY = 485f;
    static int frame, pass, fail, step, waited, taps, slot, alt, normal, vanillaAlt;
    static Item kept;
    static VirtualControlsProfile_Layout profile;

    static ILog Log => ModContent.GetInstance<CsAltFireMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csaltfire {name}: {(ok ? "ok" : "FAIL")} {detail}");
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
        if (step == 11 && self.altFunctionUse != 0)
            vanillaAlt++;
        try
        {
            Step(self);
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            Finish(self);
        }
    }

    static void Hold(Player self, int type)
    {
        self.inventory[slot] = new Item(type);
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
                Hold(self, ItemID.MonkStaffT3);
                step = 10;
                break;
            }
            case 10 when frame == waited + 300:
                SetMode(true);
                Fire();
                step = 12;
                break;
            case 12 when frame == waited + 300:
                vanillaAlt = 0;
                Log.Info($"csaltfire SHOT {++taps}");
                Report("the game counts its own alternative fire item", self.CanAlternativeFire(true));
                Fire();
                step = 11;
                break;
            case 11 when frame == waited + 300:
                Report("the game's own item fires its alternative with the aim stick", vanillaAlt > 0, $"frames={vanillaAlt}");
                Hold(self, ModContent.ItemType<AltWand>());
                step = 2;
                break;
            case 2 when frame == waited + 30:
                SetMode(true);
                waited = frame;
                step = 20;
                break;
            case 20 when frame == waited + 30:
                Log.Info($"csaltfire SHOT {++taps}");
                Report("the mode stays on for the mod item", AlternativeMode(), $"swap={GUIInstance.Active.VirtualControllerInputState.SwapAlternativeFiteModeActive}");
                Report("a mod item with AltFunctionUse counts as alternative fire", self.CanAlternativeFire(true));
                Report("a mod melee weapon gets a touch category", (int)VirtualControllerInputState.GetItemCategory(ModContent.ItemType<AltWand>()) == (int)VirtualControllerInputState.Category.NonAutoFireMeleeSwing, $"category={(int)VirtualControllerInputState.GetItemCategory(ModContent.ItemType<AltWand>())}");
                AltWand.AltUses = AltWand.NormalUses = 0;
                Fire();
                step = 3;
                break;
            case 3:
                if (AltWand.AltUses + AltWand.NormalUses > 0 && frame > waited + 60 || frame > waited + 600)
                {
                    alt = AltWand.AltUses;
                    normal = AltWand.NormalUses;
                    Report("with the mode on, the aim stick fires its alternative use", alt > 0 && normal == 0, $"alt={alt} normal={normal}");
                    SetMode(false);
                    waited = frame;
                    step = 4;
                }
                break;
            case 4 when frame == waited + 60:
                Fire();
                step = 5;
                break;
            case 5:
                if (AltWand.NormalUses > normal && frame > waited + 60 || frame > waited + 600)
                {
                    Report("with the mode off, the aim stick uses it normally", AltWand.NormalUses > normal && AltWand.AltUses == alt, $"alt={AltWand.AltUses} normal={AltWand.NormalUses}");
                    Finish(self);
                }
                break;
        }
    }

    static bool AlternativeMode()
    {
        VirtualControllerInputState input = GUIInstance.Active.VirtualControllerInputState;
        return input.ToggleAlternativeFireMode;
    }

    static void Fire()
    {
        Log.Info($"csaltfire DRAG {++taps} {StickX} {StickY} {StickX + 54} {StickY}");
        waited = frame;
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
        Log.Info($"csaltfire END pass={pass} fail={fail}");
    }
}
