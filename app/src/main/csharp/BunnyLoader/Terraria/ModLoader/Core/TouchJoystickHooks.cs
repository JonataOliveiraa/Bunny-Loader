using System;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using ContolType = VirtualControlsHardwareConfigurationMapping_Layout.ContolType;

namespace Terraria.ModLoader.Core;

internal static class TouchJoystickHooks
{
    sealed class Hold
    {
        internal ModTouchJoystick Joystick = null!;
        internal Cursor Cursor;
        internal GameHandle Pin = null!;
        internal Vector2 Offset, Centre;
    }

    const float ReleaseDeadZone = 0.15f;
    const int GreenStyle = 1;

    static Hold? hold;

    internal static void Install()
    {
        On_GUIVirtualInputController.UpdateCursor += UpdateCursor;
        On_GUIVirtualInputController.DrawAxisControl += DrawAxisControl;
        On_GUIVirtualInputController.Update += Update;
    }

    static Axis_Layout Layout(VirtualControlsHardwareConfigurationMapping_Layout slot)
    {
        if (slot.IsNull || slot.Style.IsNull) return default;
        VirtualControlsAxisConfiguration_Layout config = slot.Style.AxisConfiguration;
        if (config.IsNull) return default;
        Axis_Layout layout = config.GetStyle((VirtualControlsAxisConfiguration_Layout.Style)GreenStyle);
        if (layout.IsNull) return default;
        layout.AnchorControl = slot.AnchorControl;
        layout.Anchor = slot.Anchor;
        layout.CentreLocation = slot.Location;
        return layout;
    }

    static void UpdateCursor(On_GUIVirtualInputController.orig_UpdateCursor orig, GUIVirtualInputController self, Cursor cursor)
    {
        bool mine = false;
        try { mine = Track(cursor); }
        catch (Exception e) { ItemHooks.Report("TouchJoystick: cursor", e); }
        if (!mine) orig(self, cursor);
    }

    static bool Track(Cursor cursor)
    {
        if (cursor.IsNull) return false;
        Hold? current = hold;
        if (current != null)
        {
            return cursor.Id == current.Cursor.Id;
        }
        if (Main.gameMenu || !cursor.Down || cursor.WasDown || TouchButtonLoader.Count == 0) return false;
        VirtualControlsStyles_Layout styles = TouchButtonLoader.Styles();
        VirtualControlsProfile_Layout profile = styles.IsNull ? default : styles.ActiveProfile;
        if (profile.IsNull || profile.HardwareConfig.IsNull) return false;
        GameArray<VirtualControlsBinding_Layout> controls = profile.Controls;
        GameArray<VirtualControlsHardwareConfigurationMapping_Layout> slots = profile.HardwareConfig.Slots;
        int count = controls.IsNull || slots.IsNull ? 0 : Math.Min(controls.Length, slots.Length);
        for (int i = 0; i < count; i++)
        {
            VirtualControlsBinding_Layout binding = controls[i];
            VirtualControlsHardwareConfigurationMapping_Layout slot = slots[i];
            if (binding.IsNull || slot.IsNull || slot.Style.IsNull || slot.Style.ControlType != VirtualControlsSlotTypeMapping_Layout.SlotType.Axis) continue;
            if (TouchButtonLoader.ByAction((int)binding.Action) is not ModTouchJoystick joystick || !TouchButtonLoader.Visible(joystick) || !TouchButtonLoader.Enabled(joystick)) continue;
            Axis_Layout layout = Layout(slot);
            if (layout.IsNull) continue;
            Vector2 position = XNAUnityRunner.AdjustRealScreenForUI(cursor.Position), centre = default;
            if (!GUIAxis.IsCursorOver(position, layout, out Vector2 offset, out Vector2 axis, ref centre, false)) continue;
            hold = new Hold { Joystick = joystick, Cursor = cursor, Pin = new GameHandle(cursor.Pointer), Offset = offset, Centre = centre };
            joystick.Direction = axis;
            joystick.latch = true;
            HeldSlot = slot;
            return true;
        }
        return false;
    }

    static VirtualControlsHardwareConfigurationMapping_Layout HeldSlot;

    static void Read(Hold current, Cursor cursor, bool active)
    {
        Axis_Layout layout = Layout(HeldSlot);
        if (layout.IsNull) return;
        Vector2 centre = current.Centre, position = XNAUnityRunner.AdjustRealScreenForUI(cursor.Position);
        GUIAxis.IsCursorOver(position, layout, out Vector2 offset, out Vector2 axis, ref centre, active, true);
        current.Centre = centre;
        current.Offset = offset;
        current.Joystick.Direction = axis;
    }

    static void Release(Hold current)
    {
        hold = null;
        current.Pin.Dispose();
        ModTouchJoystick joystick = current.Joystick;
        Vector2 direction = joystick.Direction;
        joystick.Direction = Vector2.Zero;
        if (Main.gameMenu || joystick.OnRelease == null || direction.Length() < ReleaseDeadZone) return;
        try { joystick.OnRelease(Main.player[Main.myPlayer], direction); }
        catch (Exception e) { ItemHooks.Report($"{joystick.FullName}.OnRelease", e); }
    }

    static void Update(On_GUIVirtualInputController.orig_Update orig, GUIVirtualInputController self, float elapsedTime)
    {
        orig(self, elapsedTime);
        Hold? current = hold;
        if (current == null) return;
        if (Main.gameMenu || !current.Cursor.Down || current.Cursor.Dead)
        {
            Release(current);
            return;
        }
        Read(current, current.Cursor, true);
        current.Joystick.latch = true;
    }

    static void DrawAxisControl(On_GUIVirtualInputController.orig_DrawAxisControl orig, GUIVirtualInputController self, ContolType action, VirtualControlsHardwareConfigurationMapping_Layout slotConfig)
    {
        if (TouchButtonLoader.ByAction((int)action) is not ModTouchJoystick joystick)
        {
            orig(self, action, slotConfig);
            return;
        }
        if (!TouchButtonLoader.Visible(joystick)) return;
        try
        {
            Axis_Layout layout = Layout(slotConfig);
            if (layout.IsNull) return;
            Hold? current = hold != null && hold.Joystick == joystick ? hold : null;
            Vector2 offset = current?.Offset ?? Vector2.Zero, centre = current?.Centre ?? Vector2.Zero;
            GUIAxis.Draw(layout, TouchButtonLoader.Texture(joystick), ref offset, ref centre, current != null, false, !TouchButtonLoader.Enabled(joystick), (Action?)null);
        }
        catch (Exception e) { ItemHooks.Report($"{joystick.FullName}: draw", e); }
    }
}
