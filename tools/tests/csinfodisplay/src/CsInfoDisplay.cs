using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsInfoDisplay;

public sealed class CsInfoDisplayMod : Mod
{
    static int frame, pass, fail, step, waited, taps, vanilla;
    static Item keptGps, keptInfo;
    static readonly Dictionary<int, Vector2> drawn = new();
    static readonly Dictionary<int, Vector2> toggles = new();

    static ILog Log => ModContent.GetInstance<CsInfoDisplayMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUIAccessoryInfo.ItemDraw += ItemDraw;
        On_GUIAccessoryInfoToggles.InfoAccDraw += InfoAccDraw;
    }

    static void InfoAccDraw(On_GUIAccessoryInfoToggles.orig_InfoAccDraw orig, GUIAccessoryInfoToggles self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        toggles[index] = position;
        orig(self, gridLayout, index, position, scale);
    }

    static void ItemDraw(On_GUIAccessoryInfo.orig_ItemDraw orig, GUIAccessoryInfo self, ItemGrid_Layout gridLayout, int i, Vector2 position, float scale)
    {
        drawn[self.itemOffset + i] = position;
        orig(self, gridLayout, i, position, scale);
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csinfodisplay {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        self.creativeGodMode = true;
        frame++;
        try
        {
            Step(self);
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            Finish();
        }
    }

    static void TapInfoIcon()
    {
        Log.Info($"csinfodisplay TAP {++taps} 965 24");
        waited = frame;
    }

    static bool Hidden(Player player)
    {
        var hooks = typeof(InfoDisplay).Assembly.GetType("Terraria.ModLoader.Core.InfoDisplayHooks")!;
        var isHidden = hooks.GetMethod("IsHidden", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (bool)isHidden.Invoke(null, new object[] { player, ModContent.Find<InfoDisplay>("ExampleMod/ExampleInfoDisplay") })!;
    }

    static void TapToggle()
    {
        Vector2 at = toggles[vanilla] + new Vector2(18f, 18f);
        Log.Info($"csinfodisplay TAP {++taps} {at.X:0} {at.Y:0}");
        waited = frame;
    }

    static void Toggle(int index)
    {
        Main.mouseLeft = true;
        Main.mouseLeftRelease = true;
        GUIInstance.Active.GUIAccessoryInfoToggles.InfoAccOver(index);
        Main.mouseLeft = false;
        Main.mouseLeftRelease = false;
        waited = frame;
    }

    static int Rebuild(GUIAccessoryInfo info, bool inventory)
    {
        bool open = Main.playerInventory;
        Main.playerInventory = inventory;
        info.lastRebuild = -1;
        info.BuildInfoAccs();
        Main.playerInventory = open;
        return info.numInfoAccessories;
    }

    static void Step(Player self)
    {
        GUIAccessoryInfo info = GUIInstance.Active.GUIAccessoryInfo;
        switch (step)
        {
            case 0 when frame == 30:
                keptGps = self.inventory[48].Clone();
                keptInfo = self.inventory[49].Clone();
                self.inventory[48] = new Item(ItemID.GPS);
                self.inventory[49] = new Item(ModContent.Find<ModItem>("ExampleMod/ExampleInfoAccessory").Type);
                Main.playerInventory = false;
                TapInfoIcon();
                step = 1;
                break;
            case 1 when frame == waited + 120:
                drawn.Clear();
                step = 2;
                waited = frame;
                break;
            case 2 when frame == waited + 10:
            {
                int count = info.numInfoAccessories;
                vanilla = count - 1;
                Report("the info list counts the mod display", count == 4, $"count={count}");
                bool aligned = drawn.ContainsKey(vanilla) && Enumerable.Range(0, vanilla).All(k => drawn.TryGetValue(k, out Vector2 at) && Math.Abs(at.X - drawn[vanilla].X) < 1f);
                float gap = vanilla > 1 ? drawn[1].Y - drawn[0].Y : 0f;
                bool below = drawn.ContainsKey(vanilla) && vanilla > 0 && Math.Abs(drawn[vanilla].Y - drawn[vanilla - 1].Y - gap) < 1f;
                Report("the mod display sits in the game's info column", aligned && below, string.Join(" ", drawn.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value.X:0},{p.Value.Y:0}")));
                Log.Info($"csinfodisplay culture={Language.ActiveCulture.Name} text={Language.GetTextValue("Mods.ExampleMod.InfoDisplays.ExampleInfoDisplay.NoMinions")}");
                Log.Info($"csinfodisplay SHOT {++taps}");
                waited = frame;
                step = 21;
                break;
            }
            case 21 when frame == waited + 600:
                Toggle(vanilla);
                step = 3;
                break;
            case 3 when frame == waited + 30:
            {
                Report("hiding the mod display removes it from the HUD", Rebuild(info, false) == vanilla, $"count={info.numInfoAccessories}");
                int hiddenOpen = Rebuild(info, true);
                Toggle(vanilla);
                int shownOpen = Rebuild(info, true);
                Report("a hidden mod display stays in the toggle list with the inventory open", hiddenOpen == shownOpen && hiddenOpen > 0, $"hidden={hiddenOpen} shown={shownOpen}");
                Report("showing it again brings it back", Rebuild(info, false) == vanilla + 1, $"count={info.numInfoAccessories}");
                TapInfoIcon();
                step = 6;
                break;
            }
            case 6 when frame == waited + 90:
                TapInfoIcon();
                step = 7;
                break;
            case 7 when frame == waited + 90:
                Main.playerInventory = true;
                GUIInstance.Active.GUIPageIcons.OpenUI(GUIPageIcons.Category.Inventory, GUIPageIcons.Category.Equipment);
                waited = frame;
                step = 8;
                break;
            case 8 when frame == waited + 30:
            {
                GUIAccessoryInfoToggles row = GUIInstance.Active.GUIAccessoryInfoToggles;
                row.Expanded = true;
                toggles.Clear();
                waited = frame;
                step = 9;
                break;
            }
            case 9 when frame == waited + 30:
            {
                Report("the accessory tab draws the mod display among the info toggles", toggles.ContainsKey(vanilla), string.Join(" ", toggles.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value.X:0},{p.Value.Y:0}")));
                var popup = Terraria.GameContent.TextureAssets.InfoPopup[0];
                Log.Info($"csinfodisplay popup={(popup == null || !popup.IsLoaded ? "-" : $"{popup.Value.Width}x{popup.Value.Height}")}");
                Log.Info($"csinfodisplay SHOT {++taps}");
                waited = frame;
                step = 10;
                break;
            }
            case 10 when frame == waited + 600:
                TapToggle();
                toggles.Clear();
                step = 11;
                break;
            case 11:
                if (!Hidden(self) && frame < waited + 900) break;
                Report("tapping the mod toggle in the accessory tab hides the display", Hidden(self) && toggles.ContainsKey(vanilla), $"toggles={toggles.Count}");
                Log.Info($"csinfodisplay SHOT {++taps}");
                waited = frame;
                step = 12;
                break;
            case 12 when frame == waited + 600:
                TapToggle();
                toggles.Clear();
                step = 13;
                break;
            case 13:
            {
                if (Hidden(self) && frame < waited + 900) break;
                Report("tapping it again shows the display", !Hidden(self), $"toggles={toggles.Count}");
                GUIAccessoryInfoToggles row = GUIInstance.Active.GUIAccessoryInfoToggles;
                row.Expanded = false;
                GUIInstance.Active.GUIPageIcons.Close();
                Main.playerInventory = false;
                self.inventory[48] = keptGps;
                self.inventory[49] = keptInfo;
                Finish();
                break;
            }
        }
    }

    static void Finish()
    {
        step = 99;
        Log.Info($"csinfodisplay END pass={pass} fail={fail}");
    }
}
