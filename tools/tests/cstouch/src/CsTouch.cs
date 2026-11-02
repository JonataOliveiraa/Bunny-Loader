using System;
using System.Linq;
using BunnyLoader.Interop;
using log4net;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using ContolType = VirtualControlsHardwareConfigurationMapping_Layout.ContolType;

namespace CsTouch;

public sealed class TouchSystem : ModSystem
{
    public static ModTouchButton Blink = null!;
    public static ModKeybind Charge = null!;
    public static int OnPress;

    public override void Load()
    {
        Blink = TouchButtonLoader.RegisterButton(Mod, "Blink");
        Blink.OnPress = _ => OnPress++;
        Charge = KeybindLoader.RegisterKeybind(Mod, "Charge", "P");
        for (int i = 0; i < 40; i++) TouchButtonLoader.RegisterButton(Mod, $"Extra{i:00}");
    }
}

public sealed class TouchPlayer : ModPlayer
{
    public static int Presses, Releases, Held, ChargePresses;

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (TouchSystem.Blink.JustPressed)
        {
            Presses++;
            CsTouchMod.Log.Info($"cstouch JustPressed (Current {TouchSystem.Blink.Current}, Old {TouchSystem.Blink.Old}) #{Presses}");
        }
        if (TouchSystem.Blink.Current) Held++;
        if (TouchSystem.Blink.JustReleased)
        {
            Releases++;
            CsTouchMod.Log.Info($"cstouch JustReleased after {Held} held updates #{Releases}");
            Held = 0;
        }
        if (TouchSystem.Charge.JustPressed) ChargePresses++;
    }
}

public sealed class CsTouchMod : Mod
{
    const int End = 1500;
    static int frame, pass, fail, slot = -1;
    static ContolType original;

    internal static ILog Log => ModContent.GetInstance<CsTouchMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
        frame++;
        try
        {
            if (frame == 40) Setup();
            else if (frame == 50) Report("IsBound", TouchSystem.Blink.IsBound && TouchSystem.Charge.GetAssignedKeys().Count == 0, $"{TouchSystem.Blink.IsBound} {TouchSystem.Charge.GetAssignedKeys().Count}");
            else if (frame == 90) Log.Info("cstouch SHOT bound");
            else if (frame == End - 10) Finish();
        }
        catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
        if (frame == End) Log.Info($"cstouch END pass={pass} fail={fail}");
    }

    static int ActionOf(ModKeybind button) => (int)ContolType.Count + 1 + TouchButtonLoader.Buttons.ToList().IndexOf((ModTouchButton)button);

    static VirtualControlsProfile_Layout Profile() => VirtualControlsStyles_Layout.Instance.ActiveProfile;

    static void Setup()
    {
        Report("RegisterKeybind gives a ModTouchButton, and 42 buttons are registered", TouchSystem.Charge is ModTouchButton && TouchButtonLoader.Count == 42, $"count={TouchButtonLoader.Count}");
        Report("FullName", TouchSystem.Blink.FullName == "CsTouch/Blink", TouchSystem.Blink.FullName);
        Report("a name without text is the class-like name", TouchSystem.Charge.DisplayName.Value == "Charge", TouchSystem.Charge.DisplayName.Value ?? "null");
        int action = ActionOf(TouchSystem.Blink);
        GameArray<ContolType> controls = VirtualInputControllerControlConfiguration_Layout.Instance.ButtonControls;
        int mods = (int)ContolType.Count + 1 + 42;
        ContolType[] shown = controls.AsSpan().ToArray();
        Report("the editor's first page is the game's actions plus the mod button entry", shown.Length == shown.Count(a => (int)a < (int)ContolType.Count + 1) + 1 && (int)shown[^1] == mods && shown.All(a => (int)a < (int)ContolType.Count + 1 || (int)a == mods),
            string.Join(",", shown.Select(a => (int)a)));
        var quick = GUIVirtualInputController.QuickActionControlFromLayoutControl;
        Report("the draw table maps the action to its quick control", quick.Length > action && (int)quick[action] == 100, $"length {quick.Length}, value {(quick.Length > action ? (int)quick[action] : -1)}");
        GUIVirtualInputController controller = GUIInstance.Active.GUIVirtualInputController;
        Report("the controller arrays grew", controller.Pressed.Length >= (int)GUIVirtualInputController.ControlType.Count + 42, $"pressed {controller.Pressed.Length}");

        VirtualControlsProfile_Layout p = Profile();
        var slots = p.HardwareConfig.Slots;
        for (int k = 0; k < p.Controls.Length; k++)
        {
            VirtualControlsBinding_Layout b = p.Controls[k];
            if (k < slots.Length && slots[k].Style.ControlType == VirtualControlsSlotTypeMapping_Layout.SlotType.Button && (int)b.Action == 7)
            {
                slot = k;
                break;
            }
        }
        Report("a button slot (Mount) in the active profile", slot >= 0, $"slot {slot}, profile {p.Id}");
        if (slot < 0) return;
        original = p.Controls[slot].Action;
        p.Controls[slot].Action = (ContolType)action;
        Log.Info($"cstouch slot {slot} ({p.Controls[slot].SlotId}): {(int)original} -> {action}");
    }

    static void Finish()
    {
        Report("a touch becomes JustPressed", TouchPlayer.Presses >= 1, $"{TouchPlayer.Presses} press(es), onPress {TouchSystem.OnPress}");
        Report("letting go becomes JustReleased", TouchPlayer.Releases >= 1 && TouchPlayer.Releases == TouchPlayer.Presses, $"{TouchPlayer.Releases} release(s)");
        Report("OnPress once per touch", TouchSystem.OnPress == TouchPlayer.Presses, $"{TouchSystem.OnPress}/{TouchPlayer.Presses}");
        Report("Charge without a slot never fires", TouchPlayer.ChargePresses == 0, TouchPlayer.ChargePresses.ToString());
        if (slot >= 0) Profile().Controls[slot].Action = original;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok)
            pass++;
        else
            fail++;
        Log.Info($"cstouch {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
