using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.UI;

namespace CsAccSlot;

public sealed class TestSlotA : ModAccessorySlot
{
}

public sealed class TestSlotB : ModAccessorySlot
{
}

public sealed class CsAccSlotMod : Mod
{
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public;

    static int frame, pass, fail, step, waited, taps, previousLoadout, inventorySlot;
    static Item keptHead;
    static ModAccessorySlot one = null!, wing = null!, testA = null!, other = null!;
    static Rectangle button, panel, next;

    static ILog Log => ModContent.GetInstance<CsAccSlotMod>().Logger;

    static Type PanelType => typeof(ModAccessorySlot).Assembly.GetType("Terraria.ModLoader.Core.AccessorySlotPanel")!;

    static bool probing, vanillaProbe;
    static int vanillaShown;
    static Item keptAccessory;
    static int probed, probedMod, sharedOnMod;
    static string probeSample = "";

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.MouseText_DrawItemTooltip_GetLinesInfo += Probe;
    }

    static void Probe(On_Main.orig_MouseText_DrawItemTooltip_GetLinesInfo orig, Item item, ref int yoyoLogo, ref int researchLine, ref int materialsLine, float oldKB, ref int numLines, GameArray<GameString> toolTipLine, GameArray<bool> preFixLine, GameArray<bool> badPreFixLine, ref int setBonusLine, ref Microsoft.Xna.Framework.Graphics.Color setBonusColour, ref int sharedLine)
    {
        orig(item, ref yoyoLogo, ref researchLine, ref materialsLine, oldKB, ref numLines, toolTipLine, preFixLine, badPreFixLine, ref setBonusLine, ref setBonusColour, ref sharedLine);
        if (vanillaProbe && !item.IsNull && item.type == ItemID.BandofRegeneration) vanillaShown++;
        if (!probing || item.IsNull || item.type != ItemID.HermesBoots) return;
        probed++;
        bool mine = item.Pointer == one.FunctionalItem.Pointer;
        if (mine) probedMod++;
        string lines = string.Join(" | ", Enumerable.Range(0, numLines).Select(k => (string)toolTipLine[k]));
        bool hint = sharedLine >= 0 || lines.Contains(Terraria.Localization.Language.GetTextValue("UI.ItemLoadoutShareHint")) || lines.Contains(Lang.tip[23].Value);
        if (hint) sharedOnMod++;
        if (probed <= 3) Log.Info($"csaccslot probe mine={mine} context={item.tooltipContext} slot={item.tooltipSlot} sharedLine={sharedLine} lines={lines}");
        probeSample = lines;
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csaccslot {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || step == 99)
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

    static ModAccessorySlotPlayer SlotsOf(Player player) =>
        (ModAccessorySlotPlayer)typeof(AccessorySlotLoader).GetMethod("ModSlotPlayer", Hidden)!.Invoke(null, new object[] { player })!;

    static GameArray<Item> Accessories(ModAccessorySlotPlayer slots) =>
        (GameArray<Item>)typeof(ModAccessorySlotPlayer).GetField("exAccessorySlot", Hidden)!.GetValue(slots)!;

    static T PanelValue<T>(string name) => (T)PanelType.GetProperty(name, Hidden)!.GetValue(null)!;

    static int ScrollRow(Player player) => (int)typeof(ModAccessorySlotPlayer).GetField("scrollRow", Hidden)!.GetValue(SlotsOf(player))!;

    static void Wait() => waited = frame;

    static bool After(int frames) => frame >= waited + frames;

    static void Clear()
    {
        foreach (ModAccessorySlot slot in new[] { one, wing, testA })
        {
            slot.FunctionalItem = Item.New();
            slot.VanityItem = Item.New();
            slot.DyeItem = Item.New();
            slot.HideVisuals = false;
        }
    }

    static void Step(Player player)
    {
        int hermesShoe = new Item(ItemID.HermesBoots).shoeSlot;
        switch (step)
        {
            case 0 when frame == 60:
            {
                AccessorySlotLoader loader = LoaderManager.Get<AccessorySlotLoader>();
                one = ModContent.Find<ModAccessorySlot>("ExampleMod/ExampleModAccessorySlot1");
                wing = ModContent.Find<ModAccessorySlot>("ExampleMod/ExampleModWingSlot");
                testA = ModContent.GetInstance<TestSlotA>();
                Report("the Example Mod and test slots are registered", loader.TotalCount >= 5 && ModContent.Find<ModAccessorySlot>("ExampleMod/ExampleCustomLocationAndTextureSlot") != null, $"count={loader.TotalCount}");
                Report("a slot reads its player", ReferenceEquals(one.ModSlotPlayer, SlotsOf(player)) && one.ModSlotPlayer.SlotCount == loader.TotalCount, $"slots={one.ModSlotPlayer.SlotCount}");
                keptHead = player.armor[0];
                Clear();
                one.FunctionalItem = new Item(ItemID.HermesBoots);
                one.DyeItem = new Item(ItemID.RedDye);
                Wait();
                step = 1;
                break;
            }
            case 1 when After(10):
                Report("a functional mod accessory works", player.accRunSpeed >= 6f, $"accRunSpeed={player.accRunSpeed}");
                Report("it shows on the player", player.shoe == hermesShoe, $"shoe={player.shoe} wanted={hermesShoe}");
                Report("the dye in the mod slot colors it", player.cShoe == new Item(ItemID.RedDye).dye, $"cShoe={player.cShoe}");
                one.HideVisuals = true;
                Wait();
                step = 2;
                break;
            case 2 when After(10):
                Report("the eye hides it but keeps the effect", player.shoe != hermesShoe && player.accRunSpeed >= 6f, $"shoe={player.shoe} accRunSpeed={player.accRunSpeed}");
                one.HideVisuals = false;
                one.FunctionalItem = Item.New();
                one.VanityItem = new Item(ItemID.HermesBoots);
                Wait();
                step = 3;
                break;
            case 3 when After(10):
                Report("a vanity mod accessory only shows", player.shoe == hermesShoe && player.accRunSpeed < 6f, $"shoe={player.shoe} accRunSpeed={player.accRunSpeed}");
                Clear();
                player.armor[0] = Item.New();
                wing.FunctionalItem = new Item(ItemID.AngelWings);
                Wait();
                step = 4;
                break;
            case 4 when After(10):
            {
                int angel = new Item(ItemID.AngelWings).wingSlot;
                Report("the wing slot is off without a helmet", player.wingsLogic != angel && !wing.IsEnabled(), $"wingsLogic={player.wingsLogic}");
                player.armor[0] = new Item(ItemID.CopperHelmet);
                Wait();
                step = 5;
                break;
            }
            case 5 when After(10):
            {
                int angel = new Item(ItemID.AngelWings).wingSlot;
                Report("the wing slot works with a helmet", player.wingsLogic == angel, $"wingsLogic={player.wingsLogic} wanted={angel}");
                AccessorySlotLoader loader = LoaderManager.Get<AccessorySlotLoader>();
                testA.FunctionalItem = new Item(ItemID.HermesBoots);
                Report("a mod slot refuses a copy of another mod slot", loader.IsAccessoryInConflict(player, new Item(ItemID.HermesBoots), one.Type, ItemSlot.Context.ModdedAccessorySlot));
                Report("the wing slot refuses boots", loader.IsAccessoryInConflict(player, new Item(ItemID.HermesBoots), wing.Type, ItemSlot.Context.ModdedAccessorySlot));
                Report("a slot accepts a new accessory", !loader.IsAccessoryInConflict(player, new Item(ItemID.LuckyHorseshoe), one.Type, ItemSlot.Context.ModdedAccessorySlot));
                testA.FunctionalItem = Item.New();
                wing.FunctionalItem = Item.New();
                inventorySlot = Enumerable.Range(10, 40).First(k => player.inventory[k].IsAir);
                player.inventory[inventorySlot] = new Item(ItemID.AngelWings);
                ItemSlot.SwapEquip(player.inventory, ItemSlot.Context.InventoryItem, inventorySlot);
                Report("equipping wings from the inventory picks the wing slot", wing.FunctionalItem.type == ItemID.AngelWings && player.inventory[inventorySlot].IsAir, $"slot={wing.FunctionalItem.type} inventory={player.inventory[inventorySlot].type}");
                int vanillaSlot = Enumerable.Range(3, 7).First(k => player.armor[k].IsAir && player.IsItemSlotUnlockedAndUsable(k));
                int secondWings = ItemSlot.PickItemMovementAction(player.armor, ItemSlot.Context.EquipAccessory, vanillaSlot, new Item(ItemID.DemonWings));
                int sameInVanity = ItemSlot.PickItemMovementAction(player.armor, ItemSlot.Context.EquipAccessoryVanity, vanillaSlot + 10, new Item(ItemID.AngelWings));
                int otherAccessory = ItemSlot.PickItemMovementAction(player.armor, ItemSlot.Context.EquipAccessory, vanillaSlot, new Item(ItemID.LuckyHorseshoe));
                Report("a vanilla slot refuses wings while a mod slot has wings", secondWings == -1 && sameInVanity == -1 && otherAccessory != -1, $"wings={secondWings} sameInVanity={sameInVanity} horseshoe={otherAccessory} slot={vanillaSlot}");
                wing.FunctionalItem = Item.New();
                one.FunctionalItem = new Item(ItemID.HermesBoots);
                previousLoadout = player.CurrentLoadoutIndex;
                player.TrySwitchingLoadout((previousLoadout + 1) % 3);
                Wait();
                step = 6;
                break;
            }
            case 6 when After(5):
                Report("another loadout has its own mod slots", one.FunctionalItem.IsAir, $"type={one.FunctionalItem.type}");
                player.TrySwitchingLoadout(previousLoadout);
                Wait();
                step = 7;
                break;
            case 7 when After(5):
            {
                Report("switching back brings the item back", one.FunctionalItem.type == ItemID.HermesBoots, $"type={one.FunctionalItem.type}");
                one.DyeItem = new Item(ItemID.RedDye);
                var data = Main.ActivePlayerFileData;
                Player.SavePlayer(data);
                string file = data.Path + ".bl.json";
                JsonObject? saved = File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject : null;
                Report("the mod slots go into the player save", saved?["bunny:accessorySlots"] is JsonObject, file);
                var loaded = Player.LoadPlayer(data.Path, false);
                var copy = SlotsOf(loaded.Player);
                Report("loading the player brings the slots back", Accessories(copy)[one.Type].type == ItemID.HermesBoots && copy.GetSharedLoadoutItem(ItemSlot.Context.ModdedDyeSlot, one.Type, out _).type == ItemID.RedDye);
                int before = Main.ActiveItems.Count(w => w.type == ItemID.HermesBoots);
                SlotsOf(player).DropItems(player.GetSource_FromThis());
                int after = Main.ActiveItems.Count(w => w.type == ItemID.HermesBoots);
                Report("dying drops the mod slot items", one.FunctionalItem.IsAir && one.DyeItem.IsAir && after == before + 1, $"before={before} after={after}");
                foreach (WorldItem w in Main.ActiveItems.ToArray())
                    if (w.type is ItemID.HermesBoots or ItemID.RedDye) w.TurnToAir();
                one.FunctionalItem = new Item(ItemID.HermesBoots);
                Main.playerInventory = true;
                GUIInstance.Active.GUIPageIcons.OpenUI(GUIPageIcons.Category.Inventory, GUIPageIcons.Category.Equipment);
                Wait();
                step = 8;
                break;
            }
            case 8 when After(60):
                button = PanelValue<Rectangle>("ButtonRegion");
                Report("the mod slot button sits next to the loadout star", button.Width > 0, $"button={button}");
                Log.Info($"csaccslot TAP {++taps} {button.Center.X} {button.Center.Y}");
                Wait();
                step = 9;
                break;
            case 9:
                if (!PanelValue<bool>("Open") && !After(900)) break;
                panel = PanelValue<Rectangle>("PanelRegion");
                Report("tapping the button opens the panel", PanelValue<bool>("Open") && panel.Width > 0, $"panel={panel}");
                Log.Info($"csaccslot SHOT {++taps}");
                Wait();
                step = 20;
                break;
            case 20 when After(60):
            {
                var cells = (System.Collections.IDictionary)PanelType.GetField("CellRegions", Hidden)!.GetValue(null)!;
                var cell = (Rectangle)cells[(one.Type, 2)]!;
                Log.Info($"csaccslot TAP {++taps} {cell.Center.X} {cell.Center.Y}");
                Wait();
                step = 21;
                break;
            }
            case 21:
            {
                var selected = PanelType.GetProperty("SelectedCell", Hidden)!.GetValue(null);
                if (selected == null && !After(900)) break;
                Report("tapping a mod slot selects only that slot", selected is ValueTuple<int, int> s && s.Item1 == one.Type && s.Item2 == 2 && GUIInstance.Active.GUIEquipment.itemPieceSelected_internal >= 0, $"selected={selected} vanilla={GUIInstance.Active.GUIEquipment.itemPieceSelected_internal}");
                probing = true;
                Wait();
                step = 40;
                break;
            }
            case 40 when After(30):
            {
                probing = false;
                Report("a mod slot item's tooltip has no loadout share line and no Equipable line", probed > 0 && sharedOnMod == 0, $"probed={probed} mine={probedMod} shared={sharedOnMod} lines={probeSample}");
                Log.Info($"csaccslot SHOT {++taps}");
                Rectangle eye = PanelValue<Rectangle>("HideShowRegion");
                Log.Info($"csaccslot TAP {++taps} {eye.Center.X} {eye.Center.Y}");
                Wait();
                step = 22;
                break;
            }
            case 22:
                if (!one.HideVisuals && !After(900)) break;
                Report("the row's eye hides the selected mod accessory", one.HideVisuals);
                Log.Info($"csaccslot SHOT {++taps}");
                Rectangle remove = PanelValue<Rectangle>("UnequipRegion");
                Log.Info($"csaccslot TAP {++taps} {remove.Center.X} {remove.Center.Y}");
                Wait();
                step = 23;
                break;
            case 23:
                if (!one.FunctionalItem.IsAir && !After(900)) break;
                Report("Remover takes the selected mod accessory out", one.FunctionalItem.IsAir && player.inventory.AsSpan().ToArray().Any(i => i.type == ItemID.HermesBoots), $"slot={one.FunctionalItem.type}");
                foreach (int k in Enumerable.Range(0, 50))
                    if (player.inventory[k].type == ItemID.HermesBoots) player.inventory[k] = Item.New();
                one.HideVisuals = false;
                one.FunctionalItem = new Item(ItemID.HermesBoots);
                Wait();
                step = 24;
                break;
            case 24 when After(60):
            {
                var cells = (System.Collections.IDictionary)PanelType.GetField("CellRegions", Hidden)!.GetValue(null)!;
                var cell = (Rectangle)cells[(one.Type, 2)]!;
                Log.Info($"csaccslot DRAG {++taps} {cell.Center.X} {cell.Center.Y} 267 185");
                Wait();
                step = 25;
                break;
            }
            case 25:
                if ((!one.FunctionalItem.IsAir || !Main.mouseItem.IsAir || !player.inventory.AsSpan().ToArray().Any(i => i.type == ItemID.HermesBoots)) && !After(900)) break;
                Report("dragging a mod accessory out puts it in the inventory", one.FunctionalItem.IsAir && Main.mouseItem.IsAir && player.inventory.AsSpan().ToArray().Any(i => i.type == ItemID.HermesBoots), $"slot={one.FunctionalItem.type} mouse={Main.mouseItem.type}");
                Log.Info($"csaccslot SHOT {++taps}");
                foreach (int k in Enumerable.Range(0, 50))
                    if (player.inventory[k].type == ItemID.HermesBoots) player.inventory[k] = Item.New();
                one.FunctionalItem = new Item(ItemID.HermesBoots);
                Wait();
                step = 30;
                break;
            case 30 when After(60):
            {
                var cells = (System.Collections.IDictionary)PanelType.GetField("CellRegions", Hidden)!.GetValue(null)!;
                var from = (Rectangle)cells[(one.Type, 2)]!;
                var to = (Rectangle)cells[(one.Type, 0)]!;
                Log.Info($"csaccslot DRAG {++taps} {from.Center.X} {from.Center.Y} {to.Center.X} {to.Center.Y}");
                Wait();
                step = 31;
                break;
            }
            case 31 when After(420):
            {
                int inventory = player.inventory.AsSpan().ToArray().Count(i => i.type == ItemID.HermesBoots);
                int world = Main.ActiveItems.Count(w => w.type == ItemID.HermesBoots);
                var accTemp = (GameArray<Item>)PanelType.GetField("accessoryTemp", Hidden)!.GetValue(null)!;
                var dyeTemp = (GameArray<Item>)PanelType.GetField("dyeTemp", Hidden)!.GetValue(null)!;
                string Find(GameArray<Item> a) => string.Join(",", Enumerable.Range(0, a.Length).Where(k => a[k].type == ItemID.HermesBoots));
                string where = $"slot={one.FunctionalItem.type} dye={one.DyeItem.type} mouse={Main.mouseItem.type} inventory={inventory} world={world} armor=[{Find(player.armor)}] vdye=[{Find(player.dye)}] accTemp=[{Find(accTemp)}] dyeTemp=[{Find(dyeTemp)}] trash={player.trashItem.type}";
                bool kept = one.FunctionalItem.type == ItemID.HermesBoots || Main.mouseItem.type == ItemID.HermesBoots || inventory == 1 || world == 1;
                Report("dropping an accessory on a dye cell keeps it", kept && one.DyeItem.type != ItemID.HermesBoots, where);
                Log.Info($"csaccslot SHOT {++taps}");
                foreach (int k in Enumerable.Range(0, 50))
                    if (player.inventory[k].type == ItemID.HermesBoots) player.inventory[k] = Item.New();
                if (Main.mouseItem.type == ItemID.HermesBoots) Main.mouseItem.TurnToAir();
                one.DyeItem = Item.New();
                one.FunctionalItem = new Item(ItemID.HermesBoots);
                Wait();
                step = 32;
                break;
            }
            case 32 when After(60):
            {
                var cells = (System.Collections.IDictionary)PanelType.GetField("CellRegions", Hidden)!.GetValue(null)!;
                other = wing;
                one.FunctionalItem = new Item(ItemID.AngelWings);
                other.FunctionalItem = new Item(ItemID.DemonWings);
                var from = (Rectangle)cells[(one.Type, 2)]!;
                var to = (Rectangle)cells[(other.Type, 2)]!;
                Log.Info($"csaccslot DRAG {++taps} {from.Center.X} {from.Center.Y} {to.Center.X} {to.Center.Y}");
                Wait();
                step = 33;
                break;
            }
            case 33 when After(420):
            {
                int world = Main.ActiveItems.Count(w => w.type is ItemID.AngelWings or ItemID.DemonWings);
                int inventory = player.inventory.AsSpan().ToArray().Count(i => i.type is ItemID.AngelWings or ItemID.DemonWings);
                Report("dragging between two mod slots swaps them", one.FunctionalItem.type == ItemID.DemonWings && other.FunctionalItem.type == ItemID.AngelWings && Main.mouseItem.IsAir,
                    $"one={one.FunctionalItem.type} other={other.FunctionalItem.type} mouse={Main.mouseItem.type} inventory={inventory} world={world}");
                Log.Info($"csaccslot SHOT {++taps}");
                foreach (int k in Enumerable.Range(0, 50))
                    if (player.inventory[k].type is ItemID.AngelWings or ItemID.DemonWings) player.inventory[k] = Item.New();
                foreach (var w in Main.ActiveItems)
                    if (w.type is ItemID.AngelWings or ItemID.DemonWings) w.TurnToAir();
                if (!Main.mouseItem.IsAir) Main.mouseItem.TurnToAir();
                one.FunctionalItem = new Item(ItemID.AngelWings);
                other.FunctionalItem = new Item(ItemID.DemonWings);
                Wait();
                step = 34;
                break;
            }
            case 34 when After(60):
            {
                var cells = (System.Collections.IDictionary)PanelType.GetField("CellRegions", Hidden)!.GetValue(null)!;
                var from = (Rectangle)cells[(other.Type, 2)]!;
                var to = (Rectangle)cells[(one.Type, 2)]!;
                Log.Info($"csaccslot SWIPE {++taps} {from.Center.X} {from.Center.Y} {to.Center.X} {to.Center.Y}");
                Wait();
                step = 35;
                break;
            }
            case 35 when After(300):
            {
                int world = Main.ActiveItems.Count(w => w.type is ItemID.AngelWings or ItemID.DemonWings);
                int inventory = player.inventory.AsSpan().ToArray().Count(i => i.type is ItemID.AngelWings or ItemID.DemonWings);
                Report("a quick swipe between two mod slots swaps them", one.FunctionalItem.type == ItemID.DemonWings && other.FunctionalItem.type == ItemID.AngelWings && Main.mouseItem.IsAir,
                    $"one={one.FunctionalItem.type} other={other.FunctionalItem.type} mouse={Main.mouseItem.type} inventory={inventory} world={world}");
                Log.Info($"csaccslot SHOT {++taps}");
                foreach (int k in Enumerable.Range(0, 50))
                    if (player.inventory[k].type is ItemID.AngelWings or ItemID.DemonWings) player.inventory[k] = Item.New();
                foreach (var w in Main.ActiveItems)
                    if (w.type is ItemID.AngelWings or ItemID.DemonWings) w.TurnToAir();
                if (!Main.mouseItem.IsAir) Main.mouseItem.TurnToAir();
                other.FunctionalItem = Item.New();
                one.FunctionalItem = new Item(ItemID.HermesBoots);
                Wait();
                step = 26;
                break;
            }
            case 26 when After(60):
            {
                var cells = (System.Collections.IDictionary)PanelType.GetField("CellRegions", Hidden)!.GetValue(null)!;
                var cell = (Rectangle)cells[(one.Type, 2)]!;
                Log.Info($"csaccslot TAP {++taps} {cell.Center.X} {cell.Center.Y}");
                Wait();
                step = 27;
                break;
            }
            case 27:
                if (PanelType.GetProperty("SelectedCell", Hidden)!.GetValue(null) == null && !After(900)) break;
                Log.Info($"csaccslot TAP {++taps} {button.Center.X} {button.Center.Y}");
                Wait();
                step = 28;
                break;
            case 28:
                if (PanelValue<bool>("Open") && !After(900)) break;
                if (!After(120)) break;
                Report("closing the panel with a mod slot selected keeps the game alive", !PanelValue<bool>("Open") && GUIInstance.Active.GUIEquipment.itemPieceSelected_internal >= 0, $"vanilla={GUIInstance.Active.GUIEquipment.itemPieceSelected_internal}");
                Log.Info($"csaccslot TAP {++taps} {button.Center.X} {button.Center.Y}");
                Wait();
                step = 29;
                break;
            case 29:
                if (!PanelValue<bool>("Open") && !After(900)) break;
                Wait();
                step = 50;
                break;
            case 50 when After(60):
            {
                keptAccessory = player.armor[3];
                player.armor[3] = new Item(ItemID.BandofRegeneration);
                one.FunctionalItem = Item.New();
                var cells = (System.Collections.IDictionary)PanelType.GetField("CellRegions", Hidden)!.GetValue(null)!;
                var cell = (Rectangle)cells[(one.Type, 2)]!;
                Log.Info($"csaccslot TAP {++taps} {cell.Center.X} {cell.Center.Y}");
                Wait();
                step = 51;
                break;
            }
            case 51:
                if (PanelType.GetProperty("SelectedCell", Hidden)!.GetValue(null) == null && !After(900)) break;
                vanillaShown = 0;
                vanillaProbe = true;
                Wait();
                step = 52;
                break;
            case 52 when After(60):
                vanillaProbe = false;
                Report("an empty mod slot does not show the vanilla accessory's tooltip", vanillaShown == 0, $"vanilla tooltips={vanillaShown} hover={Main.HoverItem.type}");
                Log.Info($"csaccslot SHOT {++taps}");
                player.armor[3] = keptAccessory;
                one.FunctionalItem = new Item(ItemID.HermesBoots);
                Wait();
                step = 10;
                break;
            case 10 when After(120):
                next = PanelValue<Rectangle>("NextPageRegion");
                Report("more than two slots show the page buttons", next.Width > 0, $"next={next}");
                Log.Info($"csaccslot TAP {++taps} {next.Center.X} {next.Center.Y}");
                Wait();
                step = 11;
                break;
            case 11:
                if (ScrollRow(player) == 0 && !After(900)) break;
                Report("the next page button shows the next two slots", ScrollRow(player) == 2, $"row={ScrollRow(player)}");
                Log.Info($"csaccslot SHOT {++taps}");
                Wait();
                step = 12;
                break;
            case 12 when After(120):
                Log.Info($"csaccslot TAP {++taps} {button.Center.X} {button.Center.Y}");
                Wait();
                step = 13;
                break;
            case 13:
                if (PanelValue<bool>("Open") && !After(900)) break;
                Report("tapping the button again closes it", !PanelValue<bool>("Open"));
                GUIInstance.Active.GUIPageIcons.Close();
                Main.playerInventory = false;
                Clear();
                player.armor[0] = keptHead;
                Finish();
                break;
        }
    }

    static void Finish()
    {
        step = 99;
        Log.Info($"csaccslot END pass={pass} fail={fail}");
    }
}
