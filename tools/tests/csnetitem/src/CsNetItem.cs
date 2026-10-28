using System;
using CsNetItem.Common;
using CsNetItem.Content.Items;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsNetItem;

public sealed class CsNetItemMod : Mod
{
    const int TaggedValue = 1234, StampMark = 55;
    const string NoteText = "from the client";

    static int frames, pass, fail, seenAt = -1;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsNetItemMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = pass = fail = 0;
            seenAt = -1;
            done = false;
            return;
        }
        if (done || i != Main.myPlayer) return;
        frames++;
        try
        {
            if (Main.netMode == 1 && frames == 120) Client(self);
            if (Main.netMode >= 2 && frames % 60 == 0) Watch();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
    }

    static void Client(Player p)
    {
        done = true;
        for (int k = 0; k < 58; k++)
        {
            Item item = p.inventory[k];
            bool old = item.type == ModContent.ItemType<Tagged>() || item.type == ModContent.ItemType<Noted>() ||
                (item.type == ItemID.Wood && item.GetGlobalItem<Stamp>().Mark == StampMark);
            if (old) item.TurnToAir();
        }
        int tagged = Put(p, ModContent.ItemType<Tagged>()), noted = Put(p, ModContent.ItemType<Noted>()), wood = Put(p, ItemID.Wood);
        if (tagged < 0 || noted < 0 || wood < 0)
        {
            Log.Info("csnetitem client: no free inventory slots");
            return;
        }
        ((Tagged)p.inventory[tagged].ModItem!).Value = TaggedValue;
        ((Noted)p.inventory[noted].ModItem!).Note = NoteText;
        p.inventory[wood].GetGlobalItem<Stamp>().Mark = StampMark;
        foreach (int slot in new[] { tagged, noted, wood })
            NetMessage.SendData(5, -1, -1, default(NetworkText), Main.myPlayer, slot, 0, 0, 0, 0, 0);
        Log.Info($"csnetitem client: sent slots {tagged}, {noted}, {wood}");
    }

    static void Watch()
    {
        if (seenAt < 0)
        {
            for (int who = 0; who < 255 && seenAt < 0; who++)
            {
                Player p = Main.player[who];
                if (!p.IsNull && p.active && who != Main.myPlayer && !Find(p, ModContent.ItemType<Tagged>()).IsNull) seenAt = frames;
            }
            if (seenAt < 0 && frames >= 60 * 60 * 3)
            {
                done = true;
                Report("a client joined with the items", false, "none in 3 minutes");
                Log.Info($"csnetitem END pass={pass} fail={fail}");
            }
            return;
        }
        if (frames - seenAt >= 300) Server();
    }

    static void Server()
    {
        done = true;
        bool any = false;
        for (int who = 0; who < 255; who++)
        {
            Player p = Main.player[who];
            if (p.IsNull || !p.active || who == Main.myPlayer) continue;
            any = true;
            Item tagged = Find(p, ModContent.ItemType<Tagged>()), noted = Find(p, ModContent.ItemType<Noted>());
            Item wood = default;
            for (int k = 0; k < 58 && wood.IsNull; k++)
            {
                Item item = p.inventory[k];
                if (item.type == ItemID.Wood && item.GetGlobalItem<Stamp>().Mark == StampMark) wood = item;
            }
            int value = tagged.IsNull ? -1 : ((Tagged)tagged.ModItem!).Value;
            string note = noted.IsNull ? "(none)" : ((Noted)noted.ModItem!).Note;
            Report($"player {who}: ModItem.NetSend/NetReceive", value == TaggedValue, $"value={value}");
            Report($"player {who}: ModItem.SaveData goes with the item", note == NoteText, $"note='{note}'");
            Report($"player {who}: GlobalItem.NetSend/NetReceive on a vanilla item", !wood.IsNull, wood.IsNull ? "no stamped wood" : "");
        }
        if (!any) Report("a client joined", false, "no other player in the world");
        Log.Info($"csnetitem END pass={pass} fail={fail}");
    }

    static int Put(Player p, int type)
    {
        for (int k = 0; k < 50; k++)
        {
            Item item = p.inventory[k];
            if (item.type > 0 && item.stack > 0) continue;
            item.SetDefaults(type, default);
            return k;
        }
        return -1;
    }

    static Item Find(Player p, int type)
    {
        for (int k = 0; k < 58; k++)
        {
            Item item = p.inventory[k];
            if (item.type == type) return item;
        }
        return default;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csnetitem {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
