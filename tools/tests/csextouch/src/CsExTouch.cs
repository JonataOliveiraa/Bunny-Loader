using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ContolType = VirtualControlsHardwareConfigurationMapping_Layout.ContolType;

namespace CsExTouch;

public sealed class CsExTouchMod : Mod
{
    const int End = 9000;
    static VirtualControlsProfile_Layout original;
    static int frame, pass, fail, spawned = -1, projectilesSeen;
    static bool finished, wasTouch, wasHold, wasAim;
    static int touches, holds, aims;
    static readonly List<(VirtualControlsBinding_Layout Binding, ContolType Action)> Changed = new();

    static ILog Log => ModContent.GetInstance<CsExTouchMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csextouch {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static ModTouchButton Button(string name)
    {
        foreach (ModTouchButton button in TouchButtonLoader.Buttons)
            if (button.FullName == "ExampleMod/" + name) return button;
        throw new KeyNotFoundException(name);
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
        try { Step(self); }
        catch (Exception e) { Report("step", false, e.ToString()); }
    }

    static void Talk(Player player, int type, string shot)
    {
        if (spawned >= 0) Main.npc[spawned].active = false;
        spawned = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 60, (int)player.Center.Y - 60, type);
        player.SetTalkNPC(spawned);
        Main.npcChatText = Main.npc[spawned].GetChat() ?? "";
        Log.Info($"csextouch SHOT {shot}");
    }

    static void Panel(string name, Panel_Layout l) =>
        Log.Info($"csextouch layout {name} anchor={l.AnchorControl}/{l.Anchor} loc={l.Location} size={l.Size} sizeBy={l.AnchorForSize}/{l.SizeAnchorControl}/{l.SizeAnchor}/{l.SizeLocation} region={GUIPanel.Region(l)}");

    static void Button2(string name, TransactionButton_Layout l) =>
        Log.Info($"csextouch layout {name} anchor={l.AnchorControl}/{l.Anchor} loc={l.Location} region={GUITransactionButton.GetRegion(l)} pick={GUITransactionButton.GetPickRegion(l)}");

    static void Text(string name, String_Layout l) =>
        Log.Info($"csextouch layout {name} anchor={l.AnchorControl}/{l.Anchor} loc={l.Location} wrap={l.WrapWidth} lines={l.MaxLines} align={l.Alignment} scale={l.Scale}");

    static void Dump()
    {
        NPCDialogue_Layout d = NPCDialogue_Layout.Instance;
        Log.Info($"csextouch layout numbers padding={d.BackingTextPadding} min={d.MinTextHeight} offset={d.TextBackingOffset} lastLine={d.MaxWidthLastLineAddition} screen={Main.screenWidth}x{Main.screenHeight}");
        Panel("Backing", d.Backing);
        Panel("TextBacking", d.TextBacking);
        Panel("MenuDivider", d.MenuDivider);
        Panel("ProfileBacking", d.NPCProfileBacking);
        Panel("ProfileImage", d.NPCProfileImage);
        Panel("NameDivider", d.DialogueItemNameDivider);
        Text("Cost", d.Option1Cost);
        Text("Text", d.Text);
        Text("Name", d.DialogueItemName);
        Button2("Close", d.Close);
        Button2("Happiness", d.Happiness);
        Button2("Option1", d.Option1);
        Button2("Option2", d.Option2);
    }

    static void Bind(ContolType from, ModTouchButton button)
    {
        VirtualControlsProfile_Layout profile = VirtualControlsStyles_Layout.Instance.ActiveProfile;
        GameArray<VirtualControlsBinding_Layout> controls = profile.Controls;
        for (int k = 0; k < controls.Length; k++)
        {
            VirtualControlsBinding_Layout binding = controls[k];
            if (binding.IsNull || binding.Action != from) continue;
            Changed.Add((binding, from));
            binding.Action = (ContolType)TouchButtonLoader.ActionOf(button);
            Log.Info($"csextouch slot {k}: {from} -> {button.FullName}");
            return;
        }
        Report($"slot for {from}", false);
    }

    static void Step(Player player)
    {
        if (frame == 120)
        {
            ModTouchButton aim = Button("ExampleAimJoystick");
            Report("joystick type", aim is ModTouchJoystick);
            VirtualControlsStyles_Layout styles = VirtualControlsStyles_Layout.Instance;
            original = styles.ActiveProfile;
            foreach (VirtualControlsProfile_Layout profile in (VirtualControlsProfile_Layout[])styles.Profiles)
            {
                bool hasAxis = false;
                foreach (VirtualControlsBinding_Layout binding in (VirtualControlsBinding_Layout[])profile.Controls)
                    if (!binding.IsNull && binding.Action == ContolType.Aim) hasAxis = true;
                if (!hasAxis) continue;
                styles.SetActiveProfile(profile);
                Log.Info($"csextouch profile {profile.Id}");
                break;
            }
            Bind(ContolType.Aim, aim);
            Bind(ContolType.Mount, Button("ExampleHoldButton"));
            Bind(ContolType.ZoomIn, Button("ExampleTouchButton"));
            Report("sample set OnRelease", ((ModTouchJoystick)aim).OnRelease != null);
            Log.Info("csextouch READY");
        }
        if (frame < 120) return;
        ModTouchButton touch = Button("ExampleTouchButton"), hold = Button("ExampleHoldButton");
        var stick = (ModTouchJoystick)Button("ExampleAimJoystick");
        if (touch.Current && !wasTouch) { touches++; Log.Info($"csextouch touch #{touches}"); }
        if (hold.Current && !wasHold) { holds++; Log.Info($"csextouch hold start #{holds}"); }
        if (!hold.Current && wasHold) Log.Info("csextouch hold end");
        if (stick.Current && !wasAim) { aims++; Log.Info($"csextouch aim start #{aims} dir={stick.Direction}"); }
        if (!stick.Current && wasAim) Log.Info("csextouch aim end");
        if (stick.Current && frame % 30 == 0) Log.Info($"csextouch aim dir={stick.Direction}");
        wasTouch = touch.Current;
        wasHold = hold.Current;
        wasAim = stick.Current;
        int type = ModContent.Find<ModProjectile>("ExampleMod/SparklingBall").Type;
        for (int k = 0; k < Main.maxProjectiles; k++)
        {
            Projectile p = Main.projectile[k];
            if (p.active && p.type == type && p.timeLeft > 0 && p.ai[1] != 7777f)
            {
                p.ai[1] = 7777f;
                projectilesSeen++;
                Log.Info($"csextouch shot #{projectilesSeen} velocity={p.velocity}");
            }
        }
        if (frame == 300) Talk(player, NPCID.Guide, "guide");
        if (frame == 900) Talk(player, ModContent.Find<ModNPC>("ExampleMod/ExampleBoneMerchant").Type, "bone");
        if (frame == 1500) Talk(player, ModContent.Find<ModNPC>("ExampleMod/ExamplePerson").Type, "person");
        if (frame == 300 + 120 || frame == 1500 + 120) Dump();
        if (frame < End) return;
        foreach (var (binding, action) in Changed) binding.Action = action;
        VirtualControlsStyles_Layout.Instance.SetActiveProfile(original);
        if (spawned >= 0)
        {
            player.SetTalkNPC(-1);
            Main.npc[spawned].active = false;
        }
        Report("tap fired", touches > 0, $"touches={touches}");
        Report("hold fired", holds > 0, $"holds={holds}");
        Report("joystick fired", aims > 0, $"aims={aims}");
        Report("joystick shot", projectilesSeen > 0, $"shots={projectilesSeen}");
        finished = true;
        Log.Info($"csextouch END pass={pass} fail={fail}");
    }
}
