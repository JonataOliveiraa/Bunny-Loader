using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using ContolType = VirtualControlsHardwareConfigurationMapping_Layout.ContolType;

namespace CsAnimButton;

public sealed class CsAnimButtonMod : Mod
{
    const int LoadingHeight = 16 * 50, PenetratorHeight = 840;

    static ModTouchButton? penetrator;
    static ModTouchJoystick? stick;
    static VirtualControlsProfile_Layout original;
    static readonly List<(VirtualControlsBinding_Layout Binding, ContolType Action)> Changed = new();
    static readonly Dictionary<string, HashSet<int>> Seen = new();
    static readonly Dictionary<string, Rectangle> Source = new(), Region = new();
    static int frame, step, waited, taps, shots, pass, fail, held;
    static bool finished, listed;

    static ILog Log => ModContent.GetInstance<CsAnimButtonMod>().Logger;

    public override void Load()
    {
        penetrator = TouchButtonLoader.RegisterButton(this, "Penetrator");
        stick = TouchButtonLoader.RegisterJoystick(this, "PenetratorStick");
        stick.Icon = "Assets/TouchButtons/Penetrator";
        On_Player.Update += Update;
        On_GUIQuickActionButton.DrawFrame_QuickActionButton_Layout_Texture2D_Rectangle_InteractionState_reffloat += DrawFrame;
        On_GUIAxis.Draw_Axis_Layout_Texture2D_Rectangle_refVector2_refVector2_bool_bool_bool_int += DrawAxis;
    }

    public override void PostSetupContent()
    {
        penetrator!.Animation = new DrawAnimationVertical(5, 10);
        stick!.Animation = new DrawAnimationVertical(5, 10, true);
    }

    static void DrawAxis(On_GUIAxis.orig_Draw_Axis_Layout_Texture2D_Rectangle_refVector2_refVector2_bool_bool_bool_int orig, Axis_Layout layout, Texture2D icon, Rectangle srcRect,
        ref Vector2 innerScreenOffset, ref Vector2 centre, bool active, bool useAlternativeInner, bool disabled, int itemType)
    {
        orig(layout, icon, srcRect, ref innerScreenOffset, ref centre, active, useAlternativeInner, disabled, itemType);
        if (icon.IsNull || icon.Height != PenetratorHeight) return;
        Remember("stick", srcRect);
    }

    static void Remember(string name, Rectangle srcRect)
    {
        if (!Seen.TryGetValue(name, out HashSet<int>? seen)) Seen[name] = seen = new HashSet<int>();
        seen.Add(srcRect.Y);
        Source[name] = srcRect;
    }

    static int Index(ModTouchButton button)
    {
        for (int k = 0; k < TouchButtonLoader.Buttons.Count; k++)
            if (TouchButtonLoader.Buttons[k] == button) return k;
        return -1;
    }

    static void Lists()
    {
        ModTouchButton loading = Button("ExampleMod/ExampleAnimatedButton");
        GUIQuickActionButton.GetIconFrame((QuickActionButton_Layout.QuickActionControl)(100 + Index(loading)), out Texture2D icon, out Rectangle quick);
        Remember("quick icon", quick);
        bool first = !listed;
        listed = true;
        if (first) Report("quick icon texture", !icon.IsNull && icon.Height == LoadingHeight, $"{(icon.IsNull ? 0 : icon.Height)}");
        GUIVirtualInputControllerEdit editor = GUIInstance.Active.GUIVirtualInputControllerEdit;
        if (editor.IsNull) return;
        editor.GetControlTextureFrame((ContolType)TouchButtonLoader.ActionOf(loading), out Texture2D editorIcon, out Rectangle frame);
        Remember("editor icon", frame);
        if (first) Report("editor icon texture", !editorIcon.IsNull && editorIcon.Height == LoadingHeight, $"{(editorIcon.IsNull ? 0 : editorIcon.Height)}");
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csanimbutton {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static ModTouchButton Button(string fullName) => TouchButtonLoader.Buttons.First(b => b.FullName == fullName);

    static DrawAnimationVertical Loading => (DrawAnimationVertical)Button("ExampleMod/ExampleAnimatedButton").Animation!.Value;

    static void DrawFrame(On_GUIQuickActionButton.orig_DrawFrame_QuickActionButton_Layout_Texture2D_Rectangle_InteractionState_reffloat orig,
        QuickActionButton_Layout layout, Texture2D texture, Rectangle srcRect, GUIQuickActionButton.InteractionState state, ref float scale)
    {
        orig(layout, texture, srcRect, state, ref scale);
        try
        {
            string? name = texture.IsNull ? null : texture.Height == LoadingHeight ? "loading" : texture.Height == PenetratorHeight ? "penetrator" : null;
            if (name == null) return;
            Remember(name, srcRect);
            Region[name] = GUIQuickActionButton.GetRegion(layout);
        }
        catch (Exception e) { Report("draw hook", false, e.ToString()); }
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (Main.gameMenu)
        {
            frame = 0;
            orig(self, i);
            return;
        }
        if (i == Main.myPlayer && !finished)
        {
            self.creativeGodMode = true;
            self.fallStart = (int)(self.position.Y / 16f);
        }
        orig(self, i);
        if (i != Main.myPlayer || finished) return;
        frame++;
        try { Step(); }
        catch (Exception e)
        {
            Report("step", false, e.ToString());
            Finish();
        }
    }

    static void Bind(ContolType from, ModTouchButton button)
    {
        GameArray<VirtualControlsBinding_Layout> controls = VirtualControlsStyles_Layout.Instance.ActiveProfile.Controls;
        for (int k = 0; k < controls.Length; k++)
        {
            VirtualControlsBinding_Layout binding = controls[k];
            if (binding.IsNull || binding.Action != from) continue;
            Changed.Add((binding, from));
            binding.Action = (ContolType)TouchButtonLoader.ActionOf(button);
            Log.Info($"csanimbutton slot {k}: {from} -> {button.FullName}");
            return;
        }
        Report($"slot for {from}", false);
    }

    static void Tap(string name)
    {
        Rectangle region = Region[name];
        Log.Info($"csanimbutton TAP {++taps} {region.X + region.Width / 2f:0.0} {region.Y + region.Height / 2f:0.0} region={region}");
    }

    static void Shot(string what) => Log.Info($"csanimbutton SHOT {++taps} {what} shot{++shots} loadingFrame={Loading.Frame}");

    static void Step()
    {
        switch (step)
        {
            case 0 when frame == 120:
            {
                VirtualControlsStyles_Layout styles = VirtualControlsStyles_Layout.Instance;
                original = styles.ActiveProfile;
                foreach (VirtualControlsProfile_Layout profile in (VirtualControlsProfile_Layout[])styles.Profiles)
                {
                    if (!((VirtualControlsBinding_Layout[])profile.Controls).Any(b => !b.IsNull && b.Action == ContolType.Aim)) continue;
                    styles.SetActiveProfile(profile);
                    Log.Info($"csanimbutton profile {profile.Id}");
                    break;
                }
                Bind(ContolType.ZoomIn, Button("ExampleMod/ExampleAnimatedButton"));
                Bind(ContolType.Mount, penetrator!);
                Bind(ContolType.Aim, stick!);
                Report("sample has an animation", Button("ExampleMod/ExampleAnimatedButton").Animation is { IsNull: false });
                Report("test button has an animation", penetrator!.Animation is { IsNull: false });
                Log.Info("csanimbutton READY");
                Seen.Clear();
                waited = frame;
                step = 1;
                break;
            }
            case 1 when frame < waited + 180:
                if (frame % 10 == 0) Lists();
                break;
            case 1:
            {
                foreach (string name in new[] { "loading", "penetrator", "stick", "quick icon", "editor icon" })
                {
                    int count = Seen.TryGetValue(name, out HashSet<int>? seen) ? seen.Count : 0;
                    string ys = seen == null ? "" : string.Join(",", seen.OrderBy(v => v));
                    Report($"{name} drawn in frames", count >= 5, $"distinct={count} y=[{ys}]");
                }
                Report("loading frame size", Source.TryGetValue("loading", out Rectangle l) && l.Width == 48 && l.Height == 48 && l.Y % 50 == 0, $"{l}");
                Report("penetrator frame size", Source.TryGetValue("penetrator", out Rectangle p) && p.Width == 82 && p.Height == 82 && p.Y % 84 == 0, $"{p}");
                Report("stick frame size", Source.TryGetValue("stick", out Rectangle j) && j.Width == 82 && j.Height == 82 && j.Y % 84 == 0, $"{j}");
                Report("editor frame size", Source.TryGetValue("editor icon", out Rectangle e) && e.Width == 48 && e.Height == 48, $"{e}");
                Log.Info($"csanimbutton regions loading={Region.GetValueOrDefault("loading")} penetrator={Region.GetValueOrDefault("penetrator")}");
                Shot("running");
                waited = frame;
                step = 2;
                break;
            }
            case 2 when frame == waited + 7:
                Shot("running again");
                waited = frame;
                step = 3;
                break;
            case 3 when frame == waited + 30:
                Report("not paused before tap", !Loading.NotActuallyAnimating);
                Tap("loading");
                waited = frame;
                step = 4;
                break;
            case 4:
                if (Loading.NotActuallyAnimating)
                {
                    held = Loading.Frame;
                    waited = frame;
                    step = 5;
                }
                else if (frame > waited + 600)
                {
                    Report("tap pauses", false, "NotActuallyAnimating never turned on");
                    Finish();
                }
                break;
            case 5 when frame == waited + 60:
                Report("tap pauses", Loading.Frame == held, $"held={held} now={Loading.Frame}");
                Shot("paused");
                waited = frame;
                step = 6;
                break;
            case 6 when frame == waited + 30:
                Tap("loading");
                waited = frame;
                step = 7;
                break;
            case 7:
                if (!Loading.NotActuallyAnimating)
                {
                    held = Loading.Frame;
                    waited = frame;
                    step = 8;
                }
                else if (frame > waited + 600)
                {
                    Report("second tap resumes", false, "NotActuallyAnimating stayed on");
                    Finish();
                }
                break;
            case 8 when frame == waited + 60:
                Report("second tap resumes", Loading.Frame != held, $"before={held} now={Loading.Frame}");
                Finish();
                break;
        }
    }

    static void Finish()
    {
        foreach (var (binding, action) in Changed) binding.Action = action;
        if (!original.IsNull) VirtualControlsStyles_Layout.Instance.SetActiveProfile(original);
        finished = true;
        Log.Info($"csanimbutton END pass={pass} fail={fail}");
    }
}
