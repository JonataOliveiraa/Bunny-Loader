using System;
using System.Globalization;
using System.IO;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using ContolType = VirtualControlsHardwareConfigurationMapping_Layout.ContolType;
using Panel = GUIVirtualInputControllerEdit_ControlPanel;
using Slider = GUIVirtualInputControllerEdit_ControlPanel.ControlPanelSizeSlider;

namespace CsTouchSize;

public sealed class SizeSystem : ModSystem
{
    public static ModTouchButton Big = null!;
    public static int Presses;

    public override void Load()
    {
        Big = TouchButtonLoader.RegisterButton(Mod, "Big");
        Big.OnPress = _ => Presses++;
    }
}

public sealed class CsTouchSizeMod : Mod
{
    const string ButtonSlot = "S6";
    const string StickSlot = "S7";
    const int TapWait = 600;

    static int frames, pass, fail, step, stepFrame, taps, pressesBefore;
    static bool done;
    static ContolType originalAction;
    static string originalButton = "", originalStick = "";
    static Vector2 originalLocation, originalStickLocation, dragFrom, dragTo;
    static float startX, farthest;
    static Rectangle region, visual;
    static bool overNear, overFar;
    static Slider slider;

    static ILog Log => ModContent.GetInstance<CsTouchSizeMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUIVirtualInputController.DrawButtonControl += DrawButtonControl;
    }

    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    static VirtualControlsStyles_Layout Styles => VirtualControlsStyles_Layout.Instance;

    static VirtualControlsHardwareConfigurationMapping_Layout Slot(string id, out int index)
    {
        index = -1;
        GameArray<VirtualControlsHardwareConfigurationMapping_Layout> slots = Styles.ActiveProfile.HardwareConfig.Slots;
        for (int k = 0; k < slots.Length; k++)
            if (slots[k].SlotId == id)
            {
                index = k;
                return slots[k];
            }
        return default;
    }

    static VirtualControlsBinding_Layout Binding(string id)
    {
        GameArray<VirtualControlsBinding_Layout> controls = Styles.ActiveProfile.Controls;
        for (int k = 0; k < controls.Length; k++)
            if (controls[k].SlotId == id) return controls[k];
        return default;
    }

    static void DrawButtonControl(On_GUIVirtualInputController.orig_DrawButtonControl orig, GUIVirtualInputController self, ContolType action,
        VirtualControlsHardwareConfigurationMapping_Layout slotConfig, bool pressed, ref float scale)
    {
        orig(self, action, slotConfig, pressed, ref scale);
        try
        {
            if (slotConfig.IsNull || slotConfig.SlotId != ButtonSlot || action != (ContolType)TouchButtonLoader.ActionOf(SizeSystem.Big)) return;
            QuickActionButton_Layout layout = slotConfig.Style.ButtonConfiguration.GetStyle(VirtualControlsButtonConfiguration_Layout.Style.Blue);
            var texture = layout.BackingNormal.Texture;
            region = GUIButton.GetRegion(layout, texture);
            visual = GUIButton.GetRegionWithoutPicking(layout, texture);
            Vector2 centre = new(visual.Center.X, visual.Center.Y);
            overNear = GUIQuickActionButton.IsCursorOver(centre + new Vector2(60, 0), layout);
            overFar = GUIQuickActionButton.IsCursorOver(centre + new Vector2(140, 0), layout);
        }
        catch (Exception e) { Report("draw hook", false, e.Message); }
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done) return;
        frames++;
        try { Step(); }
        catch (Exception e)
        {
            Report($"step {step}", false, e.ToString());
            Finish();
        }
    }

    static void Next()
    {
        step++;
        stepFrame = frames;
    }

    static bool Waited(int n) => frames - stepFrame >= n;

    static void Tap(string why)
    {
        taps++;
        int x = visual.Center.X + 60, y = visual.Center.Y;
        Log.Info($"cstouchsize tap {why} at {x},{y} (button {visual.Width}x{visual.Height} at {visual.Center.X},{visual.Center.Y}, pick {region.Width}x{region.Height})");
        Log.Info($"cstouchsize TAP {taps} {x} {y}");
    }

    static void Step()
    {
        switch (step)
        {
            case 0:
                if (frames < 60) return;
                Setup();
                Next();
                return;
            case 1:
                if (!Waited(10)) return;
                SliderChecks();
                StyleChecks();
                Next();
                return;
            case 2:
                if (!Waited(30)) return;
                Report("the button starts with the game's small size", visual.Width == 39 && !overNear, $"{visual.Width}x{visual.Height}, near {overNear}");
                Log.Info($"cstouchsize SHOT {++taps} small");
                pressesBefore = SizeSystem.Presses;
                Tap("outside the small button");
                Next();
                return;
            case 3:
                if (!Waited(TapWait)) return;
                Report("a touch 60 units from the small button does not press it", SizeSystem.Presses == pressesBefore, $"presses {SizeSystem.Presses - pressesBefore}");
                Panel.ActiveSlotConfig = Slot(ButtonSlot, out _);
                Panel.ActiveControl = Binding(ButtonSlot);
                slider.SetValue(13f / 18f);
                Report("step 13 of the slider gives the 200% button", Slot(ButtonSlot, out _).Style.ControlTypeId == "Action_104_BL" && slider.GetStringValue() == "200%",
                    $"{Slot(ButtonSlot, out _).Style.ControlTypeId} {slider.GetStringValue()}");
                Next();
                return;
            case 4:
                if (!Waited(30)) return;
                Report("the button is drawn at 200%", visual.Width == 104, $"{visual.Width}x{visual.Height}");
                Report("the pick region grew with it", region.Width >= 140 && overNear && !overFar, $"pick {region.Width}x{region.Height}, near {overNear}, far {overFar}");
                Log.Info($"cstouchsize SHOT {++taps} big");
                pressesBefore = SizeSystem.Presses;
                Tap("inside the big button");
                Next();
                return;
            case 5:
                if (SizeSystem.Presses == pressesBefore && !Waited(TapWait)) return;
                Report("the same touch now presses the big button", SizeSystem.Presses == pressesBefore + 1, $"presses {SizeSystem.Presses - pressesBefore}");
                SaveLoadChecks();
                Next();
                return;
            case 6:
                if (!Waited(30)) return;
                Drag("outside the game's joystick");
                Next();
                return;
            case 7:
                Track();
                if (!Waited(TapWait)) return;
                Report("a drag 90 units from the game's joystick does not move the player", farthest < 8, $"moved {F(farthest)}");
                Panel.ActiveSlotConfig = Slot(StickSlot, out _);
                Panel.ActiveControl = Binding(StickSlot);
                slider.SetValue(10f / 20f);
                Report("the slider gives the 150% joystick", Slot(StickSlot, out _).Style.ControlTypeId == "Axis_156_BL" && slider.GetStringValue() == "150%",
                    $"{Slot(StickSlot, out _).Style.ControlTypeId} {slider.GetStringValue()}");
                Panel.ActiveSlotConfig = default;
                Panel.ActiveControl = default;
                Next();
                return;
            case 8:
                if (!Waited(30)) return;
                Log.Info($"cstouchsize SHOT {++taps} big joystick");
                Drag("inside the 150% joystick");
                Next();
                return;
            case 9:
                Track();
                if (farthest < 32 && !Waited(TapWait)) return;
                Report("the same drag moves the player with the 150% joystick", farthest >= 32, $"moved {F(farthest)}");
                SaveLoadChecks();
                Next();
                return;
            case 10:
                if (!Waited(30)) return;
                Restore();
                Finish();
                return;
        }
    }

    static Vector2 StickCentre()
    {
        VirtualControlsHardwareConfigurationMapping_Layout stick = Slot(StickSlot, out _);
        Axis_Layout l = stick.Style.AxisConfiguration.GetStyle(VirtualControlsAxisConfiguration_Layout.Style.Blue);
        var keep = (l.AnchorControl, l.Anchor, l.CentreLocation);
        l.AnchorControl = stick.AnchorControl;
        l.Anchor = stick.Anchor;
        l.CentreLocation = stick.Location;
        Rectangle r = GUIAxis.BackingRegion(l);
        (l.AnchorControl, l.Anchor, l.CentreLocation) = keep;
        return new Vector2(r.Center.X, r.Center.Y);
    }

    static void Drag(string why)
    {
        taps++;
        Vector2 centre = StickCentre();
        dragFrom = centre + new Vector2(90, 0);
        dragTo = centre + new Vector2(130, 0);
        startX = Main.player[Main.myPlayer].position.X;
        farthest = 0;
        Log.Info($"cstouchsize drag {why} from {F(dragFrom.X)},{F(dragFrom.Y)} (joystick at {F(centre.X)},{F(centre.Y)}, {Slot(StickSlot, out _).Style.ControlTypeId})");
        Log.Info($"cstouchsize DRAG {taps} {(int)dragFrom.X} {(int)dragFrom.Y} {(int)dragTo.X} {(int)dragTo.Y}");
    }

    static void Track() => farthest = MathF.Max(farthest, MathF.Abs(Main.player[Main.myPlayer].position.X - startX));

    static void Setup()
    {
        VirtualControlsHardwareConfigurationMapping_Layout button = Slot(ButtonSlot, out int index), stick = Slot(StickSlot, out _);
        Report("the active profile has the slots", !button.IsNull && !stick.IsNull, $"{Styles.ActiveProfile.Id} {index}");
        originalButton = button.Style.ControlTypeId ?? "";
        originalStick = stick.Style.ControlTypeId ?? "";
        VirtualControlsBinding_Layout binding = Binding(ButtonSlot);
        originalAction = binding.Action;
        binding.Action = (ContolType)TouchButtonLoader.ActionOf(SizeSystem.Big);
        button.Style.ControlTypeId = "Action_39";
        originalLocation = button.Location;
        button.Location = new Vector2(300, -155);
        originalStickLocation = stick.Location;
        stick.Location = new Vector2(550, -200);
        slider = Slider.New();
        Log.Info($"cstouchsize setup: {ButtonSlot} {originalButton} action {(int)originalAction} -> Big, {StickSlot} {originalStick}");
    }

    static void SliderChecks()
    {
        VirtualControlsHardwareConfigurationMapping_Layout stick = Slot(StickSlot, out _);
        Panel.ActiveSlotConfig = stick;
        Panel.ActiveControl = Binding(StickSlot);
        stick.Style.ControlTypeId = "Axis_104";
        Report("the joystick slider has 21 steps", slider.GetMaxValue() == 20, slider.GetMaxValue().ToString());
        Report("Axis_104 sits on step 5 and reads 100%", MathF.Abs(slider.GetValue() - 5f / 20f) < 0.001f && slider.GetStringValue() == "100%", $"{F(slider.GetValue())} {slider.GetStringValue()}");
        slider.SetValue(0f);
        Report("the left end is 50%", stick.Style.ControlTypeId == "Axis_52_BL" && slider.GetStringValue() == "50%", $"{stick.Style.ControlTypeId} {slider.GetStringValue()}");
        slider.SetValue(1f / 20f);
        Report("the next step is the game's Axis_60", stick.Style.ControlTypeId == "Axis_60" && slider.GetStringValue() == "58%", $"{stick.Style.ControlTypeId} {slider.GetStringValue()}");
        slider.SetValue(11f / 20f);
        Report("step 11 is 160%", stick.Style.ControlTypeId == "Axis_166_BL" && slider.GetStringValue() == "160%", $"{stick.Style.ControlTypeId} {slider.GetStringValue()}");
        Report("GetValue follows the new size", MathF.Abs(slider.GetValue() - 11f / 20f) < 0.001f, F(slider.GetValue()));
        slider.SetValue(1f);
        Report("the right end is the 250% joystick", stick.Style.ControlTypeId == "Axis_260_BL" && slider.GetStringValue() == "250%", $"{stick.Style.ControlTypeId} {slider.GetStringValue()}");
        stick.Style.ControlTypeId = originalStick;

        VirtualControlsHardwareConfigurationMapping_Layout button = Slot(ButtonSlot, out _);
        Panel.ActiveSlotConfig = button;
        Panel.ActiveControl = Binding(ButtonSlot);
        Report("the mod button slider uses the 19 action sizes", slider.GetMaxValue() == 18, slider.GetMaxValue().ToString());
        Report("Action_39 reads 75%", slider.GetStringValue() == "75%", slider.GetStringValue() ?? "null");
        slider.SetValue(10f / 18f);
        Report("step 10 of the button reads the round 170%", button.Style.ControlTypeId == "Action_88_BL" && slider.GetStringValue() == "170%", $"{button.Style.ControlTypeId} {slider.GetStringValue()}");
        slider.SetValue(1f);
        Report("the right end is the 250% button", button.Style.ControlTypeId == "Action_130_BL" && slider.GetStringValue() == "250%", $"{button.Style.ControlTypeId} {slider.GetStringValue()}");
        button.Style.ControlTypeId = "Action_39";
        Panel.ActiveSlotConfig = default;
        Panel.ActiveControl = default;
    }

    static void StyleChecks()
    {
        VirtualControlsStyles_Layout styles = Styles;
        VirtualControlsAxisConfiguration_Layout big = styles.GetAxisConfiguration("Axis_166_BL"), from = styles.GetAxisConfiguration("Axis_135");
        Report("Axis_166_BL exists", !big.IsNull && big.Id == "Axis_166_BL", "");
        if (!big.IsNull)
        {
            Axis_Layout g = big.GreenLayout, v = from.GreenLayout;
            float f = 166f / 135f;
            Report("the joystick scale and pick size grow together", MathF.Abs(g.Scale - v.Scale * f) < 0.01f && MathF.Abs(g.PickSize.X - v.PickSize.X * f) < 0.01f && g.Pointer != v.Pointer,
                $"scale {F(g.Scale)} pick {F(g.PickSize.X)} (game {F(v.Scale)} {F(v.PickSize.X)})");
            VirtualControlsHardwareConfigurationMapping_Layout stick = Slot(StickSlot, out _);
            var keep = (g.AnchorControl, g.Anchor, g.CentreLocation, v.AnchorControl, v.Anchor, v.CentreLocation);
            g.AnchorControl = v.AnchorControl = stick.AnchorControl;
            g.Anchor = v.Anchor = stick.Anchor;
            g.CentreLocation = v.CentreLocation = stick.Location;
            Rectangle rg = GUIAxis.BackingRegion(g), rv = GUIAxis.BackingRegion(v);
            Vector2 point = new(rv.Center.X + 105, rv.Center.Y), centre = default;
            bool inBig = GUIAxis.IsCursorOver(point, g, out _, out _, ref centre, false);
            centre = default;
            bool inGame = GUIAxis.IsCursorOver(point, v, out _, out _, ref centre, false);
            Report("the 166 joystick is drawn bigger and takes a touch 105 units away", rg.Width > rv.Width && inBig && !inGame, $"backing {rg.Width} vs {rv.Width}, touch {inBig}/{inGame}");
            (g.AnchorControl, g.Anchor, g.CentreLocation, v.AnchorControl, v.Anchor, v.CentreLocation) = keep;
        }
        VirtualControlsButtonConfiguration_Layout meta = styles.GetButtonConfiguration("Meta_56_BL");
        if (!meta.IsNull)
        {
            QuickActionButton_Layout t = meta.TealLayout, v = styles.GetButtonConfiguration("Meta_39").TealLayout;
            float f = 56f / 39f;
            Report("Meta_56_BL scales the button, the pressed size and the pick border", MathF.Abs(t.Scale - v.Scale * f) < 0.01f && MathF.Abs(t.PressedScale - v.PressedScale * f) < 0.01f && MathF.Abs(t.PickBorder.X - v.PickBorder.X * f) < 0.01f,
                $"{F(t.Scale)} {F(t.PressedScale)} {F(t.PickBorder.X)}");
        }
        else Report("Meta_56_BL exists", false, "");
        VirtualControlsGroupConfiguration_Layout group = styles.GetGroupConfiguration("Group_78_BL"), groupFrom = styles.GetGroupConfiguration("Group_52");
        if (!group.IsNull)
        {
            VirtualControlsButtonConfiguration_Layout centreButton = group.CentreMapping, outer = group.OuterMapping;
            Report("Group_78_BL points at scaled centre and outer buttons", group.CentreButtonStyle == "Action_78_BL" && group.OuterButtonStyle == "Group_78_BL" && !centreButton.IsNull && !outer.IsNull
                && MathF.Abs(centreButton.BlueLayout.Scale - 1.5f) < 0.01f, $"{group.CentreButtonStyle} {group.OuterButtonStyle} {(centreButton.IsNull ? "-" : F(centreButton.BlueLayout.Scale))}");
            Report("Group_78_BL scales the radius and the backing", MathF.Abs(group.OuterButtonRadiusMax - groupFrom.OuterButtonRadiusMax * 1.5f) < 0.01f && MathF.Abs(group.Backing.Size.X - groupFrom.Backing.Size.X * 1.5f) < 0.01f
                && group.Backing.Pointer != groupFrom.Backing.Pointer, $"radius {F(group.OuterButtonRadiusMax)} backing {F(group.Backing.Size.X)}");
        }
        else Report("Group_78_BL exists", false, "");
        Report("the game's styles are untouched", MathF.Abs(from.GreenLayout.Scale - 1.3f) < 0.001f && MathF.Abs(styles.GetButtonConfiguration("Action_65").BlueLayout.Scale - 1.25f) < 0.001f, "");
    }

    static void SaveLoadChecks()
    {
        VirtualControlsStyles_Layout styles = Styles;
        string code = VirtualControlsStyles_Layout.GetShareCode(styles.ActiveProfile) ?? "";
        Slot(ButtonSlot, out _).Style.ControlTypeId = "Action_65";
        string vanillaCode = VirtualControlsStyles_Layout.GetShareCode(styles.ActiveProfile) ?? "";
        Slot(ButtonSlot, out _).Style.ControlTypeId = "Action_104_BL";
        Report("the share code carries the nearest game size", code.Length > 0 && code == vanillaCode, $"{code.Length} chars");
        Main.SaveSettings();
        string game = File.ReadAllText(Path.Combine(Main.SavePath, "VirtualControls.json"));
        string ours = Path.Combine(Main.SavePath, "BunnyLoader.touchsizes.json");
        string sizes = File.Exists(ours) ? File.ReadAllText(ours) : "";
        Report("the game's file only has the game's sizes", !game.Contains("_BL"), "");
        Report("the size goes to BunnyLoader.touchsizes.json", sizes.Contains("\"slot\":\"S6\"") && sizes.Contains("\"style\":\"Action_65\"") && sizes.Contains("\"size\":104"), sizes);
        Report("the slot keeps the size after saving", Slot(ButtonSlot, out _).Style.ControlTypeId == "Action_104_BL", Slot(ButtonSlot, out _).Style.ControlTypeId ?? "null");
        Slot(ButtonSlot, out _).Style.ControlTypeId = "Action_65";
        styles.LoadConfiguration(Main.Configuration);
        Report("loading the controls brings the size back", Slot(ButtonSlot, out _).Style.ControlTypeId == "Action_104_BL", Slot(ButtonSlot, out _).Style.ControlTypeId ?? "null");
    }

    static void Restore()
    {
        Panel.ActiveSlotConfig = default;
        Panel.ActiveControl = default;
        Slot(ButtonSlot, out _).Style.ControlTypeId = originalButton;
        Slot(ButtonSlot, out _).Location = originalLocation;
        Slot(StickSlot, out _).Style.ControlTypeId = originalStick;
        Slot(StickSlot, out _).Location = originalStickLocation;
        Binding(ButtonSlot).Action = originalAction;
        Main.SaveSettings();
        string ours = Path.Combine(Main.SavePath, "BunnyLoader.touchsizes.json");
        Report("back to the game's sizes, the size file goes away", !File.Exists(ours), "");
    }

    static void Finish()
    {
        done = true;
        Log.Info($"cstouchsize END pass={pass} fail={fail}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cstouchsize {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
