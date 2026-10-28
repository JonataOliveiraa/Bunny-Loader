using System;
using CsDraw.Common;
using CsDraw.Content.Items;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsDraw;

public static class Checks
{
    static int pass, fail, frames, glow = -1, gel = -1, slot = -1;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsDrawMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        glow = gel = slot = -1;
        done = false;
        Glow.Reset();
        GelDraw.WorldPre = 0;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try { Step(p); }
        catch (Exception e)
        {
            Report($"frame {frames}", false, e.ToString());
            Finish(p);
        }
    }

    static void Step(Player p)
    {
        if (frames == 60)
        {
            Item glowItem = Item.New();
            glowItem.SetDefaults(ModContent.ItemType<Glow>(), default);
            Color white = new(255, 255, 255, 255);
            Color a = glowItem.GetAlpha(white);
            Report("ModItem.GetAlpha replaces the color", Same(a, Glow.Tint), $"got {a}");
            Item gelItem = Item.New();
            gelItem.SetDefaults(ItemID.Gel, default);
            Color b = gelItem.GetAlpha(white);
            Report("GlobalItem.GetAlpha replaces a vanilla item's color", Same(b, GelDraw.Tint), $"got {b}");
            Item wood = Item.New();
            wood.SetDefaults(ItemID.Wood, default);
            Color c = wood.GetAlpha(white);
            Report("GetAlpha null keeps the game's color", !Same(c, GelDraw.Tint) && !Same(c, Glow.Tint), $"got {c}");

            glow = Drop(p, ModContent.ItemType<Glow>(), 6 * 16);
            gel = Drop(p, ItemID.Gel, -6 * 16);
            slot = EmptyHotbarSlot(p);
            if (slot >= 0) p.inventory[slot].SetDefaults(ModContent.ItemType<Glow>(), default);
            Glow.Reset();
            GelDraw.WorldPre = 0;
        }
        if (frames == 120)
        {
            Report("PreDrawInWorld runs for the mod item on the ground", Glow.WorldPre > 0, $"calls={Glow.WorldPre}");
            Report("PostDrawInWorld runs after it", Glow.WorldPost > 0 && Glow.WorldPost <= Glow.WorldPre, $"pre={Glow.WorldPre} post={Glow.WorldPost}");
            Report("PreDrawInWorld gets the index in Main.item and the sprite batch", Glow.LastWhoAmI == glow && Glow.BatchSeen,
                $"whoAmI={Glow.LastWhoAmI} index={glow} batch={Glow.BatchSeen}");
            Report("PostDrawInWorld sees the scale PreDrawInWorld changed", Glow.LastScale > 1.5f, $"scale={Glow.LastScale}");
            Report("GlobalItem.PreDrawInWorld runs for a vanilla item", GelDraw.WorldPre > 0, $"calls={GelDraw.WorldPre}");
            if (slot < 0) Report("inventory drawing", false, "no free hotbar slot");
            else
            {
                Report("PreDrawInInventory runs for the item in the hotbar", Glow.InventoryPre > 0, $"calls={Glow.InventoryPre}");
                Report("PostDrawInInventory runs even when PreDrawInInventory returns false", Glow.InventoryPost > 0,
                    $"pre={Glow.InventoryPre} post={Glow.InventoryPost}");
            }
            Finish(p);
        }
    }

    static void Finish(Player p)
    {
        done = true;
        try
        {
            Remove(glow);
            Remove(gel);
            if (slot >= 0 && p.inventory[slot].type == ModContent.ItemType<Glow>()) p.inventory[slot].TurnToAir();
        }
        catch (Exception e)
        {
            Log.Error($"csdraw cleanup threw: {e}");
        }
        Log.Info($"csdraw END pass={pass} fail={fail}");
    }

    static int Drop(Player p, int type, int dx)
    {
        int x = (int)(p.position.X + p.width / 2f + dx);
        int y = (int)(p.position.Y + p.height / 2f);
        int index = Item.NewItem(X: x, Y: y, Width: 1, Height: 1, Type: type, Stack: 1, noBroadcast: false, pfix: 0, ownership: default);
        WorldItem w = Main.item[index];
        w.velocity = new Vector2(0, 0);
        return index;
    }

    static void Remove(int index)
    {
        if (index < 0) return;
        WorldItem w = Main.item[index];
        if (w.active) w.TurnToAir();
    }

    static int EmptyHotbarSlot(Player p)
    {
        for (int k = 0; k < 10; k++)
        {
            Item item = p.inventory[k];
            if (item.type <= 0 || item.stack <= 0) return k;
        }
        return -1;
    }

    static bool Same(Color a, Color b) => a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csdraw {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
