using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ContolType = VirtualControlsHardwareConfigurationMapping_Layout.ContolType;

namespace Terraria.ModLoader.Core;

internal static unsafe class TouchButtonHooks
{
    const int GroupCentre = 9;

    static Hook? visible, quickDraw, iconFrame, controlName, controlFrame;
    static int frame;
    static int groupFrame = int.MinValue;
    static readonly List<(GameHandle Id, GameArray<ContolType> Actions)> groups = new();
    static readonly Dictionary<string, GameHandle> groupKeys = new();
    static int[] before = Array.Empty<int>();

    internal static bool Applied { get; private set; }

    internal static void Install() => ItemHooks.Once("TouchButtons", () =>
    {
        On_GUIVirtualInputController.Update += Update;
        On_GUIVirtualInputController.Draw += (orig, self) =>
        {
            Guard("arrays", () => EnsureController(self));
            orig(self);
        };
        On_GUIVirtualInputController.HandleTouchCursorDown += HandleTouchCursorDown;
        TouchJoystickHooks.Install();
        On_GUIVirtualInputControllerEdit.Draw += (orig, self) =>
        {
            Guard("editor", () => Ensure(false));
            orig(self);
        };
        On_VirtualControlsStyles_Layout.SaveConfiguration += (orig, self, preferences) => TouchButtonLayouts.Save(self, () => orig(self, preferences));
        On_VirtualControlsStyles_Layout.LoadConfiguration += (orig, self, preferences) =>
        {
            orig(self, preferences);
            Guard("saved layout", () =>
            {
                Ensure(true);
                TouchButtonLayouts.Apply(self);
            });
        };
        On_VirtualControlsStyles_Layout.GetShareCode += (orig, profile) =>
            TouchButtonLayouts.WithoutMods(profile.IsNull ? Array.Empty<VirtualControlsProfile_Layout>() : new[] { profile }, () => orig(profile));
        visible = ItemHooks.Raw("", "GUIVirtualInputController",
            "bool DetermineButtonVisible(int slot, VirtualControlsHardwareConfigurationMapping_Layout.ContolType action, VirtualControlsHardwareConfigurationMapping_Layout slotConfig)",
            (IntPtr)(delegate* unmanaged<IntPtr, int, int, IntPtr, IntPtr, byte>)&DetermineButtonVisible);
        quickDraw = ItemHooks.Raw("", "GUIQuickActionButton",
            "void Draw(QuickActionButton_Layout.QuickActionControl actionType, QuickActionButton_Layout layout, bool pressed, ref float scale, bool ignoreChecks, bool ignoreIcon)",
            (IntPtr)(delegate* unmanaged<int, IntPtr, byte, float*, byte, byte, IntPtr, void>)&QuickDraw);
        iconFrame = ItemHooks.Raw("", "GUIQuickActionButton",
            "void GetIconFrame(QuickActionButton_Layout.QuickActionControl actionType, out Texture2D iconTexture, out Rectangle frame)",
            (IntPtr)(delegate* unmanaged<int, IntPtr*, Rectangle*, IntPtr, void>)&GetIconFrame);
        controlName = ItemHooks.Raw("", "GUIVirtualInputControllerEdit",
            "string GetControlName(VirtualControlsHardwareConfigurationMapping_Layout.ContolType control)",
            (IntPtr)(delegate* unmanaged<IntPtr, int, IntPtr, IntPtr>)&GetControlName);
        controlFrame = ItemHooks.Raw("", "GUIVirtualInputControllerEdit",
            "void GetControlTextureFrame(VirtualControlsHardwareConfigurationMapping_Layout.ContolType controlType, out Texture2D tex, out Rectangle frame)",
            (IntPtr)(delegate* unmanaged<IntPtr, int, IntPtr*, Rectangle*, IntPtr, void>)&GetControlTextureFrame);
        TouchButtonOptions.Install();
    });

    static void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception e) { ItemHooks.Report($"TouchButtons: {what}", e); }
    }

    internal static void ApplySaved()
    {
        Applied = true;
        Guard("saved layout", () =>
        {
            Ensure(true);
            TouchButtonLayouts.Apply(TouchButtonLoader.Styles());
        });
    }

    static void Update(On_GUIVirtualInputController.orig_Update orig, GUIVirtualInputController self, float elapsedTime)
    {
        frame++;
        Guard("arrays", () =>
        {
            EnsureController(self);
            Ensure(false);
        });
        Guard("groups", RefreshGroups);
        int count = groups.Count;
        if (count == 0)
        {
            orig(self, elapsedTime);
            return;
        }
        if (before.Length < count) before = new int[count];
        for (int i = 0; i < count; i++) before[i] = GroupIndex(self, groups[i].Id);
        orig(self, elapsedTime);
        for (int i = 0; i < count; i++)
        {
            int was = before[i];
            GameArray<ContolType> actions = groups[i].Actions;
            if (was < 0 || was == GroupCentre || was >= actions.Length) continue;
            if (GroupIndex(self, groups[i].Id) != -1) continue;
            if (TouchButtonLoader.ByAction((int)actions[was]) is ModTouchButton button) TouchButtonLoader.Press(button);
        }
    }

    static void HandleTouchCursorDown(On_GUIVirtualInputController.orig_HandleTouchCursorDown orig, GUIVirtualInputController self, int slot, Vector2 cursorUIPosition, Cursor cursor)
    {
        ModTouchButton? button = null;
        try
        {
            (VirtualControlsBinding_Layout binding, VirtualControlsSlotTypeMapping_Layout.SlotType type) = SlotAt(slot);
            if (!binding.IsNull && type == VirtualControlsSlotTypeMapping_Layout.SlotType.Button)
                button = TouchButtonLoader.ByAction((int)binding.Action);
        }
        catch (Exception e) { ItemHooks.Report("TouchButtons: slot", e); }
        if (button == null)
        {
            orig(self, slot, cursorUIPosition, cursor);
            return;
        }
        Guard("touch", () =>
        {
            EnsureController(self);
            int control = TouchButtonLoader.ControlOf(button);
            bool first = !self.WasPressed[control];
            self.Pressed[control] = true;
            if (first) TouchButtonLoader.Press(button);
        });
    }

    [UnmanagedCallersOnly]
    static byte DetermineButtonVisible(IntPtr self, int slot, int action, IntPtr slotConfig, IntPtr method)
    {
        Hook? hook = visible;
        if (hook == null) return 1;
        if (action >= TouchButtonLoader.FirstAction && TouchButtonLoader.ByAction(action) is ModTouchButton button && !TouchButtonLoader.Visible(button)) return 0;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(slot);
        call.Arg(action);
        call.Arg(slotConfig);
        call.Arg(method);
        return (byte)(ItemHooks.Original(ref call, hook, "GUIVirtualInputController.DetermineButtonVisible") & 0xff);
    }

    [UnmanagedCallersOnly]
    static void QuickDraw(int actionType, IntPtr layout, byte pressed, float* scale, byte ignoreChecks, byte ignoreIcon, IntPtr method)
    {
        Hook? hook = quickDraw;
        if (hook == null) return;
        if (actionType >= TouchButtonLoader.QuickBase)
        {
            ModTouchButton? button = TouchButtonLoader.ByQuick(actionType);
            Texture2D texture = button != null && ignoreIcon == 0 ? TouchButtonLoader.Texture(button) : default;
            if (!texture.IsNull && layout != IntPtr.Zero)
            {
                var state = pressed != 0 ? GUIQuickActionButton.InteractionState.Pressed
                    : TouchButtonLoader.Enabled(button!) ? GUIQuickActionButton.InteractionState.Normal : GUIQuickActionButton.InteractionState.Disabled;
                try { GUIQuickActionButton.Draw(new QuickActionButton_Layout(layout), texture, state, ref *scale); }
                catch (Exception e) { ItemHooks.Report($"{button!.FullName}: draw", e); }
                return;
            }
            actionType = TouchButtonLoader.QuickUnbound;
        }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(actionType);
        call.Arg(layout);
        call.Arg(pressed);
        call.Arg((IntPtr)scale);
        call.Arg(ignoreChecks);
        call.Arg(ignoreIcon);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "GUIQuickActionButton.Draw");
    }

    [UnmanagedCallersOnly]
    static void GetIconFrame(int actionType, IntPtr* iconTexture, Rectangle* frameOut, IntPtr method)
    {
        Hook? hook = iconFrame;
        if (hook == null) return;
        if (actionType >= TouchButtonLoader.QuickBase)
        {
            ModTouchButton? button = TouchButtonLoader.ByQuick(actionType);
            Texture2D texture = button != null ? TouchButtonLoader.Texture(button) : default;
            if (!texture.IsNull)
            {
                *iconTexture = texture.Pointer;
                *frameOut = new Rectangle(0, 0, texture.Width, texture.Height);
                return;
            }
            actionType = TouchButtonLoader.QuickUnbound;
        }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(actionType);
        call.Arg((IntPtr)iconTexture);
        call.Arg((IntPtr)frameOut);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "GUIQuickActionButton.GetIconFrame");
    }

    [UnmanagedCallersOnly]
    static IntPtr GetControlName(IntPtr self, int control, IntPtr method)
    {
        Hook? hook = controlName;
        if (hook == null) return IntPtr.Zero;
        if (control >= TouchButtonLoader.FirstAction && TouchButtonLoader.ByAction(control) is ModTouchButton button)
            return GameString.New(TouchButtonLoader.Label(button));
        if (TouchButtonOptions.IsPageAction(control)) return GameString.New(TouchButtonOptions.Label(control));
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(control);
        call.Arg(method);
        return (IntPtr)ItemHooks.Original(ref call, hook, "GUIVirtualInputControllerEdit.GetControlName");
    }

    [UnmanagedCallersOnly]
    static void GetControlTextureFrame(IntPtr self, int controlType, IntPtr* tex, Rectangle* frameOut, IntPtr method)
    {
        Hook? hook = controlFrame;
        if (hook == null) return;
        if (controlType >= TouchButtonLoader.FirstAction)
        {
            Texture2D texture = default;
            if (TouchButtonLoader.ByAction(controlType) is ModTouchButton button) texture = TouchButtonLoader.Texture(button);
            else if (controlType == TouchButtonOptions.ModsAction) texture = BunnyHead();
            if (!texture.IsNull)
            {
                *tex = texture.Pointer;
                *frameOut = new Rectangle(0, 0, texture.Width, texture.Height);
                return;
            }
            controlType = TouchButtonLoader.Unbound;
        }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(controlType);
        call.Arg((IntPtr)tex);
        call.Arg((IntPtr)frameOut);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "GUIVirtualInputControllerEdit.GetControlTextureFrame");
    }

    static Texture2D BunnyHead()
    {
        try
        {
            IntPtr asset = Host.Native()->BunnyHeadTexture();
            return asset == IntPtr.Zero ? default : ReLogic.Content.Asset<Texture2D>.ForGameAsset(asset, "bunny:bunnyHead").Value;
        }
        catch (Exception e)
        {
            ItemHooks.Report("TouchButtons: bunny icon", e);
            return default;
        }
    }

    internal static void Ensure(bool force)
    {
        if (TouchButtonLoader.Count == 0) return;
        if (!force && frame % 60 != 0) return;
        Guard("statics", Statics);
        Guard("input state", InputState);
        Guard("editor list", TouchButtonOptions.Refresh);
        Guard("controller", () => EnsureController(TouchButtonLoader.Controller()));
    }

    internal static void EnsureController(GUIVirtualInputController controller)
    {
        int count = TouchButtonLoader.Count;
        if (controller.IsNull || count == 0) return;
        int size = TouchButtonLoader.FirstControl + count;
        GameArray<bool> pressed = controller.Pressed;
        if (pressed.IsNull || pressed.Length >= size) return;
        controller.Pressed = Grown(controller.Pressed, size);
        controller.WasPressed = Grown(controller.WasPressed, size);
        controller.Scale = Grown(controller.Scale, size);
    }

    static GameArray<T> Grown<T>(GameArray<T> array, int size) where T : unmanaged =>
        array.IsNull || array.Length >= size ? array : array.Resized(size);

    static void Statics()
    {
        VirtualControlsButtonConfiguration_Layout.ButtonStyles = Mapped(VirtualControlsButtonConfiguration_Layout.ButtonStyles, _ => 0, _ => 0);
        VirtualControlsAxisConfiguration_Layout.AxisStyles = Mapped(VirtualControlsAxisConfiguration_Layout.AxisStyles, _ => 0, _ => 1);
        GUIVirtualInputController.QuickActionControlFromLayoutControl =
            Mapped(GUIVirtualInputController.QuickActionControlFromLayoutControl, _ => TouchButtonLoader.QuickUnbound, TouchButtonLoader.QuickOf);
        GUIVirtualInputController.ButtonControlFromLayoutControl = Mapped(GUIVirtualInputController.ButtonControlFromLayoutControl, _ => 0, TouchButtonLoader.ControlOf);
        VirtualControllerInputState.DefaultMode = Mapped(VirtualControllerInputState.DefaultMode, i => i, TouchButtonLoader.ActionOf);
        VirtualControllerInputState.RemappedMode = Mapped(VirtualControllerInputState.RemappedMode, i => i, TouchButtonLoader.ActionOf);
    }

    static void InputState()
    {
        GUIInstance gui = GUIInstance.Active;
        if (gui.IsNull) return;
        VirtualControllerInputState state = gui.VirtualControllerInputState;
        if (state.IsNull) return;
        int last = TouchButtonLoader.LastAction + TouchButtonOptions.PageActions;
        if (HasLast(state.RemappedBindings, last)) return;
        try { state.RefreshAlternativeModeMappings(); }
        catch (Exception e) { ItemHooks.Report("TouchButtons: RefreshAlternativeModeMappings", e); }
        if (!HasLast(state.RemappedBindings, last))
            state.RemappedBindings = Mapped(state.RemappedBindings, i => i, TouchButtonLoader.ActionOf);
    }

    static bool HasLast(GameArray<ContolType> array, int last) => !array.IsNull && array.Length > last && (int)array[last] == last;

    static GameArray<T> Mapped<T>(GameArray<T> array, Func<int, int> fill, Func<ModKeybind, int> value) where T : unmanaged
    {
        if (array.IsNull || Unsafe.SizeOf<T>() != sizeof(int)) return array;
        int size = TouchButtonLoader.LastAction + 1 + TouchButtonOptions.PageActions;
        GameArray<T> result = array;
        if (array.Length < size)
        {
            int old = array.Length;
            result = array.Resized(size);
            for (int i = old; i < size; i++)
            {
                int v = fill(i);
                result[i] = Unsafe.As<int, T>(ref v);
            }
        }
        foreach (ModTouchButton button in TouchButtonLoader.Buttons)
        {
            int i = TouchButtonLoader.ActionOf(button), v = value(button);
            if (Unsafe.As<T, int>(ref result[i]) != v) result[i] = Unsafe.As<int, T>(ref v);
        }
        return result;
    }

    internal static (VirtualControlsBinding_Layout Binding, VirtualControlsSlotTypeMapping_Layout.SlotType Type) SlotAt(int slot)
    {
        VirtualControlsStyles_Layout styles = TouchButtonLoader.Styles();
        if (styles.IsNull) return default;
        VirtualControlsProfile_Layout profile = styles.ActiveProfile;
        if (profile.IsNull) return default;
        GameArray<VirtualControlsBinding_Layout> controls = profile.Controls;
        VirtualControlsHardwareConfiguration_Layout hardwareConfig = profile.HardwareConfig;
        if (controls.IsNull || hardwareConfig.IsNull) return default;
        GameArray<VirtualControlsHardwareConfigurationMapping_Layout> slots = hardwareConfig.Slots;
        if (slot < 0 || slot >= controls.Length || slots.IsNull || slot >= slots.Length) return default;
        VirtualControlsHardwareConfigurationMapping_Layout hardware = slots[slot];
        if (hardware.IsNull || hardware.Style.IsNull) return default;
        return (controls[slot], hardware.Style.ControlType);
    }

    static void RefreshGroups()
    {
        if (frame - groupFrame < 30) return;
        groupFrame = frame;
        groups.Clear();
        if (TouchButtonLoader.Count == 0) return;
        VirtualControlsStyles_Layout styles = TouchButtonLoader.Styles();
        VirtualControlsProfile_Layout profile = styles.IsNull ? default : styles.ActiveProfile;
        if (profile.IsNull) return;
        GameArray<VirtualControlsBinding_Layout> controls = profile.Controls;
        VirtualControlsHardwareConfiguration_Layout hardwareConfig = profile.HardwareConfig;
        if (controls.IsNull || hardwareConfig.IsNull || hardwareConfig.Slots.IsNull) return;
        GameArray<VirtualControlsHardwareConfigurationMapping_Layout> slots = hardwareConfig.Slots;
        int n = Math.Min(controls.Length, slots.Length);
        for (int i = 0; i < n; i++)
        {
            VirtualControlsHardwareConfigurationMapping_Layout hardware = slots[i];
            VirtualControlsBinding_Layout binding = controls[i];
            if (hardware.IsNull || binding.IsNull || hardware.Style.IsNull || hardware.Style.ControlType != VirtualControlsSlotTypeMapping_Layout.SlotType.Group) continue;
            GameArray<ContolType> actions = binding.GroupActions;
            if (actions.IsNull) continue;
            bool ours = false;
            for (int j = 0; j < actions.Length; j++)
                if (TouchButtonLoader.ByAction((int)actions[j]) != null) ours = true;
            string? id = hardware.SlotId;
            if (!ours || id == null) continue;
            if (!groupKeys.TryGetValue(id, out GameHandle? key)) groupKeys[id] = key = new GameHandle(GameString.New(id));
            groups.Add((key, actions));
        }
    }

    static int GroupIndex(GUIVirtualInputController controller, GameHandle id)
    {
        GameObject states = controller.GroupStates;
        if (states.IsNull) return -1;
        var dictionary = new GameDictionary(states);
        return dictionary.TryGetValue(id.Target, out GUIVirtualInputController.GroupControlState state) && !state.IsNull ? state.pressedIndex : -1;
    }
}
