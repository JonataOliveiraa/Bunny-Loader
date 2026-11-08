using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ContolType = VirtualControlsHardwareConfigurationMapping_Layout.ContolType;
using Control = QuickActionButton_Layout.QuickActionControl;

namespace Terraria.ModLoader.Core;

internal static class AltFunctionTouchHooks
{
    const int InteractPress = 13, SwapPress = 30;

    static Item empty;
    static GameHandle? emptyHandle;
    static readonly Dictionary<nint, (Texture_Layout Layout, GameHandle Handle)> icons = new();

    public static void Install()
    {
        On_GUIQuickActionButton.Draw_QuickActionControl_QuickActionButton_Layout_bool_reffloat_bool_bool += Draw;
        On_GUIVirtualInputController.HandleTouchCursorDown += HandleTouchCursorDown;
    }

    static VirtualControllerInputState SwapInput()
    {
        GUIInstance gui = GUIInstance.Active;
        VirtualControllerInputState input = gui.IsNull ? default : gui.VirtualControllerInputState;
        if (input.IsNull || input.hasToggleFireInteract || !input.hasTouchRightStick) return default;
        return ItemTimingHooks.AlternativeItem(Main.LocalPlayer).IsNull ? default : input;
    }

    static bool SwapsOnInteract(int slot)
    {
        if (TouchButtonHooks.SlotAt(slot).Binding is not { IsNull: false } binding || binding.Action != ContolType.Interact) return false;
        if (Main.HasInteractableObjectThatIsNotATile) return false;
        GUIInstance gui = GUIInstance.Active;
        return !gui.IsNull && (gui.GUIPageIcons.IsNull || !gui.GUIPageIcons.IsPageSelected(GUIPageIcons.Category.Housing));
    }

    static void HandleTouchCursorDown(On_GUIVirtualInputController.orig_HandleTouchCursorDown orig, GUIVirtualInputController self, int slot, Vector2 cursorUIPosition, Cursor cursor)
    {
        GameArray<bool> pressed = default;
        VirtualControllerInputState input = default;
        Mouse.OverrideState virtualCursor = default;
        ButtonState virtualCursorState = default;
        try
        {
            if (SwapsOnInteract(slot) && !(input = SwapInput()).IsNull)
            {
                pressed = self.Pressed;
                if (pressed.IsNull || pressed.Length <= SwapPress || pressed[InteractPress]) pressed = default;
                virtualCursor = Mouse._virtualCursorOverride;
                virtualCursorState = Mouse._virtualCursorOverrideState;
            }
        }
        catch (Exception e) { ItemHooks.Report("AltFunctionUse: touch", e); }
        orig(self, slot, cursorUIPosition, cursor);
        if (pressed.IsNull) return;
        try
        {
            if (!pressed[InteractPress]) return;
            pressed[InteractPress] = false;
            pressed[SwapPress] = true;
            Mouse._virtualCursorOverride = virtualCursor;
            Mouse._virtualCursorOverrideState = virtualCursorState;
            if (!cursor.IsNull && cursor.Down && !cursor.WasDown) input.ToggleAlternativeFireMode = !input.ToggleAlternativeFireMode;
        }
        catch (Exception e) { ItemHooks.Report("AltFunctionUse: touch", e); }
    }

    static void Draw(On_GUIQuickActionButton.orig_Draw_QuickActionControl_QuickActionButton_Layout_bool_reffloat_bool_bool orig, Control actionType, QuickActionButton_Layout layout, bool pressed, ref float scale, bool ignoreChecks, bool ignoreIcon)
    {
        TransactionButton_Layout swap = default;
        Texture_Layout icon = default;
        if (actionType == Control.Interact && !ignoreIcon)
        {
            try { (swap, icon) = SwapButton(); }
            catch (Exception e) { ItemHooks.Report("AltFunctionIcon", e); }
        }
        if (swap.IsNull)
        {
            orig(actionType, layout, pressed, ref scale, ignoreChecks, ignoreIcon);
            return;
        }
        Texture_Layout original = swap.IconTexture;
        if (!icon.IsNull) swap.IconTexture = icon;
        try { GUIQuickActionButton.DrawOverloadedButtonStyle(swap, Empty(), layout, false, pressed, ref scale); }
        catch (Exception e) { ItemHooks.Report("AltFunctionIcon", e); }
        finally
        {
            if (!icon.IsNull) swap.IconTexture = original;
        }
    }

    static (TransactionButton_Layout, Texture_Layout) SwapButton()
    {
        VirtualControllerInputState input = SwapInput();
        VirtualInputController_Layout controls = VirtualInputController_Layout.Instance;
        if (input.IsNull || controls.IsNull) return default;
        TransactionButton_Layout swap = controls.ItemSwap;
        if (swap.IsNull) return default;
        Player player = Main.LocalPlayer;
        Texture2D texture = Icon(ItemTimingHooks.AlternativeItem(player), player, input.ToggleAlternativeFireMode);
        return (swap, texture.IsNull ? default : Layout(texture));
    }

    static Texture2D Icon(Item item, Player player, bool alternative)
    {
        Texture2D icon = default;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { icon = m.AltFunctionIcon(player, alternative)?.Value ?? default; }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.AltFunctionIcon", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try
            {
                Texture2D own = g.AltFunctionIcon(item, player, alternative)?.Value ?? default;
                if (!own.IsNull) icon = own;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.AltFunctionIcon", e); }
        }
        return icon;
    }

    static Texture_Layout Layout(Texture2D texture)
    {
        if (icons.TryGetValue(texture.Pointer, out var known)) return known.Layout;
        string id = $"BunnyLoader/AltFunctionIcon/{texture.Pointer:x}";
        Texture_Layout layout = Texture_Layout.New(id);
        var handle = new GameHandle(layout.Pointer);
        layout._texture = texture;
        layout._lastTextureId = id;
        icons[texture.Pointer] = (layout, handle);
        return layout;
    }

    static Item Empty()
    {
        if (emptyHandle == null)
        {
            empty = Item.New();
            emptyHandle = new GameHandle(empty.Pointer);
        }
        return empty;
    }
}
