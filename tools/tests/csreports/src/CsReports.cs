using System;
using System.Linq;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsReports;

public sealed class CsReportsMod : Mod
{
    static int frame, pass, fail, step, waited, taps, pet;
    static string option1 = "", option2 = "";
    static Rectangle region1, region2;

    static ILog Log => ModContent.GetInstance<CsReportsMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUINPCDialogue.SetupButtonText += SetupButtonText;
    }

    static void SetupButtonText(On_GUINPCDialogue.orig_SetupButtonText orig, GUINPCDialogue self, ref GameString text, ref Texture2D texture,
        ref GameString text2, ref Texture2D texture2, ref int cost, ref bool happiness)
    {
        orig(self, ref text, ref texture, ref text2, ref texture2, ref cost, ref happiness);
        string? first = text, second = text2;
        option1 = first ?? "";
        option2 = second ?? "";
        region1 = GUITransactionButton.GetRegion(NPCDialogue_Layout.Instance.Option1);
        region2 = GUITransactionButton.GetRegion(NPCDialogue_Layout.Instance.Option2);
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csreports {name}: {(ok ? "ok" : "FAIL")} {detail}");
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

    static void Texts()
    {
        string chat = Language.GetTextValue("Mods.ExampleMod.Dialogue.ExampleTravelingMerchant.HiveBackpackDialogue");
        Report("an item name tag in NPC chat becomes the item's id", chat.Contains($"[i:{ItemID.HiveBackpack}]") && !chat.Contains("[i:HiveBackpack]"), chat);
        Log.Info($"csreports raw open={Language.GetTextValue("CommonItemTooltip.RightClickToOpen")}");
        string hold = Language.GetTextValue("Mods.ExampleMod.Items.HoldStyleShowcase.Tooltip");
        Report("input placeholders become the phone's touch glyphs", !hold.Contains("{InputTrigger") && hold.Contains("<interact>"), hold.Replace('\n', '|'));
        Terraria.UI.ItemTooltip tooltip = Lang.GetTooltip(ModContent.Find<ModItem>("ExampleMod/HoldStyleShowcase").Type);
        string lines = string.Join("|", Enumerable.Range(0, tooltip.Lines).Select(tooltip.GetLine));
        Report("the item tooltip the game draws turns it into the button glyph", !lines.Contains("{InputTrigger") && !lines.Contains("<interact>") && lines.Contains("[ct:"), lines);
    }

    static void CanStack()
    {
        int type = ModContent.Find<ModItem>("ExampleMod/ExampleCanStackItem").Type;
        Item Bag(string crafter)
        {
            var bag = new Item(type);
            bag.ModItem.GetType().GetField("craftedPlayerName")!.SetValue(bag.ModItem, crafter);
            return bag;
        }
        Item a = Bag("Alpha"), b = Bag("Beta"), c = Bag("Alpha");
        Report("bags from different crafters do not stack", !ItemLoader.CanStack(a, b) && ItemLoader.CanStack(a, c), $"ab={ItemLoader.CanStack(a, b)} ac={ItemLoader.CanStack(a, c)}");
    }

    static void Step(Player self)
    {
        switch (step)
        {
            case 0 when frame == 30:
            {
                Texts();
                CanStack();
                pet = NPC.NewNPC(self.GetSource_FromThis(), (int)self.Center.X + 40, (int)self.Bottom.Y - 4, ModContent.Find<ModNPC>("ExampleMod/ExampleTownPet").Type);
                waited = frame;
                step = 1;
                break;
            }
            case 1 when frame == waited + 20:
                Main.playerInventory = false;
                Main.npc[pet].Bottom = self.Bottom + new Vector2(30, 0);
                Main.npc[pet].velocity = Vector2.Zero;
                self.SetTalkNPC(pet);
                Main.npcChatText = Main.npc[pet].GetChat();
                waited = frame;
                step = 2;
                break;
            case 2 when frame == waited + 40:
            {
                string petText = Language.GetTextValue("UI.PetTheAnimal");
                Report("the town pet offers the pet button", option1 == petText || option2 == petText, $"option1={option1} option2={option2} pet={petText}");
                Rectangle region = option1 == petText ? region1 : region2;
                Log.Info($"csreports TAP {++taps} {region.Center.X} {region.Center.Y}");
                waited = frame;
                step = 3;
                break;
            }
            case 3:
                if (self.petting.isPetting || frame > waited + 600)
                {
                    Report("a real tap on the pet button pets the town pet", self.petting.isPetting, $"npc={self.petting.npc}");
                    Main.npcChatText = Language.GetTextValue("Mods.ExampleMod.Dialogue.ExampleTravelingMerchant.HiveBackpackDialogue");
                    waited = frame;
                    step = 4;
                }
                break;
            case 4 when frame == waited + 60:
                Log.Info($"csreports SHOT {++taps}");
                waited = frame;
                step = 5;
                break;
            case 5 when frame == waited + 60:
                self.SetTalkNPC(-1);
                Main.npcChatText = "";
                Finish();
                break;
        }
    }

    static void Finish()
    {
        step = 99;
        Log.Info($"csreports END pass={pass} fail={fail}");
    }
}
