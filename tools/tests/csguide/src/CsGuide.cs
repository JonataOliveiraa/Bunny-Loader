using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsGuide;

public sealed class CsGuideMod : Mod
{
    static int frame, pass, fail, step, waited, guide, goblin, shop, slot = -1, gelSlot = -1, taps;
    static Item keptOre, keptGel;

    static ILog Log => ModContent.GetInstance<CsGuideMod>().Logger;

    static Rectangle option1, option2;
    static string label1 = "", label2 = "";

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUINPCDialogue.SetupButtonText += SetupButtonText;
    }

    static void SetupButtonText(On_GUINPCDialogue.orig_SetupButtonText orig, GUINPCDialogue self, ref GameString text, ref Microsoft.Xna.Framework.Graphics.Texture2D texture,
        ref GameString text2, ref Microsoft.Xna.Framework.Graphics.Texture2D texture2, ref int cost, ref bool happiness)
    {
        orig(self, ref text, ref texture, ref text2, ref texture2, ref cost, ref happiness);
        option1 = GUITransactionButton.GetRegion(NPCDialogue_Layout.Instance.Option1);
        option2 = GUITransactionButton.GetRegion(NPCDialogue_Layout.Instance.Option2);
        string? first = text, second = text2;
        label1 = first ?? "";
        label2 = second ?? "";
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csguide {name}: {(ok ? "ok" : "FAIL")} {detail}");
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

    static int Floor(int x, int fromY)
    {
        for (int y = fromY; y < Main.maxTilesY - 10; y++)
            if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType])
                return y;
        return fromY;
    }

    static void Talk(int npc)
    {
        if (Main.playerInventory)
            Main.LocalPlayer.ToggleInv();
        Main.playerInventory = false;
        Main.npc[npc].Bottom = Main.LocalPlayer.Bottom + new Vector2(40, 0);
        Main.npc[npc].velocity = Vector2.Zero;
        Main.LocalPlayer.SetTalkNPC(npc);
        Main.npcChatText = Main.npc[npc].GetChat();
        NPC target = Main.npc[npc];
        Log.Info($"csguide talk {npc} type={target.type} active={target.active} town={target.townNPC} chat={Main.LocalPlayer.talkNPC} text={Main.npcChatText}");
        waited = frame;
    }

    static void Tap(Rectangle region)
    {
        Log.Info($"csguide TAP {++taps} {region.Center.X} {region.Center.Y} inventory={Main.playerInventory} chat={Main.LocalPlayer.talkNPC}");
        waited = frame;
    }

    static void CloseChat()
    {
        Main.LocalPlayer.SetTalkNPC(-1);
        Main.npcChatText = "";
        Main.InGuideCraftMenu = false;
        Main.InReforgeMenu = false;
        if (Main.playerInventory)
            Main.LocalPlayer.ToggleInv();
        Main.playerInventory = false;
        waited = frame;
    }

    static void CloseInventory()
    {
        if (Main.playerInventory)
            Log.Info($"csguide TAP {++taps} 20 20");
        waited = frame;
    }

    static int FreeSlot(Player player, int skip)
    {
        for (int k = 49; k >= 10; k--)
            if (k != skip && player.inventory[k].IsAir)
                return k;
        return skip == 48 ? 47 : 48;
    }

    static List<int> Materials()
    {
        var list = new GameList(GUIInstance.Active.GUICraftGuide._materialItems);
        var slots = new List<int>();
        for (int k = 0; k < list.Count; k++)
            slots.Add(list.Get<int>(k));
        return slots;
    }

    static bool PopupFinds(int inventorySlot, int recipe)
    {
        GUICraftGuide craft = GUIInstance.Active.GUICraftGuide;
        int index = Materials().IndexOf(inventorySlot);
        if (index < 0)
            return false;
        craft._selectedItem = index;
        GUICraftGuidePopup popup = GUIInstance.Active.GUICraftGuidePopup;
        popup.FindRecipes();
        for (int k = 0; k < popup.numAvailableGuideRecipes; k++)
            if (popup.availableGuideRecipe[k] == recipe)
                return true;
        return false;
    }

    static void Step(Player self)
    {
        switch (step)
        {
            case 0 when frame == 30:
            {
                int cx = (int)(self.Center.X / 16), floor = Floor(cx, (int)(self.Bottom.Y / 16) - 2);
                guide = NPC.NewNPC(self.GetSource_FromThis(), (cx + 3) * 16, floor * 16, NPCID.Guide);
                goblin = NPC.NewNPC(self.GetSource_FromThis(), (cx - 3) * 16, floor * 16, NPCID.GoblinTinkerer);
                shop = NPC.NewNPC(self.GetSource_FromThis(), (cx + 6) * 16, floor * 16, ModContent.NPCType<ShopNpc>());
                int ore = ModContent.ItemType<GuideOre>();
                Report("the mod recipes are registered", GuideRecipes.Sword >= 0 && GuideRecipes.Bar >= Recipe.maxRecipes, $"sword={GuideRecipes.Sword} bar={GuideRecipes.Bar} max={Recipe.maxRecipes} total={Recipe.numRecipes}");
                Report("an item used only by mod recipes is a material", ItemID.Sets.IsAMaterial[ore] && new Item(ore).material, $"set={ItemID.Sets.IsAMaterial[ore]} item={new Item(ore).material}");
                slot = FreeSlot(self, -1);
                gelSlot = FreeSlot(self, slot);
                keptOre = self.inventory[slot].Clone();
                keptGel = self.inventory[gelSlot].Clone();
                self.inventory[slot] = new Item(ore, 5);
                self.inventory[gelSlot] = new Item(ItemID.Gel, 10);
                Talk(goblin);
                step = 1;
                break;
            }
            case 1 when frame == waited + 40:
                Tap(option2);
                step = 2;
                break;
            case 2:
                if (Main.InReforgeMenu || frame > waited + 600)
                {
                    Report("a real tap on the Goblin's Reforge button opens it", Main.InReforgeMenu);
                    CloseChat();
                    step = 3;
                }
                break;
            case 3 when frame == waited + 30:
                CloseInventory();
                step = 30;
                break;
            case 30 when !Main.playerInventory && frame > waited + 60 || frame == waited + 600:
                Talk(shop);
                step = 4;
                break;
            case 4 when frame == waited + 40:
                Tap(option1);
                step = 5;
                break;
            case 5:
                if (Main.npcShop > 0 || frame > waited + 600)
                {
                    Report("a real tap on the mod NPC's Shop button opens its shop", Main.npcShop > 0, $"shop={Main.npcShop}");
                    CloseChat();
                    step = 6;
                }
                break;
            case 6 when frame == waited + 30:
                CloseInventory();
                step = 60;
                break;
            case 60 when !Main.playerInventory && frame > waited + 60 || frame == waited + 600:
                Talk(guide);
                step = 7;
                break;
            case 7 when frame == waited + 40:
                Log.Info($"csguide guide buttons: {label1} | {label2}");
                Tap(label1 == Language.GetTextValue("LegacyInterface.25") ? option1 : option2);
                step = 8;
                break;
            case 8:
                if (Main.InGuideCraftMenu && frame > waited + 120 || frame > waited + 600)
                {
                    Report("a real tap on the Guide's Crafting button opens it", Main.InGuideCraftMenu);
                    Log.Info($"csguide SHOT {++taps}");
                    List<int> materials = Materials();
                    Report("the guide lists the mod-only material", materials.Contains(slot), $"slot={slot} list={string.Join(",", materials)}");
                    Report("the guide finds a mod recipe beyond the game's limit", PopupFinds(slot, GuideRecipes.Bar), $"recipe={GuideRecipes.Bar}");
                    Report("the guide finds a mod recipe from a vanilla material", PopupFinds(gelSlot, GuideRecipes.Sword), $"recipe={GuideRecipes.Sword}");
                    CloseChat();
                    self.inventory[slot] = keptOre;
                    self.inventory[gelSlot] = keptGel;
                    Finish();
                }
                break;
        }
    }

    static void Finish()
    {
        step = 99;
        Log.Info($"csguide END pass={pass} fail={fail}");
    }
}
