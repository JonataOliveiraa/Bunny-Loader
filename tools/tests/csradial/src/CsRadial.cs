using System;
using System.Linq;
using BunnyLoader.Interop;
using log4net;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using ContolType = VirtualControlsHardwareConfigurationMapping_Layout.ContolType;

namespace CsRadial;

public sealed class RadialSystem : ModSystem
{
    public static ModTouchButton A = null!, B = null!;

    public override void Load()
    {
        A = TouchButtonLoader.RegisterButton(Mod, "RadialA");
        B = TouchButtonLoader.RegisterButton(Mod, "RadialB");
    }
}

public sealed class RadialPlayer : ModPlayer
{
    public static int PressesA, PressesB;

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (RadialSystem.A.JustPressed) PressesA++;
        if (RadialSystem.B.JustPressed) PressesB++;
    }
}

public sealed class CsRadialMod : Mod
{
    static int frame, pass, fail;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csradial {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static ILog Log => ModContent.GetInstance<CsRadialMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer) return;
        self.creativeGodMode = true;
        frame++;
        try
        {
            if (frame == 40) Survey();
            if (frame == 60) Log.Info("csradial SHOT 1");
            if (frame > 40) Watch();
            if (frame >= 150 && direction < Directions.Length) Drive();
            if (direction == Directions.Length && finished == 0)
            {
                finished = frame;
                Restore();
                Report("the group holds the mod buttons", !advanced.IsNull, $"profile={(advanced.IsNull ? "-" : advanced.Id)}");
                Report("picking a mod button in the radial runs it once", RadialPlayer.PressesA == 1 && RadialPlayer.PressesB == 1, $"A={RadialPlayer.PressesA} B={RadialPlayer.PressesB}");
                Log.Info($"csradial END pass={pass} fail={fail}");
            }
        }
        catch (Exception e) { Log.Info($"csradial frame {frame}: FAIL {e}"); }
    }

    static int direction, attempt, sent = -1, finished, taps = 1;
    static bool opened;

    static void Drive()
    {
        if (sent < 0)
        {
            (float dx, float dy) = Directions[direction];
            Log.Info($"csradial DRAG {++taps} {CentreX} {CentreY} {CentreX + dx} {CentreY + dy}");
            sent = frame;
            opened = false;
            return;
        }
        GUIVirtualInputController.GroupControlState state = GroupState("S11");
        bool closed = state.IsNull || state.CurrentState == GUIVirtualInputController.GroupControlState.State.Closed;
        if (!closed) opened = true;
        if (opened && closed && frame > sent + 30)
        {
            direction++;
            attempt = 0;
            sent = -1;
        }
        else if (!opened && frame > sent + 900)
        {
            if (++attempt > 2) direction++;
            sent = -1;
        }
    }

    static GUIVirtualInputController.GroupControlState GroupState(string id)
    {
        var states = new GameDictionary(GUIInstance.Active.GUIVirtualInputController.GroupStates);
        using var key = new GameHandle(GameString.New(id));
        return states.TryGetValue(key.Target, out GUIVirtualInputController.GroupControlState state) ? state : default;
    }

    const float CentreX = 233f, CentreY = 420f;
    static readonly (float, float)[] Directions = { (0f, -64f), (64f, 0f), (0f, 64f), (-64f, 0f) };
    static VirtualControlsProfile_Layout original, advanced;
    static GameArray<ContolType> group;
    static ContolType[] kept = Array.Empty<ContolType>();
    static string last = "";

    static void Watch()
    {
        GUIVirtualInputController controller = GUIInstance.Active.GUIVirtualInputController;
        var states = new GameDictionary(controller.GroupStates);
        string text = "";
        foreach (string id in new[] { "S11", "S12" })
        {
            using var key = new GameHandle(GameString.New(id));
            text += states.TryGetValue(key.Target, out GUIVirtualInputController.GroupControlState state) && !state.IsNull ? $" {id}={state.pressedIndex}/{state.CurrentState}" : $" {id}=-";
        }
        text += $" A={RadialPlayer.PressesA} B={RadialPlayer.PressesB}";
        if (text == last) return;
        last = text;
        Log.Info($"csradial watch f={frame}{text}");
    }

    static void Restore()
    {
        if (group.IsNull) return;
        for (int k = 0; k < kept.Length; k++) group[k] = kept[k];
        if (!original.IsNull) VirtualControlsStyles_Layout.Instance.SetActiveProfile(original);
    }

    static void Survey()
    {
        VirtualControlsStyles_Layout styles = VirtualControlsStyles_Layout.Instance;
        original = styles.ActiveProfile;
        foreach (VirtualControlsProfile_Layout profile in styles.Profiles.AsSpan().ToArray())
        {
            if (profile.IsNull || profile.HardwareConfig.IsNull) continue;
            var slots = profile.HardwareConfig.Slots;
            for (int k = 0; k < profile.Controls.Length && k < slots.Length; k++)
            {
                VirtualControlsBinding_Layout binding = profile.Controls[k];
                if (slots[k].Style.IsNull || slots[k].Style.ControlType != VirtualControlsSlotTypeMapping_Layout.SlotType.Group) continue;
                string group = binding.GroupActions.IsNull ? "" : string.Join(",", binding.GroupActions.AsSpan().ToArray().Select(a => (int)a));
                Log.Info($"csradial profile {profile.Id} slot {k} id={slots[k].SlotId} group=[{group}]");
                if (profile.Id == "Advanced" && slots[k].SlotId == "S11")
                {
                    advanced = profile;
                    CsRadialMod.group = binding.GroupActions;
                }
            }
        }
        if (advanced.IsNull) return;
        kept = CsRadialMod.group.AsSpan().ToArray();
        CsRadialMod.group[0] = (ContolType)TouchButtonLoader.ActionOf(RadialSystem.A);
        CsRadialMod.group[3] = (ContolType)TouchButtonLoader.ActionOf(RadialSystem.B);
        styles.SetActiveProfile(advanced);
        Log.Info($"csradial group now [{string.Join(",", CsRadialMod.group.AsSpan().ToArray().Select(a => (int)a))}] active={styles.ActiveProfile.Id}");
    }
}
