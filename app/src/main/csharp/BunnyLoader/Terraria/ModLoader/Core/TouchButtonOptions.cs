using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Hooks;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.Localization;
using ContolType = VirtualControlsHardwareConfigurationMapping_Layout.ContolType;
using ActionSetting = GUIVirtualInputControllerEdit_ControlPanel.ControlPanelActionSetting;

namespace Terraria.ModLoader.Core;

internal static unsafe class TouchButtonOptions
{
    internal const int PageActions = 3;
    const int ClickedState = (int)GUITransactionButton.InputState.Clicked;
    const int NoneState = (int)GUITransactionButton.InputState.None;
    const int OverState = (int)GUITransactionButton.InputState.Over;

    const string TransactionSignature =
        "GUITransactionButton.InputState Draw(TransactionButton_Layout layout, Item item, string label, bool disabled, ref float scale, bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState)";

    static Hook? transactionDraw;
    static readonly int* TransactionWanted = (int*)NativeMemory.AllocZeroed(sizeof(int));
    static int[]? vanilla;
    static int lines = 1;
    static int page;
    static IntPtr openSetting;
    static GameHandle? openPin;
    const int ApplyDelayFrames = 1;

    const int SwallowFrames = 3;

    static bool capturing, clicked;
    static int applyDelay, swallowClick;

    static ModTouchButton[]? plainSource;
    static readonly List<int> plainActions = new(), built = new(), axisWanted = new();

    internal static int ModsAction => TouchButtonLoader.FirstAction + TouchButtonLoader.Count;

    internal static int BackAction => ModsAction + 1;

    internal static int MoreAction => ModsAction + 2;

    internal static bool IsPageAction(int action) => action >= ModsAction && action < ModsAction + PageActions;

    static int PerPage => Math.Max(1, (vanilla?.Length ?? 27) - 2);

    static List<int> PlainActions()
    {
        ModTouchButton[] all = TouchButtonLoader.Snapshot;
        if (ReferenceEquals(plainSource, all)) return plainActions;
        plainActions.Clear();
        foreach (ModTouchButton button in all)
            if (button is not ModTouchJoystick) plainActions.Add(TouchButtonLoader.ActionOf(button));
        plainSource = all;
        return plainActions;
    }

    static int Pages => (PlainActions().Count + PerPage - 1) / PerPage;

    static void SyncTransaction() => *TransactionWanted = capturing || swallowClick > 0 ? 1 : 0;

    internal static void Install()
    {
        On_GUIVirtualInputControllerEdit_ControlPanel_ControlPanelActionSetting.PulldownOptionDraw += PulldownOptionDraw;
        On_GUIVirtualInputControllerEdit.Draw += (orig, self) =>
        {
            if (openSetting != IntPtr.Zero && !new ActionSetting(openSetting).displayOptions)
                CloseList();
            if (swallowClick > 0)
            {
                swallowClick--;
                SyncTransaction();
            }
            if (applyDelay > 0 && --applyDelay == 0)
            {
                try { Apply(); }
                catch (Exception e) { ItemHooks.Report("TouchButtons: change page", e); }
            }
            orig(self);
        };
        var gate = new GateSpec { Arg = HookGates.AbsoluteArg, PointerOffset = -1, FieldOffset = 0, FieldSize = 4, Address = (IntPtr)TransactionWanted };
        try
        {
            transactionDraw = Hook.InstallGated(new GameClass("", "GUITransactionButton"), TransactionSignature,
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte, float*, byte, byte, byte, byte, IntPtr, int>)&TransactionDraw, gate, out _);
        }
        catch (Exception e) { Log.Error($"item hooks: GUITransactionButton.{TransactionSignature} is not hooked: {e.Message}"); }
    }

    internal static string Label(int action) =>
        Language.GetTextValue(action == ModsAction ? "BunnyLoader.TouchButtonsMods" : action == BackAction ? "BunnyLoader.Back" : "BunnyLoader.TouchButtonsMore");

    internal static void Refresh()
    {
        VirtualInputControllerControlConfiguration_Layout config = VirtualInputControllerControlConfiguration_Layout.Instance;
        if (config.IsNull || config.ButtonControls.IsNull) return;
        if (vanilla == null)
        {
            var own = new List<int>();
            foreach (ContolType action in config.ButtonControls)
                if ((int)action < TouchButtonLoader.FirstAction) own.Add((int)action);
            vanilla = own.ToArray();
            ItemGrid_Layout grid = config.PulldownOptionGrid;
            if (!grid.IsNull) lines = Math.Max(1, grid.ItemLineCount);
        }
        Apply();
        ApplyAxis(config);
    }

    static void ApplyAxis(VirtualInputControllerControlConfiguration_Layout config)
    {
        if (config.AxisControls.IsNull) return;
        List<int> wanted = axisWanted;
        wanted.Clear();
        foreach (ContolType action in config.AxisControls)
            if ((int)action < TouchButtonLoader.FirstAction) wanted.Add((int)action);
        foreach (ModTouchButton button in TouchButtonLoader.Snapshot)
            if (button is ModTouchJoystick) wanted.Add(TouchButtonLoader.ActionOf(button));
        if (!Same(config.AxisControls, wanted))
        {
            GameArray<ContolType> list = config.AxisControls.Length != wanted.Count ? config.AxisControls.Resized(wanted.Count) : config.AxisControls;
            for (int i = 0; i < wanted.Count; i++) list[i] = (ContolType)wanted[i];
            config.AxisControls = list;
        }
        ItemGrid_Layout grid = config.PulldownOptionGridAxis;
        if (grid.IsNull || grid.ItemCount >= wanted.Count) return;
        grid.ItemCount = wanted.Count;
    }

    static List<int> Build()
    {
        List<int> list = built;
        list.Clear();
        List<int> plain = PlainActions();
        int count = plain.Count;
        if (page <= 0 || count == 0)
        {
            list.AddRange(vanilla!);
            if (count > 0) list.Add(ModsAction);
            return list;
        }
        list.Add(BackAction);
        int first = (page - 1) * PerPage;
        for (int i = first; i < Math.Min(count, first + PerPage); i++) list.Add(plain[i]);
        if (page < Pages) list.Add(MoreAction);
        return list;
    }

    static void Apply()
    {
        if (vanilla == null) return;
        VirtualInputControllerControlConfiguration_Layout config = VirtualInputControllerControlConfiguration_Layout.Instance;
        if (config.IsNull) return;
        List<int> wanted = Build();
        GameArray<ContolType> current = config.ButtonControls;
        if (!Same(current, wanted))
        {
            GameArray<ContolType> list = current.IsNull || current.Length != wanted.Count ? current.Resized(wanted.Count) : current;
            for (int i = 0; i < wanted.Count; i++) list[i] = (ContolType)wanted[i];
            config.ButtonControls = list;
            current = list;
        }
        if (openSetting != IntPtr.Zero && !new ActionSetting(openSetting).axisOptions)
        {
            var setting = new ActionSetting(openSetting);
            setting.controlOptions = current;
            setting.selectedOption = wanted.IndexOf((int)setting.GetControlType());
        }
        ItemGrid_Layout grid = config.PulldownOptionGrid;
        if (grid.IsNull) return;
        int items = wanted.Count;
        if (grid.ItemLineCount != lines) grid.ItemLineCount = lines;
        if (grid.ItemCount != items) grid.ItemCount = items;
    }

    static bool Same(GameArray<ContolType> array, List<int> wanted)
    {
        if (array.IsNull || array.Length != wanted.Count) return false;
        for (int i = 0; i < wanted.Count; i++)
            if ((int)array[i] != wanted[i]) return false;
        return true;
    }

    static int PageOf(int action)
    {
        int index = PlainActions().IndexOf(action);
        return index >= 0 ? index / PerPage + 1 : 0;
    }

    static void CloseList()
    {
        openSetting = IntPtr.Zero;
        openPin = null;
        if (page == 0)
            return;
        page = 0;
        Apply();
    }

    static void OpenList(ActionSetting setting)
    {
        openSetting = setting.Pointer;
        openPin = new GameHandle(setting.Pointer);
        try
        {
            int wanted = PageOf((int)setting.GetControlType());
            if (wanted != page)
            {
                page = wanted;
                applyDelay = ApplyDelayFrames;
            }
        }
        catch (Exception e) { ItemHooks.Report("TouchButtons: open the list", e); }
    }

    static void PulldownOptionDraw(On_GUIVirtualInputControllerEdit_ControlPanel_ControlPanelActionSetting.orig_PulldownOptionDraw orig, ActionSetting self,
        ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        if (self.Pointer != openSetting)
            OpenList(self);
        GameArray<ContolType> options = self.controlOptions;
        if (options.IsNull || index < 0 || index >= options.Length)
            return;
        int action = (int)options[index];
        if (!IsPageAction(action))
        {
            orig(self, gridLayout, index, position, scale);
            return;
        }
        capturing = true;
        SyncTransaction();
        clicked = false;
        try { orig(self, gridLayout, index, position, scale); }
        finally
        {
            capturing = false;
            SyncTransaction();
        }
        if (!clicked) return;
        swallowClick = SwallowFrames;
        SyncTransaction();
        Main.mouseLeftRelease = true;
        page = action == ModsAction ? 1 : action == MoreAction ? page + 1 : page - 1;
        page = Math.Clamp(page, 0, Pages);
        applyDelay = ApplyDelayFrames;
        try { SoundEngine.PlaySound(12); }
        catch (Exception e) { ItemHooks.Report("TouchButtons: change page", e); }
    }

    [UnmanagedCallersOnly]
    static int TransactionDraw(IntPtr layout, IntPtr item, IntPtr label, byte disabled, float* scale, byte forcedPressed, byte hasControllerFocus, byte forceOver,
        byte disablePressedState, IntPtr method)
    {
        Hook? hook = transactionDraw;
        if (hook == null) return NoneState;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(layout);
        call.Arg(item);
        call.Arg(label);
        call.Arg(disabled);
        call.Arg((IntPtr)scale);
        call.Arg(forcedPressed);
        call.Arg(hasControllerFocus);
        call.Arg(forceOver);
        call.Arg(disablePressedState);
        call.Arg(method);
        int state = (int)ItemHooks.Original(ref call, hook, "GUITransactionButton.Draw");
        if (swallowClick > 0 && state == ClickedState)
            return OverState;
        if (!capturing || state != ClickedState) return state;
        clicked = true;
        return OverState;
    }
}
