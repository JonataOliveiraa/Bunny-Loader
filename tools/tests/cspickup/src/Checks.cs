using System;
using System.Collections.Generic;
using CsPickup.Content.Items;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsPickup;

public static class Checks
{
    const int Stone = 1;

    static int pass, fail, frames;
    static bool done;
    static int untouchable = -1, orb = -1, gel = -1, fullOrb = -1, wood = -1, magnet = -1, styled = -1, far = -1, shy = -1;
    static int gelBefore, woodBefore;
    static float farX, shyX;
    static readonly List<int> filled = new();

    static ILog Log => ModContent.GetInstance<CsPickupMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
        filled.Clear();
        untouchable = orb = gel = fullOrb = wood = magnet = styled = far = shy = -1;
        Untouchable.Seen = "";
        Orb.Calls = FullOrb.Calls = Styled.Calls = 0;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        p.statLife = p.statLifeMax2;
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
        if (frames == 1)
        {
            ClearFloor();
            for (int k = Main.spawnTileX - 30; k <= Main.spawnTileX + 30; k++)
                WorldGen.PlaceTile(k, Floor, Stone, true, true, -1, 0);
        }
        if (frames < 300) Stand(p);

        if (frames == 10)
        {
            untouchable = Drop(p, ModContent.ItemType<Untouchable>(), 0);
            orb = Drop(p, ModContent.ItemType<Orb>(), 0);
            gel = Drop(p, ItemID.Gel, 0);
            gelBefore = Count(p, ItemID.Gel);
        }
        if (frames == 20)
        {
            WorldItem w = Main.item[orb];
            Report("Item.GetWorldItem finds the item on the ground", w.active && w.inner.GetWorldItem() == w, $"active={w.active}");
        }
        if (frames == 60)
        {
            WorldItem u = Main.item[untouchable], g = Main.item[gel], o = Main.item[orb];
            int type = ModContent.ItemType<Untouchable>();
            Report("CanPickup false: the mod item stays on the ground", u.active && Count(p, type) == 0,
                $"active={u.active} inventory={Count(p, type)}");
            Report("CanPickup gets the item and the player", Untouchable.Seen == $"{type} {Main.myPlayer}", Untouchable.Seen);
            Report("GlobalItem.CanPickup false: the gel stays on the ground", g.active && Count(p, ItemID.Gel) == gelBefore,
                $"active={g.active} inventory {gelBefore} -> {Count(p, ItemID.Gel)}");
            Report("OnPickup false: the item is gone and not in the inventory",
                !o.active && Orb.Calls == 1 && Count(p, ModContent.ItemType<Orb>()) == 0,
                $"active={o.active} calls={Orb.Calls} inventory={Count(p, ModContent.ItemType<Orb>())}");
            Remove(untouchable);
            Remove(gel);
        }

        if (frames == 70)
        {
            FillInventory(p);
            Item sample = Item.New();
            sample.SetDefaults(ModContent.ItemType<FullOrb>(), default);
            Item log = Item.New();
            log.SetDefaults(ItemID.Wood, default);
            bool orbFits = p.ItemSpace(sample).CanTakeItem, woodFits = p.ItemSpace(log).CanTakeItem;
            Report("ItemSpace true: the game says it fits with a full inventory", orbFits && !woodFits,
                $"orb={orbFits} wood={woodFits} filled={filled.Count}");
            fullOrb = Drop(p, ModContent.ItemType<FullOrb>(), 0);
            wood = Drop(p, ItemID.Wood, 0);
            woodBefore = Count(p, ItemID.Wood);
        }
        if (frames == 120)
        {
            WorldItem f = Main.item[fullOrb], w = Main.item[wood];
            Report("ItemSpace + OnPickup: picked up and consumed with a full inventory", !f.active && FullOrb.Calls == 1,
                $"active={f.active} calls={FullOrb.Calls}");
            Report("without ItemSpace the wood does not fit and stays", w.active && Count(p, ItemID.Wood) == woodBefore,
                $"active={w.active} inventory {woodBefore} -> {Count(p, ItemID.Wood)}");
            Remove(wood);
            EmptyFilled(p);
        }

        if (frames == 130)
        {
            magnet = Drop(p, ModContent.ItemType<Magnet>(), -20 * 16);
            styled = Drop(p, ModContent.ItemType<Styled>(), 20 * 16);
            far = Drop(p, ItemID.Wood, 12 * 16);
            farX = Main.item[far].position.X;
        }
        if (frames == 131)
        {
            WorldItem w = Main.item[magnet];
            int vanilla = p.GetItemGrabRange(w.inner), range = p.GetItemGrabRange(w);
            Report("GrabRange: GetItemGrabRange(WorldItem) adds the 400", range == vanilla + 400, $"game={vanilla} with mod={range}");
        }
        if (frames == 250)
        {
            WorldItem m = Main.item[magnet], f = Main.item[far];
            Report("GrabRange +400: pulled from 20 tiles and picked up", !m.active && Count(p, ModContent.ItemType<Magnet>()) == 1,
                $"active={m.active} x={(int)(m.position.X / 16)} player={(int)(p.position.X / 16)}");
            Report("GrabStyle: the mod pulled it its own way and it was picked up",
                Styled.Calls > 0 && Count(p, ModContent.ItemType<Styled>()) == 1,
                $"calls={Styled.Calls} inventory={Count(p, ModContent.ItemType<Styled>())}");
            Report("the wood 12 tiles away is not pulled", f.active && Math.Abs(f.position.X - farX) < 2,
                $"active={f.active} x {farX} -> {f.position.X}");
            Remove(far);
            shy = Drop(p, ModContent.ItemType<Shy>(), 30);
            shyX = Main.item[shy].position.X;
        }
        if (frames == 300)
        {
            WorldItem s = Main.item[shy];
            Report("negative GrabRange: close, inside the game's range, not pulled", s.active && Math.Abs(s.position.X - shyX) < 2,
                $"active={s.active} x {shyX} -> {s.position.X}");
            Finish(p);
        }
    }

    static void Finish(Player p)
    {
        done = true;
        try
        {
            Remove(shy);
            EmptyFilled(p);
            var inventory = p.inventory;
            int magnetType = ModContent.ItemType<Magnet>(), styledType = ModContent.ItemType<Styled>();
            for (int k = 0; k < 58; k++)
            {
                int type = inventory[k].type;
                if (type == magnetType || type == styledType) inventory[k].TurnToAir();
            }
            ClearFloor();
            p.position = new Vector2(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
            p.fallStart = Main.spawnTileY - 3;
        }
        catch (Exception e)
        {
            Log.Error($"cspickup cleanup threw: {e}");
        }
        Log.Info($"cspickup END pass={pass} fail={fail}");
    }

    static int Floor => Main.spawnTileY - 40;

    static void ClearFloor()
    {
        for (int i = Main.spawnTileX - 30; i <= Main.spawnTileX + 30; i++)
            for (int j = Floor - 6; j <= Floor; j++)
                WorldGen.KillTile(i, j, false, false, true);
    }

    static void Stand(Player p)
    {
        p.position = new Vector2(Main.spawnTileX * 16 - p.width / 2f, Floor * 16 - p.height);
        p.velocity = new Vector2(0, 0);
        p.fallStart = Floor - 3;
    }

    static int Drop(Player p, int type, int dx)
    {
        int x = (int)(p.position.X + p.width / 2f + dx);
        int y = (int)(p.position.Y + p.height - 12);
        int index = Item.NewItem(X: x, Y: y, Width: 1, Height: 1, Type: type, Stack: 1, noBroadcast: false, pfix: 0, ownership: default);
        Main.item[index].velocity = new Vector2(0, 0);
        return index;
    }

    static int Count(Player p, int type)
    {
        var inventory = p.inventory;
        int n = 0;
        for (int k = 0; k < 58; k++)
        {
            Item item = inventory[k];
            if (item.type == type) n += item.stack;
        }
        return n;
    }

    static void Remove(int index)
    {
        if (index < 0) return;
        WorldItem w = Main.item[index];
        if (w.active) w.TurnToAir();
    }

    static void FillInventory(Player p)
    {
        var inventory = p.inventory;
        for (int k = 0; k < 50; k++)
        {
            if (inventory[k].type != 0) continue;
            inventory[k].SetDefaults(ItemID.DirtBlock, default);
            filled.Add(k);
        }
    }

    static void EmptyFilled(Player p)
    {
        var inventory = p.inventory;
        foreach (int k in filled)
            if (inventory[k].type == ItemID.DirtBlock) inventory[k].TurnToAir();
        filled.Clear();
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cspickup {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
