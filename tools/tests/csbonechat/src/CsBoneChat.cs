using System;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsBoneChat;

public sealed class CsBoneChatMod : Mod
{
    static int frame, pass, fail, step, waited, taps, bone = -1, guide = -1, held = -1;
    static Vector2 spot;
    static Rectangle option1;
    static string label1 = "";

    static ILog Log => ModContent.GetInstance<CsBoneChatMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUINPCDialogue.SetupButtonText += SetupButtonText;
    }

    static void SetupButtonText(On_GUINPCDialogue.orig_SetupButtonText orig, GUINPCDialogue self, ref GameString text, ref Microsoft.Xna.Framework.Graphics.Texture2D texture,
        ref GameString text2, ref Microsoft.Xna.Framework.Graphics.Texture2D texture2, ref int cost, ref bool happy)
    {
        orig(self, ref text, ref texture, ref text2, ref texture2, ref cost, ref happy);
        option1 = GUITransactionButton.GetRegion(NPCDialogue_Layout.Instance.Option1);
        string? first = text;
        label1 = first ?? "";
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csbonechat {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        self.creativeGodMode = true;
        frame++;
        if (held >= 0 && Main.npc[held].active)
        {
            Main.npc[held].Bottom = spot;
            Main.npc[held].velocity = Vector2.Zero;
        }
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

    static Vector2 ScreenFraction(Vector2 world)
    {
        var size = new Vector2(Main.screenWidth, Main.screenHeight);
        Vector2 zoom = Main.GameViewMatrix.Zoom;
        Vector2 screen = (world - Main.screenPosition - size / 2f) * zoom + size / 2f;
        return screen / size;
    }

    static int Spawn(Player self, int type)
    {
        int cx = (int)(self.Center.X / 16) + 4, floor = Floor(cx, (int)(self.Bottom.Y / 16) - 2);
        spot = new Vector2(cx * 16 + 8, floor * 16);
        int n = NPC.NewNPC(self.GetSource_FromThis(), (int)spot.X, (int)spot.Y, type);
        held = n;
        return n;
    }

    static void TapNPC(int n)
    {
        Vector2 at = ScreenFraction(Main.npc[n].Center);
        Log.Info($"csbonechat FTAP {++taps} {at.X:0.0000} {at.Y:0.0000} type={Main.npc[n].type} town={Main.npc[n].townNPC} chat={Main.LocalPlayer.talkNPC}");
        waited = frame;
    }

    static void CloseChat()
    {
        Main.LocalPlayer.SetTalkNPC(-1);
        Main.npcChatText = "";
        Main.npcShop = 0;
        if (Main.playerInventory)
            Main.LocalPlayer.ToggleInv();
        Main.playerInventory = false;
        waited = frame;
    }

    static void Step(Player self)
    {
        switch (step)
        {
            case 0 when frame == 30:
                Main.playerInventory = false;
                bone = Spawn(self, ModContent.Find<ModNPC>("ExampleMod/ExampleBoneMerchant").Type);
                waited = frame;
                step = 1;
                break;
            case 1 when frame == waited + 60:
                Log.Info($"csbonechat SHOT {++taps}");
                TapNPC(bone);
                step = 2;
                break;
            case 2:
                if (Main.LocalPlayer.talkNPC == bone && frame > waited + 40 || frame > waited + 900)
                {
                    NPC npc = Main.npc[bone];
                    Report("a real tap on the Example Bone Merchant opens its chat", Main.LocalPlayer.talkNPC == bone, $"chat={Main.LocalPlayer.talkNPC} text={Main.npcChatText}");
                    Report("the merchant stays a non-town NPC after the tap", !npc.townNPC);
                    bool button = GUIInstance.Active.GUINPCDialogue.ShowHappiness;
                    Report("NoTownNPCHappiness hides the happiness report and button", Main.LocalPlayer.currentShoppingSettings.HappinessReport == "" && !button,
                        $"report='{Main.LocalPlayer.currentShoppingSettings.HappinessReport}' button={button}");
                    Report("the first button is the shop", label1 == Language.GetTextValue("LegacyInterface.28"), $"label={label1}");
                    Log.Info($"csbonechat SHOT {++taps}");
                    Log.Info($"csbonechat TAP {++taps} {option1.Center.X} {option1.Center.Y}");
                    waited = frame;
                    step = 3;
                }
                break;
            case 3:
                if (Main.npcShop > 0 || frame > waited + 600)
                {
                    Report("the Shop button opens the merchant's shop", Main.npcShop > 0, $"shop={Main.npcShop}");
                    CloseChat();
                    Main.npc[bone].active = false;
                    held = -1;
                    step = 4;
                }
                break;
            case 4 when frame == waited + 60:
                guide = Spawn(self, NPCID.Guide);
                waited = frame;
                step = 5;
                break;
            case 5 when frame == waited + 60:
                TapNPC(guide);
                step = 6;
                break;
            case 6:
                if (Main.LocalPlayer.talkNPC == guide || frame > waited + 900)
                {
                    Report("a real tap on the Guide still opens its chat", Main.LocalPlayer.talkNPC == guide, $"chat={Main.LocalPlayer.talkNPC}");
                    CloseChat();
                    Main.npc[guide].active = false;
                    held = -1;
                    Finish();
                }
                break;
        }
    }

    static void Finish()
    {
        step = 99;
        Log.Info($"csbonechat END pass={pass} fail={fail}");
    }
}
